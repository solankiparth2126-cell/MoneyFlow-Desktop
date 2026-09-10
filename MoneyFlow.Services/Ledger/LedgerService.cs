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

namespace MoneyFlow.Services.Ledger;

public class LedgerService : ILedgerService
{
    private readonly AppDbContext _context;
    private readonly ILedgerRepository _ledgerRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(
        AppDbContext context,
        ILedgerRepository ledgerRepo,
        IGroupRepository groupRepo,
        ICompanyRepository companyRepo,
        IUnitOfWork unitOfWork,
        ILogger<LedgerService> logger)
    {
        _context = context;
        _ledgerRepo = ledgerRepo;
        _groupRepo = groupRepo;
        _companyRepo = companyRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LedgerEntity> CreateLedgerAsync(int companyId, LedgerCreateDto dto, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct)
            ?? throw new ArgumentException($"Company with ID {companyId} does not exist.", nameof(companyId));

        if (string.IsNullOrWhiteSpace(dto.LedgerName))
        {
            throw new ArgumentException("Ledger name cannot be empty.", nameof(dto.LedgerName));
        }

        var group = await _groupRepo.GetByIdAsync(dto.GroupId, ct)
            ?? throw new ArgumentException($"Group with ID {dto.GroupId} does not exist.", nameof(dto.GroupId));

        if (group.CompanyId != companyId)
        {
            throw new InvalidOperationException("The specified group does not belong to the active company.");
        }

        if (dto.OpeningBalance < 0)
        {
            throw new ArgumentException("Opening balance cannot be negative.", nameof(dto.OpeningBalance));
        }

        var nameExists = await _ledgerRepo.LedgerNameExistsAsync(companyId, dto.LedgerName.Trim(), null, ct);
        if (nameExists)
        {
            throw new InvalidOperationException($"A ledger named '{dto.LedgerName.Trim()}' already exists in this company.");
        }

        var ledger = new LedgerEntity
        {
            CompanyId = companyId,
            GroupId = dto.GroupId,
            LedgerName = dto.LedgerName.Trim(),
            OpeningBalance = dto.OpeningBalance,
            OpeningBalanceType = dto.OpeningBalanceType,
            Address = dto.Address?.Trim() ?? string.Empty,
            Phone = dto.Phone?.Trim() ?? string.Empty,
            Email = dto.Email?.Trim() ?? string.Empty,
            PAN = dto.PAN?.Trim().ToUpperInvariant() ?? string.Empty,
            State = dto.State?.Trim() ?? string.Empty,
            CreditLimit = dto.CreditLimit,
            CreditDays = dto.CreditDays,
            BankName = dto.BankName?.Trim() ?? string.Empty,
            BankAccountNumber = dto.BankAccountNumber?.Trim() ?? string.Empty,
            IFSC = dto.IFSC?.Trim().ToUpperInvariant() ?? string.Empty,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        await _ledgerRepo.AddAsync(ledger, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created ledger '{LedgerName}' (ID: {LedgerId}) for Company {CompanyId} under Group '{GroupName}'",
            ledger.LedgerName, ledger.LedgerId, companyId, group.GroupName);

        return ledger;
    }

    public async Task<LedgerEntity> UpdateLedgerAsync(LedgerUpdateDto dto, CancellationToken ct = default)
    {
        var ledger = await _ledgerRepo.GetByIdAsync(dto.LedgerId, ct)
            ?? throw new ArgumentException($"Ledger with ID {dto.LedgerId} does not exist.", nameof(dto.LedgerId));

        if (string.IsNullOrWhiteSpace(dto.LedgerName))
        {
            throw new ArgumentException("Ledger name cannot be empty.", nameof(dto.LedgerName));
        }

        var group = await _groupRepo.GetByIdAsync(dto.GroupId, ct)
            ?? throw new ArgumentException($"Group with ID {dto.GroupId} does not exist.", nameof(dto.GroupId));

        if (group.CompanyId != ledger.CompanyId)
        {
            throw new InvalidOperationException("The specified group does not belong to this company.");
        }

        if (dto.OpeningBalance < 0)
        {
            throw new ArgumentException("Opening balance cannot be negative.", nameof(dto.OpeningBalance));
        }

        var nameExists = await _ledgerRepo.LedgerNameExistsAsync(ledger.CompanyId, dto.LedgerName.Trim(), ledger.LedgerId, ct);
        if (nameExists)
        {
            throw new InvalidOperationException($"A ledger named '{dto.LedgerName.Trim()}' already exists in this company.");
        }

        ledger.GroupId = dto.GroupId;
        ledger.LedgerName = dto.LedgerName.Trim();
        ledger.OpeningBalance = dto.OpeningBalance;
        ledger.OpeningBalanceType = dto.OpeningBalanceType;
        ledger.Address = dto.Address?.Trim() ?? string.Empty;
        ledger.Phone = dto.Phone?.Trim() ?? string.Empty;
        ledger.Email = dto.Email?.Trim() ?? string.Empty;
        ledger.PAN = dto.PAN?.Trim().ToUpperInvariant() ?? string.Empty;
        ledger.State = dto.State?.Trim() ?? string.Empty;
        ledger.CreditLimit = dto.CreditLimit;
        ledger.CreditDays = dto.CreditDays;
        ledger.BankName = dto.BankName?.Trim() ?? string.Empty;
        ledger.BankAccountNumber = dto.BankAccountNumber?.Trim() ?? string.Empty;
        ledger.IFSC = dto.IFSC?.Trim().ToUpperInvariant() ?? string.Empty;
        ledger.IsActive = dto.IsActive;
        ledger.UpdatedAt = DateTime.Now;

        _ledgerRepo.Update(ledger);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated ledger '{LedgerName}' (ID: {LedgerId})", ledger.LedgerName, ledger.LedgerId);

        return ledger;
    }

    public async Task<bool> DeleteLedgerAsync(int ledgerId, CancellationToken ct = default)
    {
        var ledger = await _ledgerRepo.GetByIdAsync(ledgerId, ct);
        if (ledger == null) return false;

        var hasVouchers = await _context.VoucherEntries
            .AnyAsync(ve => ve.LedgerId == ledgerId, ct);

        if (hasVouchers)
        {
            throw new InvalidOperationException(
                $"Cannot delete ledger '{ledger.LedgerName}' because accounting transactions have already been recorded against it.");
        }

        _ledgerRepo.Delete(ledger);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted ledger '{LedgerName}' (ID: {LedgerId})", ledger.LedgerName, ledger.LedgerId);
        return true;
    }

    public async Task<LedgerEntity?> GetLedgerByIdAsync(int ledgerId, CancellationToken ct = default)
    {
        return await _ledgerRepo.GetWithGroupAsync(ledgerId, ct);
    }

    public async Task<LedgerDetailDto?> GetLedgerDetailsAsync(int ledgerId, CancellationToken ct = default)
    {
        var ledger = await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId, ct);

        if (ledger == null) return null;

        return new LedgerDetailDto
        {
            LedgerId = ledger.LedgerId,
            CompanyId = ledger.CompanyId,
            GroupId = ledger.GroupId,
            GroupName = ledger.Group?.GroupName ?? string.Empty,
            GroupNature = ledger.Group?.Nature ?? GroupNature.Assets,
            LedgerName = ledger.LedgerName,
            OpeningBalance = ledger.OpeningBalance,
            OpeningBalanceType = ledger.OpeningBalanceType,
            Address = ledger.Address,
            Phone = ledger.Phone,
            Email = ledger.Email,
            PAN = ledger.PAN,
            State = ledger.State,
            CreditLimit = ledger.CreditLimit,
            CreditDays = ledger.CreditDays,
            BankName = ledger.BankName,
            BankAccountNumber = ledger.BankAccountNumber,
            IFSC = ledger.IFSC,
            IsActive = ledger.IsActive,
            CreatedAt = ledger.CreatedAt,
            UpdatedAt = ledger.UpdatedAt
        };
    }

    public async Task<IReadOnlyList<LedgerSummaryDto>> GetLedgersByCompanyAsync(
        int companyId,
        string? searchTerm = null,
        int? groupId = null,
        CancellationToken ct = default)
    {
        var query = _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId);

        if (groupId.HasValue)
        {
            query = query.Where(l => l.GroupId == groupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(l => l.LedgerName.ToLower().Contains(term) ||
                                     (l.Group != null && l.Group.GroupName.ToLower().Contains(term)));
        }

        var list = await query
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

        return list;
    }

    public async Task<bool> ValidateLedgerNameAsync(
        int companyId,
        string ledgerName,
        int? excludeLedgerId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ledgerName)) return false;
        return !await _ledgerRepo.LedgerNameExistsAsync(companyId, ledgerName.Trim(), excludeLedgerId, ct);
    }
}
