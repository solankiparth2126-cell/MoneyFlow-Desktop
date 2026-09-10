using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.FinancialYear;

public class FinancialYearService : IFinancialYearService
{
    private readonly IFinancialYearRepository _fyRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyContext _companyContext;
    private readonly ILogger<FinancialYearService> _logger;

    public FinancialYearService(
        IFinancialYearRepository fyRepo,
        ICompanyRepository companyRepo,
        IUnitOfWork unitOfWork,
        ICompanyContext companyContext,
        ILogger<FinancialYearService> logger)
    {
        _fyRepo = fyRepo;
        _companyRepo = companyRepo;
        _unitOfWork = unitOfWork;
        _companyContext = companyContext;
        _logger = logger;
    }

    public async Task<Core.Entities.FinancialYear> CreateFinancialYearAsync(int companyId, FinancialYearCreateDto dto, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct)
            ?? throw new KeyNotFoundException($"Company with ID {companyId} not found.");

        if (dto.StartDate >= dto.EndDate)
        {
            throw new ArgumentException("Financial Year Start Date must be earlier than End Date.", nameof(dto.StartDate));
        }

        // Validate date overlap with existing financial years for this company
        var existingYears = await _fyRepo.GetByCompanyIdAsync(companyId, ct);
        bool hasOverlap = existingYears.Any(f => f.StartDate <= dto.EndDate && f.EndDate >= dto.StartDate);
        if (hasOverlap)
        {
            throw new InvalidOperationException($"The date range {dto.StartDate:dd-MMM-yyyy} to {dto.EndDate:dd-MMM-yyyy} overlaps with an existing Financial Year.");
        }

        string yearName = string.IsNullOrWhiteSpace(dto.YearName)
            ? $"{dto.StartDate.Year}-{(dto.EndDate.Year % 100):D2}"
            : dto.YearName.Trim();

        var fy = new Core.Entities.FinancialYear
        {
            CompanyId = companyId,
            YearName = yearName,
            StartDate = dto.StartDate.Date,
            EndDate = dto.EndDate.Date,
            IsClosed = false,
            CreatedAt = DateTime.Now
        };

        await _fyRepo.AddAsync(fy, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Financial Year {YearName} created for Company ID {CompanyId}.", yearName, companyId);

        return fy;
    }

    public async Task<IReadOnlyList<FinancialYearSummaryDto>> GetFinancialYearsByCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var years = await _fyRepo.GetByCompanyIdAsync(companyId, ct);
        int? currentActiveFYId = _companyContext.CurrentFinancialYear?.FinancialYearId;

        return years.Select(f => new FinancialYearSummaryDto
        {
            FinancialYearId = f.FinancialYearId,
            CompanyId = f.CompanyId,
            YearName = f.YearName,
            StartDate = f.StartDate,
            EndDate = f.EndDate,
            IsClosed = f.IsClosed,
            IsActive = f.FinancialYearId == currentActiveFYId
        }).ToList();
    }

    public async Task<bool> SetActiveFinancialYearAsync(int financialYearId, CancellationToken ct = default)
    {
        var fy = await _fyRepo.GetByIdAsync(financialYearId, ct);
        if (fy == null) return false;

        if (fy.IsClosed)
        {
            throw new InvalidOperationException($"Financial Year '{fy.YearName}' is closed and cannot be set as active.");
        }

        _companyContext.SetActiveFinancialYear(fy);
        _logger.LogInformation("Active Financial Year switched to {YearName}.", fy.YearName);
        return true;
    }

    public bool ValidateDateInCurrentFY(DateTime transactionDate, out string errorMessage)
    {
        var currentFY = _companyContext.CurrentFinancialYear;
        if (currentFY == null)
        {
            errorMessage = "No active Financial Year is currently selected.";
            return false;
        }

        var date = transactionDate.Date;
        if (date < currentFY.StartDate.Date || date > currentFY.EndDate.Date)
        {
            errorMessage = $"Transaction date ({date:dd-MMM-yyyy}) is outside the active Financial Year ({currentFY.YearName}: {currentFY.StartDate:dd-MMM-yyyy} to {currentFY.EndDate:dd-MMM-yyyy}). Transactions outside the active financial year are not permitted.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public async Task<bool> CloseFinancialYearAsync(int financialYearId, CancellationToken ct = default)
    {
        var fy = await _fyRepo.GetByIdAsync(financialYearId, ct);
        if (fy == null) return false;

        fy.IsClosed = true;
        _fyRepo.Update(fy);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_companyContext.CurrentFinancialYear?.FinancialYearId == financialYearId)
        {
            // Switch to another open FY if available
            var otherFY = (await _fyRepo.GetByCompanyIdAsync(fy.CompanyId, ct)).FirstOrDefault(f => !f.IsClosed && f.FinancialYearId != financialYearId);
            if (otherFY != null)
            {
                _companyContext.SetActiveFinancialYear(otherFY);
            }
        }

        _logger.LogInformation("Financial Year {YearName} has been closed.", fy.YearName);
        return true;
    }

    public Core.Entities.FinancialYear? GetCurrentFinancialYear()
    {
        return _companyContext.CurrentFinancialYear;
    }
}
