using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;

namespace MoneyFlow.Services.Company;

public class CompanySplitService : ICompanySplitService
{
    private readonly AppDbContext _context;
    private readonly IBillAllocationService _billService;
    private readonly ICompanyContext _companyContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompanySplitService> _logger;

    public CompanySplitService(
        AppDbContext context,
        IBillAllocationService billService,
        ICompanyContext companyContext,
        IUnitOfWork unitOfWork,
        ILogger<CompanySplitService> logger)
    {
        _context = context;
        _billService = billService;
        _companyContext = companyContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> ValidateSplitEligibilityAsync(int companyId, DateTime splitFromDate, CancellationToken ct = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, ct);

        if (company == null) return false;
        if (splitFromDate.Date <= company.FinancialYearFrom.Date) return false;

        return true;
    }

    public async Task<Core.Entities.Company> SplitCompanyDataAsync(CompanySplitDto dto, CancellationToken ct = default)
    {
        var sourceCompany = await _context.Companies
            .Include(c => c.Groups)
            .Include(c => c.Ledgers)
            .Include(c => c.Units)
            .Include(c => c.StockItems)
            .FirstOrDefaultAsync(c => c.CompanyId == dto.SourceCompanyId, ct)
            ?? throw new ArgumentException($"Source Company with ID {dto.SourceCompanyId} not found.", nameof(dto.SourceCompanyId));

        DateTime splitDate = dto.SplitFromDate.Date;
        DateTime closingDate = splitDate.AddDays(-1);

        using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // 1. Determine new company number & directory
            var existingCompanies = await _context.Companies.AsNoTracking().ToListAsync(ct);
            int maxNum = 10000;
            foreach (var c in existingCompanies)
            {
                if (int.TryParse(c.CompanyNumber, out int n) && n > maxNum)
                {
                    maxNum = n;
                }
            }
            string newCompanyNumber = string.IsNullOrWhiteSpace(dto.NewCompanyNumber)
                ? (maxNum + 1).ToString("D6")
                : dto.NewCompanyNumber.Trim();

            string baseDir = string.IsNullOrWhiteSpace(dto.TargetDataDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MoneyFlow", "Data")
                : dto.TargetDataDirectory.Trim();

            string targetDir = Path.Combine(baseDir, newCompanyNumber);
            Directory.CreateDirectory(targetDir);
            Directory.CreateDirectory(Path.Combine(targetDir, "Backups"));
            Directory.CreateDirectory(Path.Combine(targetDir, "Exports"));

            string newName = string.IsNullOrWhiteSpace(dto.NewCompanyName)
                ? $"{sourceCompany.CompanyName} ({splitDate.Year}-{(splitDate.Year + 1) % 100:D2})"
                : dto.NewCompanyName.Trim();

            // 2. Create New Company Entity
            var newCompany = new Core.Entities.Company
            {
                CompanyName = newName,
                Address = sourceCompany.Address,
                State = sourceCompany.State,
                Country = sourceCompany.Country,
                PAN = sourceCompany.PAN,
                Email = sourceCompany.Email,
                Phone = sourceCompany.Phone,
                Currency = sourceCompany.Currency,
                FinancialYearFrom = splitDate,
                BooksBeginningFrom = splitDate,
                CompanyNumber = newCompanyNumber,
                DataDirectory = targetDir,
                PasswordHash = sourceCompany.PasswordHash,
                PasswordSalt = sourceCompany.PasswordSalt,
                AutoBackupOnExit = sourceCompany.AutoBackupOnExit,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            await _context.Companies.AddAsync(newCompany, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 3. Duplicate Units
            var unitMap = new Dictionary<int, int>();
            foreach (var u in sourceCompany.Units)
            {
                var newUnit = new Unit
                {
                    CompanyId = newCompany.CompanyId,
                    UnitName = u.UnitName,
                    FormalName = u.FormalName,
                    DecimalPlaces = u.DecimalPlaces,
                    IsActive = u.IsActive
                };
                await _context.Units.AddAsync(newUnit, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                unitMap[u.UnitId] = newUnit.UnitId;
            }

            // 4. Duplicate Groups preserving hierarchy
            var groupMap = new Dictionary<int, int>();
            var primaryGroups = sourceCompany.Groups.Where(g => g.ParentGroupId == null).ToList();
            var subGroups = sourceCompany.Groups.Where(g => g.ParentGroupId != null).ToList();

            foreach (var pg in primaryGroups)
            {
                var newGroup = new GroupEntity
                {
                    CompanyId = newCompany.CompanyId,
                    GroupName = pg.GroupName,
                    Nature = pg.Nature,
                    PrimaryGroup = pg.PrimaryGroup,
                    AffectProfitLoss = pg.AffectProfitLoss,
                    ParentGroupId = null
                };
                await _context.Groups.AddAsync(newGroup, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                groupMap[pg.GroupId] = newGroup.GroupId;
            }

            foreach (var sg in subGroups)
            {
                int? newParentId = sg.ParentGroupId.HasValue && groupMap.TryGetValue(sg.ParentGroupId.Value, out int pid)
                    ? pid
                    : null;

                var newGroup = new GroupEntity
                {
                    CompanyId = newCompany.CompanyId,
                    GroupName = sg.GroupName,
                    Nature = sg.Nature,
                    PrimaryGroup = sg.PrimaryGroup,
                    AffectProfitLoss = sg.AffectProfitLoss,
                    ParentGroupId = newParentId
                };
                await _context.Groups.AddAsync(newGroup, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                groupMap[sg.GroupId] = newGroup.GroupId;
            }

            // 5. Query all Voucher Entries for Source Company up to closingDate
            var sourceEntries = await _context.VoucherEntries
                .AsNoTracking()
                .Include(ve => ve.Voucher)
                .Where(ve => ve.Voucher != null
                          && ve.Voucher.CompanyId == sourceCompany.CompanyId
                          && !ve.Voucher.IsDeleted
                          && ve.Voucher.VoucherDate.Date <= closingDate)
                .ToListAsync(ct);

            // Group entries by LedgerId
            var entriesByLedger = sourceEntries.GroupBy(ve => ve.LedgerId).ToDictionary(g => g.Key, g => g.ToList());

            // 6. Calculate Net Profit/Loss of the prior period to transfer to Capital
            decimal totalIncome = 0m;
            decimal totalExpense = 0m;

            var pnlLedgerIds = sourceCompany.Ledgers
                .Where(l => l.Group != null && l.Group.AffectProfitLoss)
                .Select(l => l.LedgerId)
                .ToHashSet();

            foreach (var ledgerId in pnlLedgerIds)
            {
                if (entriesByLedger.TryGetValue(ledgerId, out var entries))
                {
                    decimal debits = entries.Sum(e => e.Debit);
                    decimal credits = entries.Sum(e => e.Credit);
                    var l = sourceCompany.Ledgers.First(x => x.LedgerId == ledgerId);
                    if (l.Group?.Nature == GroupNature.Income)
                    {
                        totalIncome += (credits - debits);
                    }
                    else if (l.Group?.Nature == GroupNature.Expenses)
                    {
                        totalExpense += (debits - credits);
                    }
                }
            }
            decimal netProfit = totalIncome - totalExpense;

            // 7. Duplicate Ledgers with Closing Balances as Opening Balances
            var ledgerMap = new Dictionary<int, int>();
            foreach (var l in sourceCompany.Ledgers)
            {
                int newGroupId = groupMap.TryGetValue(l.GroupId, out int gid) ? gid : groupMap.Values.First();
                decimal closingBalance = 0m;
                BalanceType closingType = BalanceType.Debit;

                // Only Balance Sheet items carry forward balances
                bool isBalanceSheet = l.Group == null || !l.Group.AffectProfitLoss;
                if (isBalanceSheet)
                {
                    decimal openingNet = l.OpeningBalanceType == BalanceType.Debit ? l.OpeningBalance : -l.OpeningBalance;
                    decimal debitSum = entriesByLedger.TryGetValue(l.LedgerId, out var entries) ? entries.Sum(e => e.Debit) : 0m;
                    decimal creditSum = entriesByLedger.TryGetValue(l.LedgerId, out var entries2) ? entries2.Sum(e => e.Credit) : 0m;
                    decimal net = openingNet + debitSum - creditSum;

                    // If Capital Account, add net profit of previous year
                    if (l.Group?.GroupName.Equals("Capital Account", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        net -= netProfit; // Credits increase capital
                    }

                    if (net >= 0)
                    {
                        closingBalance = net;
                        closingType = BalanceType.Debit;
                    }
                    else
                    {
                        closingBalance = Math.Abs(net);
                        closingType = BalanceType.Credit;
                    }
                }

                var newLedger = new LedgerEntity
                {
                    CompanyId = newCompany.CompanyId,
                    GroupId = newGroupId,
                    LedgerName = l.LedgerName,
                    OpeningBalance = closingBalance,
                    OpeningBalanceType = closingType,
                    Address = l.Address,
                    State = l.State,
                    PAN = l.PAN,
                    Email = l.Email,
                    Phone = l.Phone,
                    BankName = l.BankName,
                    BankAccountNumber = l.BankAccountNumber,
                    IFSC = l.IFSC,
                    IsActive = l.IsActive,
                    CreatedAt = DateTime.Now
                };

                await _context.Ledgers.AddAsync(newLedger, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                ledgerMap[l.LedgerId] = newLedger.LedgerId;
            }

            // 8. Duplicate Stock Items
            foreach (var si in sourceCompany.StockItems)
            {
                int? newUnitId = si.UnitId.HasValue && unitMap.TryGetValue(si.UnitId.Value, out int uid) ? uid : null;
                var newStockItem = new StockItem
                {
                    CompanyId = newCompany.CompanyId,
                    ItemName = si.ItemName,
                    UnitId = newUnitId,
                    OpeningQuantity = si.OpeningQuantity,
                    OpeningRate = si.OpeningRate,
                    OpeningValue = si.OpeningValue,
                    IsActive = si.IsActive,
                    CreatedAt = DateTime.Now
                };
                await _context.StockItems.AddAsync(newStockItem, ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 9. Carry forward pending unpaid bills into new company as opening bills
            var pendingBills = new List<PendingBillDto>();
            foreach (var srcLedger in sourceCompany.Ledgers)
            {
                if (ledgerMap.TryGetValue(srcLedger.LedgerId, out int newLedgerId))
                {
                    var pending = await _billService.GetPendingBillsAsync(sourceCompany.CompanyId, srcLedger.LedgerId, ct);
                    if (pending.Count > 0)
                    {
                        // Create opening voucher entry placeholder or bill allocation for new year
                        var dummyVoucher = new Voucher
                        {
                            CompanyId = newCompany.CompanyId,
                            FinancialYearId = 0, // Will attach to initial FY
                            VoucherTypeId = 1,
                            VoucherNumber = "OP-BILLS",
                            VoucherDate = splitDate,
                            Narration = "Opening Unpaid Bills from previous year",
                            CreatedAt = DateTime.Now
                        };
                        var dummyEntry = new VoucherEntry
                        {
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
                        // Save opening bills
                        await _context.Vouchers.AddAsync(dummyVoucher, ct);
                    }
                }
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // 10. Create Initial Financial Year for New Company
            int startYear = splitDate.Year;
            int endYear = startYear + 1;
            string fyName = $"{startYear}-{(endYear % 100):D2}";

            var financialYear = new FinancialYearEntity
            {
                CompanyId = newCompany.CompanyId,
                YearName = fyName,
                StartDate = splitDate,
                EndDate = splitDate.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.Now
            };
            await _context.FinancialYears.AddAsync(financialYear, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Update dummy opening vouchers to reference the created FY
            var openingVouchers = await _context.Vouchers
                .Where(v => v.CompanyId == newCompany.CompanyId && v.FinancialYearId == 0)
                .ToListAsync(ct);
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
