using System;
using System.Collections.Generic;
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
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;

namespace MoneyFlow.Services.Accounting;

public class AccountingService : IAccountingService
{
    private readonly AppDbContext _context;
    private readonly IVoucherRepository _voucherRepo;
    private readonly ILedgerRepository _ledgerRepo;
    private readonly IFinancialYearRepository _fyRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AccountingService> _logger;

    public AccountingService(
        AppDbContext context,
        IVoucherRepository voucherRepo,
        ILedgerRepository ledgerRepo,
        IFinancialYearRepository fyRepo,
        ICompanyRepository companyRepo,
        IUnitOfWork unitOfWork,
        ILogger<AccountingService> logger)
    {
        _context = context;
        _voucherRepo = voucherRepo;
        _ledgerRepo = ledgerRepo;
        _fyRepo = fyRepo;
        _companyRepo = companyRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public VoucherValidationResult ValidateVoucher(VoucherCreateDto dto, DateTime? fyStartDate = null, DateTime? fyEndDate = null)
    {
        var errors = new List<string>();

        if (dto.Entries == null || dto.Entries.Count < 2)
        {
            errors.Add("A voucher must contain at least two accounting entries.");
        }

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        if (dto.Entries != null)
        {
            for (int i = 0; i < dto.Entries.Count; i++)
            {
                var entry = dto.Entries[i];
                int line = i + 1;

                if (entry.LedgerId <= 0)
                {
                    errors.Add($"Line {line}: A valid ledger must be selected.");
                }

                if (entry.Debit < 0 || entry.Credit < 0)
                {
                    errors.Add($"Line {line}: Debit and Credit amounts cannot be negative.");
                }

                if (entry.Debit > 0 && entry.Credit > 0)
                {
                    errors.Add($"Line {line}: An entry cannot have both Debit and Credit amounts.");
                }

                if (entry.Debit == 0 && entry.Credit == 0)
                {
                    errors.Add($"Line {line}: An entry must have either a Debit or a Credit amount.");
                }

                totalDebit += entry.Debit;
                totalCredit += entry.Credit;
            }
        }

        if (totalDebit != totalCredit)
        {
            decimal diff = Math.Abs(totalDebit - totalCredit);
            errors.Add($"Voucher is not balanced. Debit: ₹{totalDebit:N2}, Credit: ₹{totalCredit:N2}, Difference: ₹{diff:N2}");
        }

        if (fyStartDate.HasValue && fyEndDate.HasValue)
        {
            if (dto.VoucherDate.Date < fyStartDate.Value.Date || dto.VoucherDate.Date > fyEndDate.Value.Date)
            {
                errors.Add($"Voucher date {dto.VoucherDate:dd-MMM-yyyy} is outside the active Financial Year period ({fyStartDate.Value:dd-MMM-yyyy} to {fyEndDate.Value:dd-MMM-yyyy}).");
            }
        }

        return errors.Count == 0
            ? VoucherValidationResult.Success(totalDebit, totalCredit)
            : VoucherValidationResult.Failure(totalDebit, totalCredit, errors.ToArray());
    }

    public async Task<Voucher> SaveVoucherAsync(int companyId, VoucherCreateDto dto, CancellationToken ct = default)
    {
        await using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var company = await _companyRepo.GetByIdAsync(companyId, ct)
                ?? throw new ArgumentException($"Company with ID {companyId} does not exist.", nameof(companyId));

            var fy = await _fyRepo.GetByIdAsync(dto.FinancialYearId, ct)
                ?? throw new ArgumentException($"Financial year with ID {dto.FinancialYearId} does not exist.", nameof(dto.FinancialYearId));

            if (fy.CompanyId != companyId)
            {
                throw new InvalidOperationException("The financial year does not belong to the active company.");
            }

            if (fy.IsClosed)
            {
                throw new InvalidOperationException($"Financial year '{fy.YearName}' is closed/locked for transactions.");
            }

            var validation = ValidateVoucher(dto, fy.StartDate, fy.EndDate);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
            }

            // Validate all referenced ledgers belong to this company
            var ledgerIds = dto.Entries.Select(e => e.LedgerId).Distinct().ToList();
            var ledgers = await _context.Ledgers
                .Where(l => ledgerIds.Contains(l.LedgerId) && l.CompanyId == companyId && l.IsActive)
                .Select(l => l.LedgerId)
                .ToListAsync(ct);

            if (ledgers.Count != ledgerIds.Count)
            {
                throw new InvalidOperationException("One or more selected ledgers do not exist, are inactive, or do not belong to the active company.");
            }

            // Validate Contra vouchers are strictly between Cash and Bank accounts
            var voucherType = await _context.VoucherTypes.FirstOrDefaultAsync(vt => vt.VoucherTypeId == dto.VoucherTypeId, ct);
            if (voucherType?.Type == VoucherTypeEnum.Contra)
            {
                var cashBankLedgers = await GetCashAndBankLedgersAsync(companyId, ct);
                var validCashBankIds = cashBankLedgers.Select(l => l.LedgerId).ToHashSet();
                if (ledgerIds.Any(id => !validCashBankIds.Contains(id)))
                {
                    throw new InvalidOperationException("Contra vouchers can only be recorded between Cash and Bank accounts.");
                }
            }

            var nextVoucherNumber = await _voucherRepo.GetNextVoucherNumberAsync(companyId, dto.VoucherTypeId, dto.FinancialYearId, ct);

            var voucher = new Voucher
            {
                CompanyId = companyId,
                FinancialYearId = dto.FinancialYearId,
                VoucherTypeId = dto.VoucherTypeId,
                VoucherNumber = nextVoucherNumber,
                VoucherDate = dto.VoucherDate,
                ReferenceNumber = dto.ReferenceNumber?.Trim() ?? string.Empty,
                Narration = dto.Narration?.Trim() ?? string.Empty,
                CreatedAt = DateTime.Now,
                CreatedBy = "System",
                IsDeleted = false
            };

            foreach (var entryDto in dto.Entries)
            {
                voucher.VoucherEntries.Add(new VoucherEntry
                {
                    LedgerId = entryDto.LedgerId,
                    Debit = entryDto.Debit,
                    Credit = entryDto.Credit,
                    Narration = entryDto.Narration?.Trim() ?? string.Empty
                });
            }

            await _voucherRepo.AddAsync(voucher, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Successfully saved voucher {VoucherNumber} (ID: {VoucherId}) for Company {CompanyId} with Total ₹{Amount:N2}",
                voucher.VoucherNumber, voucher.VoucherId, companyId, validation.TotalDebit);

            return voucher;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save voucher for Company {CompanyId}. Transaction rolled back.", companyId);
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<LedgerBalanceDto> GetLedgerBalanceAsync(int companyId, int ledgerId, DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var ledger = await _context.Ledgers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId && l.CompanyId == companyId, ct)
            ?? throw new ArgumentException($"Ledger with ID {ledgerId} does not exist in this company.", nameof(ledgerId));

        decimal initialNet = ledger.OpeningBalanceType == BalanceType.Debit
            ? ledger.OpeningBalance
            : -ledger.OpeningBalance;

        var entriesQuery = _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.LedgerId == ledgerId && !ve.Voucher!.IsDeleted && ve.Voucher.CompanyId == companyId);

