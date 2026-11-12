using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Repositories;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;

namespace MoneyFlow.Services.Company;

public class CompanySplitService : ICompanySplitService
{
    private readonly IBillAllocationService _billService;
    private readonly ICompanyContext _companyContext;
    private readonly ICompanyDataStore _dataStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanySplitService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public CompanySplitService(
        IBillAllocationService billService,
        ICompanyContext companyContext,
        ICompanyDataStore dataStore,
        IUnitOfWork unitOfWork,
        ILogger<CompanySplitService> logger)
    {
        _billService = billService;
        _companyContext = companyContext;
        _dataStore = dataStore;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    internal CompanySplitService(
        MoneyFlow.Data.AppDbContext context,
        IBillAllocationService billService,
        ICompanyContext companyContext,
        ICompanyDataStore dataStore,
        IUnitOfWork unitOfWork,
        ILogger<CompanySplitService> logger)
        : this(billService, companyContext, dataStore, unitOfWork, logger)
    {
    }

    internal CompanySplitService(
        MoneyFlow.Data.AppDbContext context,
        IBillAllocationService billService,
        ICompanyContext companyContext,
        IUnitOfWork unitOfWork,
        ILogger<CompanySplitService> logger)
        : this(billService, companyContext, (ICompanyDataStore)null!, unitOfWork, logger)
    {
    }

    public Task<bool> ValidateSplitEligibilityAsync(int companyId, DateTime splitFromDate, CancellationToken ct = default)
    {
        var company = _dataStore.GetEntityList<Core.Entities.Company>().FirstOrDefault(c => c.CompanyId == companyId);
        if (company == null) return Task.FromResult(false);
        if (splitFromDate.Date <= company.FinancialYearFrom.Date) return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public async Task<Core.Entities.Company> SplitCompanyDataAsync(CompanySplitDto dto, CancellationToken ct = default)
    {
        var sourceCompany = _dataStore.GetEntityList<Core.Entities.Company>()
            .FirstOrDefault(c => c.CompanyId == dto.SourceCompanyId)
            ?? throw new ArgumentException($"Source Company with ID {dto.SourceCompanyId} not found.", nameof(dto.SourceCompanyId));

        sourceCompany.Groups = _dataStore.GetEntityList<GroupEntity>().Where(g => g.CompanyId == sourceCompany.CompanyId).ToList();
        sourceCompany.Ledgers = _dataStore.GetEntityList<LedgerEntity>().Where(l => l.CompanyId == sourceCompany.CompanyId).ToList();
        sourceCompany.Units = _dataStore.GetEntityList<Unit>().Where(u => u.CompanyId == sourceCompany.CompanyId).ToList();
        sourceCompany.StockItems = _dataStore.GetEntityList<StockItem>().Where(s => s.CompanyId == sourceCompany.CompanyId).ToList();

        DateTime splitDate = dto.SplitFromDate.Date;
        DateTime closingDate = splitDate.AddDays(-1);

        using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 1. Determine new company number & directory
            var existingCompanies = _dataStore.GetEntityList<Core.Entities.Company>();
            int maxNum = 10000;
            foreach (var c in existingCompanies)
            {
                if (int.TryParse(c.CompanyNumber, out int n) && n > maxNum)
                {
                    maxNum = n;
                }
            }
            string newCompanyNumber = (maxNum + 1).ToString();
            string newName = string.IsNullOrWhiteSpace(dto.NewCompanyName)
                ? $"{sourceCompany.CompanyName} (From {splitDate:yyyy})"
                : dto.NewCompanyName.Trim();

            string baseDir = Path.GetDirectoryName(sourceCompany.DataDirectory)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string targetDir = Path.Combine(baseDir, newCompanyNumber);
            Directory.CreateDirectory(targetDir);

            // 2. Create New Company entity
            int newCompId = existingCompanies.Count > 0 ? existingCompanies.Max(c => c.CompanyId) + 1 : 1;
            var newCompany = new Core.Entities.Company
            {
                CompanyId = newCompId,
                CompanyName = newName,
                CompanyNumber = newCompanyNumber,
                Address = sourceCompany.Address,
                State = sourceCompany.State,
                Country = sourceCompany.Country,
                Phone = sourceCompany.Phone,
                Email = sourceCompany.Email,
                PAN = sourceCompany.PAN,
                Currency = sourceCompany.Currency,
                DataDirectory = targetDir,
                FinancialYearFrom = splitDate,
                BooksBeginningFrom = splitDate,
                PasswordHash = sourceCompany.PasswordHash,
                PasswordSalt = sourceCompany.PasswordSalt,
                AutoBackupOnExit = sourceCompany.AutoBackupOnExit,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _dataStore.GetEntityList<Core.Entities.Company>().Add(newCompany);
            await _unitOfWork.SaveChangesAsync(ct);

            // 3. Duplicate Units
            var unitMap = new Dictionary<int, int>();
            int nextUnitId = _dataStore.GetEntityList<Unit>().Count > 0 ? _dataStore.GetEntityList<Unit>().Max(u => u.UnitId) + 1 : 1;
            foreach (var u in sourceCompany.Units)
            {
                var newUnit = new Unit
                {
                    UnitId = nextUnitId++,
                    CompanyId = newCompany.CompanyId,
                    UnitName = u.UnitName,
                    FormalName = u.FormalName,
                    DecimalPlaces = u.DecimalPlaces,
                    IsActive = u.IsActive
                };
                _dataStore.GetEntityList<Unit>().Add(newUnit);
                unitMap[u.UnitId] = newUnit.UnitId;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 4. Duplicate Groups preserving hierarchy
            var groupMap = new Dictionary<int, int>();
            var primaryGroups = sourceCompany.Groups.Where(g => g.ParentGroupId == null).ToList();
            var subGroups = sourceCompany.Groups.Where(g => g.ParentGroupId != null).ToList();

            int nextGroupId = _dataStore.GetEntityList<GroupEntity>().Count > 0 ? _dataStore.GetEntityList<GroupEntity>().Max(g => g.GroupId) + 1 : 1;
            foreach (var pg in primaryGroups)
            {
                var newGroup = new GroupEntity
                {
                    GroupId = nextGroupId++,
                    CompanyId = newCompany.CompanyId,
                    GroupName = pg.GroupName,
                    Nature = pg.Nature,
                    PrimaryGroup = pg.PrimaryGroup,
                    AffectProfitLoss = pg.AffectProfitLoss,
                    ParentGroupId = null,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _dataStore.GetEntityList<GroupEntity>().Add(newGroup);
                groupMap[pg.GroupId] = newGroup.GroupId;
            }

            foreach (var sg in subGroups)
            {
                int? newParentId = sg.ParentGroupId.HasValue && groupMap.TryGetValue(sg.ParentGroupId.Value, out int pid)
                    ? pid
                    : (int?)null;

                var newGroup = new GroupEntity
                {
                    GroupId = nextGroupId++,
                    CompanyId = newCompany.CompanyId,
                    GroupName = sg.GroupName,
                    Nature = sg.Nature,
                    PrimaryGroup = sg.PrimaryGroup,
                    AffectProfitLoss = sg.AffectProfitLoss,
                    ParentGroupId = newParentId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _dataStore.GetEntityList<GroupEntity>().Add(newGroup);
                groupMap[sg.GroupId] = newGroup.GroupId;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 5. Query all Voucher Entries for Source Company up to closingDate
            var allVouchers = _dataStore.GetEntityList<Voucher>()
                .Where(v => v.CompanyId == sourceCompany.CompanyId && !v.IsDeleted && v.VoucherDate.Date <= closingDate)
                .ToDictionary(v => v.VoucherId);

            var sourceEntries = _dataStore.GetEntityList<VoucherEntry>()
                .Where(ve => allVouchers.ContainsKey(ve.VoucherId))
                .ToList();

            // Group entries by LedgerId
            var entriesByLedger = sourceEntries.GroupBy(ve => ve.LedgerId).ToDictionary(g => g.Key, g => g.ToList());

            // 6. Calculate Net Profit/Loss of the prior period to transfer to Capital
            decimal totalIncome = 0m;
            decimal totalExpense = 0m;

            var pnlLedgerIds = sourceCompany.Ledgers
                .Where(l => l.Group != null && l.Group.AffectProfitLoss)
                .Select(l => l.LedgerId)
                .ToHashSet();

            foreach (var lid in pnlLedgerIds)
            {
                if (entriesByLedger.TryGetValue(lid, out var entries))
                {
                    decimal d = entries.Sum(e => e.Debit);
                    decimal c = entries.Sum(e => e.Credit);
                    var srcLedger = sourceCompany.Ledgers.First(l => l.LedgerId == lid);
                    if (srcLedger.Group?.Nature == GroupNature.Income)
                    {
                        totalIncome += (c - d);
                    }
                    else
                    {
                        totalExpense += (d - c);
                    }
                }
            }

            decimal netProfitLoss = totalIncome - totalExpense;

            // 7. Duplicate Ledgers with computed Closing Balances
            var ledgerMap = new Dictionary<int, int>();
            int nextLedgerId = _dataStore.GetEntityList<LedgerEntity>().Count > 0 ? _dataStore.GetEntityList<LedgerEntity>().Max(l => l.LedgerId) + 1 : 1;

            foreach (var l in sourceCompany.Ledgers)
            {
                if (!groupMap.TryGetValue(l.GroupId, out int newGroupId))
                {
                    continue;
                }

                decimal closingBal = 0m;
                var closingBalType = BalanceType.Debit;

                if (pnlLedgerIds.Contains(l.LedgerId))
                {
                    closingBal = 0m;
                    closingBalType = BalanceType.Debit;
                }
                else
                {
                    decimal opDr = l.OpeningBalanceType == BalanceType.Debit ? l.OpeningBalance : 0m;
                    decimal opCr = l.OpeningBalanceType == BalanceType.Credit ? l.OpeningBalance : 0m;
                    decimal txDr = 0m;
                    decimal txCr = 0m;

                    if (entriesByLedger.TryGetValue(l.LedgerId, out var txs))
                    {
                        txDr = txs.Sum(e => e.Debit);
                        txCr = txs.Sum(e => e.Credit);
                    }

                    decimal netBal = (opDr + txDr) - (opCr + txCr);

                    if (l.Group?.GroupName.Equals("Capital Account", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        netBal -= netProfitLoss;
                    }

                    if (netBal >= 0)
                    {
                        closingBal = netBal;
                        closingBalType = BalanceType.Debit;
                    }
                    else
                    {
                        closingBal = -netBal;
                        closingBalType = BalanceType.Credit;
                    }
                }

                var newLedger = new LedgerEntity
                {
                    LedgerId = nextLedgerId++,
                    CompanyId = newCompany.CompanyId,
                    GroupId = newGroupId,
                    LedgerName = l.LedgerName,
                    OpeningBalance = closingBal,
                    OpeningBalanceType = closingBalType,
                    CreditLimit = l.CreditLimit,
                    CreditDays = l.CreditDays,
                    Address = l.Address,
                    Phone = l.Phone,
                    Email = l.Email,
                    PAN = l.PAN,
                    State = l.State,
                    BankName = l.BankName,
                    BankAccountNumber = l.BankAccountNumber,
                    IFSC = l.IFSC,
                    IsActive = l.IsActive,
                    CreatedAt = DateTime.Now
                };

                _dataStore.GetEntityList<LedgerEntity>().Add(newLedger);
                ledgerMap[l.LedgerId] = newLedger.LedgerId;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 8. Duplicate Stock Items
            int nextStockId = _dataStore.GetEntityList<StockItem>().Count > 0 ? _dataStore.GetEntityList<StockItem>().Max(s => s.StockItemId) + 1 : 1;
            foreach (var si in sourceCompany.StockItems)
            {
                int? newUnitId = si.UnitId.HasValue && unitMap.TryGetValue(si.UnitId.Value, out int uid) ? uid : (int?)null;
                var newStockItem = new StockItem
                {
                    StockItemId = nextStockId++,
                    CompanyId = newCompany.CompanyId,
                    ItemName = si.ItemName,
                    UnitId = newUnitId,
                    OpeningQuantity = si.OpeningQuantity,
                    OpeningRate = si.OpeningRate,
                    OpeningValue = si.OpeningValue,
                    IsActive = si.IsActive,
                    CreatedAt = DateTime.Now
                };
                _dataStore.GetEntityList<StockItem>().Add(newStockItem);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 9. Carry forward pending unpaid bills into new company as opening bills
            int nextVoucherId = _dataStore.GetEntityList<Voucher>().Count > 0 ? _dataStore.GetEntityList<Voucher>().Max(v => v.VoucherId) + 1 : 1;
            int nextEntryId = _dataStore.GetEntityList<VoucherEntry>().Count > 0 ? _dataStore.GetEntityList<VoucherEntry>().Max(ve => ve.VoucherEntryId) + 1 : 1;

            foreach (var srcLedger in sourceCompany.Ledgers)
            {
                if (ledgerMap.TryGetValue(srcLedger.LedgerId, out int newLedgerId))
                {
                    var pending = await _billService.GetPendingBillsAsync(sourceCompany.CompanyId, srcLedger.LedgerId, ct);
                    if (pending.Count > 0)
                    {
                        var dummyVoucher = new Voucher
                        {
                            VoucherId = nextVoucherId++,
                            CompanyId = newCompany.CompanyId,
                            FinancialYearId = 0,
                            VoucherTypeId = 1,
                            VoucherNumber = "OP-BILLS",
                            VoucherDate = splitDate,
                            Narration = "Opening Unpaid Bills from previous year",
                            CreatedAt = DateTime.Now
                        };
                        var dummyEntry = new VoucherEntry
                        {
                            VoucherEntryId = nextEntryId++,
                            VoucherId = dummyVoucher.VoucherId,
                            LedgerId = newLedgerId,
                            Debit = srcLedger.OpeningBalanceType == BalanceType.Debit ? pending.Sum(p => p.PendingAmount) : 0m,
                            Credit = srcLedger.OpeningBalanceType == BalanceType.Credit ? pending.Sum(p => p.PendingAmount) : 0m,
                            Narration = "Opening Bill Balances"
                        };

                        foreach (var p in pending)
                        {
                            dummyEntry.BillAllocations.Add(new BillAllocation
                            {
                                CompanyId = newCompany.CompanyId,
                                LedgerId = newLedgerId,
                                BillType = BillType.NewRef,
                                BillName = p.BillName,
                                DueDate = p.DueDate,
                                CreditDays = p.CreditDays,
                                Amount = p.PendingAmount,
                                CreatedAt = DateTime.Now
                            });
                        }

                        dummyVoucher.VoucherEntries.Add(dummyEntry);
                        _dataStore.GetEntityList<Voucher>().Add(dummyVoucher);
                        _dataStore.GetEntityList<VoucherEntry>().Add(dummyEntry);
                    }
                }
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 10. Create Initial Financial Year for New Company
            int startYear = splitDate.Year;
            int endYear = startYear + 1;
            string fyName = $"{startYear}-{(endYear % 100):D2}";
            int nextFyId = _dataStore.GetEntityList<FinancialYearEntity>().Count > 0 ? _dataStore.GetEntityList<FinancialYearEntity>().Max(f => f.FinancialYearId) + 1 : 1;

            var financialYear = new FinancialYearEntity
            {
                FinancialYearId = nextFyId,
                CompanyId = newCompany.CompanyId,
                YearName = fyName,
                StartDate = splitDate,
                EndDate = splitDate.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.Now
            };
            _dataStore.GetEntityList<FinancialYearEntity>().Add(financialYear);
            await _unitOfWork.SaveChangesAsync(ct);

            // Update dummy opening vouchers to reference the created FY
            var openingVouchers = _dataStore.GetEntityList<Voucher>()
                .Where(v => v.CompanyId == newCompany.CompanyId && v.FinancialYearId == 0)
                .ToList();
            foreach (var ov in openingVouchers)
            {
                ov.FinancialYearId = financialYear.FinancialYearId;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            _logger.LogInformation("Successfully split Company {SourceId} into {NewId} ({NewName}) as of {Date}.",
                sourceCompany.CompanyId, newCompany.CompanyId, newCompany.CompanyName, splitDate);

            if (dto.AutoOpenAfterSplit)
            {
                _companyContext.SetActiveCompany(newCompany, financialYear);
            }

            return newCompany;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to split company data for Company ID {CompanyId}.", dto.SourceCompanyId);
            throw;
        }
    }

    public Task<Core.Entities.Company> SplitCompanyAsync(CompanySplitDto dto, CancellationToken ct = default)
    {
        return SplitCompanyDataAsync(dto, ct);
    }
}