        if (asOfDate.HasValue)
        {
            var endOfDay = asOfDate.Value.Date.AddDays(1).AddTicks(-1);
            entriesQuery = entriesQuery.Where(ve => ve.Voucher!.VoucherDate <= endOfDay);
        }

        var totals = await entriesQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .FirstOrDefaultAsync(ct);

        decimal totalDebit = totals?.TotalDebit ?? 0m;
        decimal totalCredit = totals?.TotalCredit ?? 0m;

        decimal netClosing = initialNet + totalDebit - totalCredit;

        return new LedgerBalanceDto
        {
            LedgerId = ledgerId,
            LedgerName = ledger.LedgerName,
            OpeningBalance = ledger.OpeningBalance,
            OpeningType = ledger.OpeningBalanceType,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = Math.Abs(netClosing),
            ClosingType = netClosing >= 0 ? BalanceType.Debit : BalanceType.Credit
        };
    }

    public async Task<LedgerStatementDto> GetLedgerStatementAsync(int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var ledger = await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId && l.CompanyId == companyId, ct)
            ?? throw new ArgumentException($"Ledger with ID {ledgerId} does not exist in this company.", nameof(ledgerId));

        var startOfDay = fromDate.Date;
        var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);

        // Calculate opening balance as of fromDate
        decimal initialMasterNet = ledger.OpeningBalanceType == BalanceType.Debit
            ? ledger.OpeningBalance
            : -ledger.OpeningBalance;

        var priorTotals = await _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.LedgerId == ledgerId && !ve.Voucher!.IsDeleted && ve.Voucher.CompanyId == companyId && ve.Voucher.VoucherDate < startOfDay)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .FirstOrDefaultAsync(ct);

        decimal priorDebit = priorTotals?.TotalDebit ?? 0m;
        decimal priorCredit = priorTotals?.TotalCredit ?? 0m;
        decimal openingNet = initialMasterNet + priorDebit - priorCredit;

        // Fetch period vouchers that contain an entry for this ledger
        var periodVouchers = await _context.Vouchers
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .Where(v => v.CompanyId == companyId &&
                        !v.IsDeleted &&
                        v.VoucherDate >= startOfDay &&
                        v.VoucherDate <= endOfDay &&
                        v.VoucherEntries.Any(e => e.LedgerId == ledgerId))
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToListAsync(ct);

        var lines = new List<LedgerStatementLineDto>();
        decimal currentNet = openingNet;
        decimal totalPeriodDebit = 0m;
        decimal totalPeriodCredit = 0m;

        foreach (var v in periodVouchers)
        {
            var myEntries = v.VoucherEntries.Where(e => e.LedgerId == ledgerId).ToList();
            var otherEntries = v.VoucherEntries
                .Where(e => e.LedgerId != ledgerId)
                .Select(e => e.Ledger?.LedgerName ?? "Account")
                .Distinct()
                .ToList();

            string particulars = otherEntries.Count switch
            {
                0 => v.Narration,
                1 => otherEntries[0],
                _ => string.Join(", ", otherEntries)
            };

            foreach (var entry in myEntries)
            {
                currentNet += (entry.Debit - entry.Credit);
                totalPeriodDebit += entry.Debit;
                totalPeriodCredit += entry.Credit;

                lines.Add(new LedgerStatementLineDto
                {
                    VoucherId = v.VoucherId,
                    VoucherNumber = v.VoucherNumber,
                    VoucherTypeName = v.VoucherType?.Name ?? "Voucher",
                    Date = v.VoucherDate,
                    Particulars = particulars,
                    Narration = !string.IsNullOrWhiteSpace(entry.Narration) ? entry.Narration : v.Narration,
                    Debit = entry.Debit,
                    Credit = entry.Credit,
                    RunningBalance = Math.Abs(currentNet),
                    RunningType = currentNet >= 0 ? BalanceType.Debit : BalanceType.Credit
                });
            }
        }

        return new LedgerStatementDto
        {
            LedgerId = ledger.LedgerId,
            LedgerName = ledger.LedgerName,
            GroupName = ledger.Group?.GroupName ?? string.Empty,
            FromDate = fromDate,
            ToDate = toDate,
            OpeningBalance = Math.Abs(openingNet),
            OpeningType = openingNet >= 0 ? BalanceType.Debit : BalanceType.Credit,
            Lines = lines,
            TotalDebit = totalPeriodDebit,
            TotalCredit = totalPeriodCredit,
            ClosingBalance = Math.Abs(currentNet),
            ClosingType = currentNet >= 0 ? BalanceType.Debit : BalanceType.Credit
        };
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(int companyId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var startOfDay = fromDate.Date;
        var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);

        // Fetch all active ledgers with their group
        var ledgers = await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.Group != null ? l.Group.GroupName : string.Empty)
            .ThenBy(l => l.LedgerName)
            .ToListAsync(ct);

        // Fetch all voucher entries up to end of period
        var allEntries = await _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.Voucher!.CompanyId == companyId &&
                         !ve.Voucher.IsDeleted &&
                         ve.Voucher.VoucherDate <= endOfDay)
            .Select(ve => new
            {
                ve.LedgerId,
                ve.Debit,
                ve.Credit,
                ve.Voucher!.VoucherDate
            })
            .ToListAsync(ct);

        var priorLookup = allEntries
            .Where(e => e.VoucherDate < startOfDay)
            .GroupBy(e => e.LedgerId)
            .ToDictionary(
                g => g.Key,
                g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) });

        var periodLookup = allEntries
            .Where(e => e.VoucherDate >= startOfDay && e.VoucherDate <= endOfDay)
            .GroupBy(e => e.LedgerId)
            .ToDictionary(
                g => g.Key,
                g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) });

        var items = new List<TrialBalanceItemDto>();
        decimal totalOpeningDebit = 0m;
        decimal totalOpeningCredit = 0m;
        decimal totalPeriodDebit = 0m;
        decimal totalPeriodCredit = 0m;
        decimal totalClosingDebit = 0m;
        decimal totalClosingCredit = 0m;

        foreach (var ledger in ledgers)
        {
            decimal masterOpeningNet = ledger.OpeningBalanceType == BalanceType.Debit
                ? ledger.OpeningBalance
                : -ledger.OpeningBalance;

            decimal priorDr = priorLookup.TryGetValue(ledger.LedgerId, out var prior) ? prior.Debit : 0m;
            decimal priorCr = prior != null ? prior.Credit : 0m;

            decimal openingNet = masterOpeningNet + priorDr - priorCr;
            decimal openingDebit = openingNet >= 0 ? openingNet : 0m;
            decimal openingCredit = openingNet < 0 ? Math.Abs(openingNet) : 0m;

            decimal periodDr = periodLookup.TryGetValue(ledger.LedgerId, out var cur) ? cur.Debit : 0m;
            decimal periodCr = cur != null ? cur.Credit : 0m;

            decimal closingNet = openingNet + periodDr - periodCr;
            decimal closingDebit = closingNet >= 0 ? closingNet : 0m;
            decimal closingCredit = closingNet < 0 ? Math.Abs(closingNet) : 0m;

            // Only include if there is any balance or activity
            if (openingDebit != 0 || openingCredit != 0 || periodDr != 0 || periodCr != 0 || closingDebit != 0 || closingCredit != 0)
            {
                items.Add(new TrialBalanceItemDto
                {
                    LedgerId = ledger.LedgerId,
                    LedgerName = ledger.LedgerName,
                    GroupId = ledger.GroupId,
                    GroupName = ledger.Group?.GroupName ?? string.Empty,
                    GroupNature = ledger.Group?.Nature ?? GroupNature.Assets,
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = periodDr,
                    PeriodCredit = periodCr,
                    ClosingDebit = closingDebit,
                    ClosingCredit = closingCredit
                });

                totalOpeningDebit += openingDebit;
                totalOpeningCredit += openingCredit;
                totalPeriodDebit += periodDr;
                totalPeriodCredit += periodCr;
                totalClosingDebit += closingDebit;
                totalClosingCredit += closingCredit;
            }
        }

        return new TrialBalanceDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            Items = items,
            TotalOpeningDebit = totalOpeningDebit,
            TotalOpeningCredit = totalOpeningCredit,
            TotalPeriodDebit = totalPeriodDebit,
            TotalPeriodCredit = totalPeriodCredit,
            TotalClosingDebit = totalClosingDebit,
            TotalClosingCredit = totalClosingCredit
        };
    }

    public async Task<VoucherType?> GetVoucherTypeByEnumAsync(VoucherTypeEnum type, CancellationToken ct = default)
    {
        var voucherType = await _context.VoucherTypes
            .FirstOrDefaultAsync(vt => vt.Type == type && vt.IsActive, ct);

        if (voucherType != null) return voucherType;

        // Fallback: If not found in DB (e.g. fresh in-memory test), create default
        string name = type.ToString();
        string code = type switch
        {
            VoucherTypeEnum.Payment => "PMT",
            VoucherTypeEnum.Receipt => "RCT",
            VoucherTypeEnum.Contra => "CTR",
            VoucherTypeEnum.Journal => "JRN",
            VoucherTypeEnum.Sales => "SLS",
            VoucherTypeEnum.Purchase => "PUR",
            VoucherTypeEnum.DebitNote => "DBN",
            VoucherTypeEnum.CreditNote => "CRN",
            _ => "VCH"
        };
        string prefix = type switch
        {
            VoucherTypeEnum.Payment => "PAY-",
            VoucherTypeEnum.Receipt => "RCT-",
            VoucherTypeEnum.Contra => "CTR-",
            VoucherTypeEnum.Journal => "JRN-",
            VoucherTypeEnum.Sales => "SLS-",
            VoucherTypeEnum.Purchase => "PUR-",
            VoucherTypeEnum.DebitNote => "DBN-",
            VoucherTypeEnum.CreditNote => "CRN-",
            _ => "VCH-"
        };

        voucherType = new VoucherType
        {
            Name = name,
            Code = code,
            Type = type,
            Prefix = prefix,
            IsActive = true
        };
        _context.VoucherTypes.Add(voucherType);
        await _unitOfWork.SaveChangesAsync(ct);
        return voucherType;
    }

    public async Task<string> GetNextVoucherNumberPreviewAsync(int companyId, int voucherTypeId, int financialYearId, CancellationToken ct = default)
    {
        return await _voucherRepo.GetNextVoucherNumberAsync(companyId, voucherTypeId, financialYearId, ct);
    }

    public async Task<IReadOnlyList<LedgerSummaryDto>> GetCashAndBankLedgersAsync(int companyId, CancellationToken ct = default)
    {
        var cashBankGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && (g.GroupName == "Cash-in-Hand" || g.GroupName == "Bank Accounts"))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var childGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && g.ParentGroupId.HasValue && cashBankGroupIds.Contains(g.ParentGroupId.Value))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var allTargetGroupIds = cashBankGroupIds.Concat(childGroupIds).Distinct().ToList();

        return await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive && allTargetGroupIds.Contains(l.GroupId))
            .OrderBy(l => l.LedgerName)
            .Select(l => new LedgerSummaryDto
            {
                LedgerId = l.LedgerId,
                GroupId = l.GroupId,
                LedgerName = l.LedgerName,
                GroupName = l.Group != null ? l.Group.GroupName : string.Empty,
                GroupNature = l.Group != null ? l.Group.Nature : GroupNature.Assets,
                OpeningBalance = l.OpeningBalance,
                OpeningBalanceType = l.OpeningBalanceType,
                IsActive = l.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerSummaryDto>> GetCustomerPartyLedgersAsync(int companyId, CancellationToken ct = default)
    {
        var targetGroupNames = new[] { "Sundry Debtors", "Cash-in-Hand", "Bank Accounts" };
        var parentGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && targetGroupNames.Contains(g.GroupName))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var childGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && g.ParentGroupId.HasValue && parentGroupIds.Contains(g.ParentGroupId.Value))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var allTargetGroupIds = parentGroupIds.Concat(childGroupIds).Distinct().ToList();

        return await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive && allTargetGroupIds.Contains(l.GroupId))
            .OrderBy(l => l.LedgerName)
            .Select(l => new LedgerSummaryDto
            {
                LedgerId = l.LedgerId,
                GroupId = l.GroupId,
                LedgerName = l.LedgerName,
                GroupName = l.Group != null ? l.Group.GroupName : string.Empty,
                GroupNature = l.Group != null ? l.Group.Nature : GroupNature.Assets,
                OpeningBalance = l.OpeningBalance,
                OpeningBalanceType = l.OpeningBalanceType,
                IsActive = l.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LedgerSummaryDto>> GetSalesLedgersAsync(int companyId, CancellationToken ct = default)
    {
        var targetGroupNames = new[] { "Sales Accounts", "Direct Income", "Indirect Income" };
        var parentGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && targetGroupNames.Contains(g.GroupName))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var childGroupIds = await _context.Groups
            .Where(g => g.CompanyId == companyId && g.ParentGroupId.HasValue && parentGroupIds.Contains(g.ParentGroupId.Value))
            .Select(g => g.GroupId)
            .ToListAsync(ct);

        var allTargetGroupIds = parentGroupIds.Concat(childGroupIds).Distinct().ToList();

        return await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive && allTargetGroupIds.Contains(l.GroupId))
            .OrderBy(l => l.LedgerName)
            .Select(l => new LedgerSummaryDto
            {
                LedgerId = l.LedgerId,
                GroupId = l.GroupId,
                LedgerName = l.LedgerName,
                GroupName = l.Group != null ? l.Group.GroupName : string.Empty,
                GroupNature = l.Group != null ? l.Group.Nature : GroupNature.Income,
                OpeningBalance = l.OpeningBalance,
                OpeningBalanceType = l.OpeningBalanceType,
                IsActive = l.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<Voucher?> GetVoucherByIdAsync(int voucherId, CancellationToken ct = default)
    {
        return await _voucherRepo.GetVoucherWithEntriesAsync(voucherId, ct);
    }

    public async Task<bool> DeleteVoucherAsync(int voucherId, CancellationToken ct = default)
    {
        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == voucherId, ct);
        if (voucher == null) return false;

        voucher.IsDeleted = true;
        voucher.ModifiedAt = DateTime.Now;
        voucher.ModifiedBy = "System";

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Soft deleted voucher {VoucherNumber} (ID: {VoucherId})", voucher.VoucherNumber, voucher.VoucherId);
        return true;
    }

    public async Task<IReadOnlyList<Voucher>> GetVouchersByTypeAsync(
        int companyId,
        int financialYearId,
        VoucherTypeEnum type,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = _context.Vouchers
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .Where(v => v.CompanyId == companyId &&
                        v.FinancialYearId == financialYearId &&
                        v.VoucherType!.Type == type &&
                        !v.IsDeleted);

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(v => v.VoucherDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(v => v.VoucherDate <= end);
        }

        return await query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.VoucherNumber)
            .ToListAsync(ct);
    }
}
