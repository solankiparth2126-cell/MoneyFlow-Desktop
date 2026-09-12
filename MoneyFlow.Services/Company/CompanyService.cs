using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using Group = MoneyFlow.Core.Entities.Group;
using FinancialYear = MoneyFlow.Core.Entities.FinancialYear;
using Ledger = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Services.Company;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepo;
    private readonly IFinancialYearRepository _financialYearRepo;
    private readonly IGroupRepository _groupRepo;
    private readonly ILedgerRepository _ledgerRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyContext _companyContext;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(
        ICompanyRepository companyRepo,
        IFinancialYearRepository financialYearRepo,
        IGroupRepository groupRepo,
        ILedgerRepository ledgerRepo,
        IUnitOfWork unitOfWork,
        ICompanyContext companyContext,
        ILogger<CompanyService> logger)
    {
        _companyRepo = companyRepo;
        _financialYearRepo = financialYearRepo;
        _groupRepo = groupRepo;
        _ledgerRepo = ledgerRepo;
        _unitOfWork = unitOfWork;
        _companyContext = companyContext;
        _logger = logger;
    }

    public async Task<Core.Entities.Company> CreateCompanyAsync(CompanyCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            throw new ArgumentException("Company name is required.", nameof(dto.CompanyName));
        }

        string trimmedName = dto.CompanyName.Trim();
        bool nameExists = await _companyRepo.AnyAsync(c => c.CompanyName.ToLower() == trimmedName.ToLower(), ct);
        if (nameExists)
        {
            throw new InvalidOperationException($"A company with the name '{trimmedName}' already exists.");
        }

        using var tx = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Determine company number & isolated data directory
            var existing = await _companyRepo.GetAllAsync(ct);
            int maxNum = 10000;
            foreach (var c in existing)
            {
                if (int.TryParse(c.CompanyNumber, out int n) && n > maxNum)
                {
                    maxNum = n;
                }
            }
            string assignedCompanyNumber = string.IsNullOrWhiteSpace(dto.CompanyNumber)
                ? (maxNum + 1).ToString("D6")
                : dto.CompanyNumber.Trim();

            string baseDir = string.IsNullOrWhiteSpace(dto.DataDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MoneyFlow", "Data")
                : dto.DataDirectory.Trim();

            string targetCompanyDir = baseDir.EndsWith(assignedCompanyNumber, StringComparison.OrdinalIgnoreCase)
                ? baseDir
                : Path.Combine(baseDir, assignedCompanyNumber);

            // Ensure isolated company folder on disk
            try
            {
                Directory.CreateDirectory(targetCompanyDir);
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Backups"));
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Exports"));
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Reports"));

                var meta = new
                {
                    CompanyNumber = assignedCompanyNumber,
                    CompanyName = trimmedName,
                    CreatedAt = DateTime.Now,
                    FinancialYearFrom = dto.FinancialYearFrom,
                    Currency = dto.Currency,
                    DataDirectory = targetCompanyDir
                };
                string metaJson = System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(targetCompanyDir, "company.json"), metaJson);
            }
            catch (Exception exDir)
            {
                _logger.LogWarning(exDir, "Could not initialize directory {TargetDir}: {Message}", targetCompanyDir, exDir.Message);
            }

            string? pwdHash = null;
            string? pwdSalt = null;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                (pwdHash, pwdSalt) = HashPassword(dto.Password.Trim());
            }

            // 1. Create Company Entity
            var company = new Core.Entities.Company
            {
                CompanyName = trimmedName,
                Address = dto.Address?.Trim() ?? string.Empty,
                State = dto.State?.Trim() ?? string.Empty,
                Country = string.IsNullOrWhiteSpace(dto.Country) ? "India" : dto.Country.Trim(),
                PAN = dto.PAN?.Trim().ToUpperInvariant() ?? string.Empty,
                Email = dto.Email?.Trim() ?? string.Empty,
                Phone = dto.Phone?.Trim() ?? string.Empty,
                FinancialYearFrom = dto.FinancialYearFrom.Date,
                BooksBeginningFrom = dto.BooksBeginningFrom.Date,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "₹" : dto.Currency.Trim(),
                CompanyNumber = assignedCompanyNumber,
                DataDirectory = targetCompanyDir,
                PasswordHash = pwdHash,
                PasswordSalt = pwdSalt,
                AutoBackupOnExit = dto.AutoBackupOnExit,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            await _companyRepo.AddAsync(company, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 2. Create Initial Financial Year
            int startYear = company.FinancialYearFrom.Year;
            int endYear = startYear + 1;
            string fyName = $"{startYear}-{(endYear % 100):D2}";

            var financialYear = new Core.Entities.FinancialYear
            {
                CompanyId = company.CompanyId,
                YearName = fyName,
                StartDate = company.FinancialYearFrom,
                EndDate = company.FinancialYearFrom.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.Now
            };

            await _financialYearRepo.AddAsync(financialYear, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // 3. Seed Default Chart of Accounts Groups (Section 13)
            var groupsMap = await SeedDefaultGroupsAsync(company.CompanyId, ct);

            // 4. Optionally Seed Default Ledgers (Section 15)
            if (dto.CreateDefaultLedgers)
            {
                await SeedDefaultLedgersAsync(company.CompanyId, groupsMap, ct);
            }

            await tx.CommitAsync(ct);

            _logger.LogInformation("Company {CompanyName} (No: {CompanyNumber}, ID: {CompanyId}) created successfully at {DataDir}.",
                company.CompanyName, company.CompanyNumber, company.CompanyId, company.DataDirectory);

            // Set as active company in context
            _companyContext.SetActiveCompany(company, financialYear);

            return company;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to create company: {CompanyName}", dto.CompanyName);
            throw;
        }
    }

    public async Task<Core.Entities.Company> UpdateCompanyAsync(CompanyUpdateDto dto, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(dto.CompanyId, ct)
            ?? throw new KeyNotFoundException($"Company with ID {dto.CompanyId} not found.");

        string trimmedName = dto.CompanyName.Trim();
        bool nameExists = await _companyRepo.AnyAsync(c => c.CompanyName.ToLower() == trimmedName.ToLower() && c.CompanyId != dto.CompanyId, ct);
        if (nameExists)
        {
            throw new InvalidOperationException($"Another company with the name '{trimmedName}' already exists.");
        }

        company.CompanyName = trimmedName;
        company.Address = dto.Address?.Trim() ?? string.Empty;
        company.State = dto.State?.Trim() ?? string.Empty;
        company.Country = string.IsNullOrWhiteSpace(dto.Country) ? "India" : dto.Country.Trim();
        company.PAN = dto.PAN?.Trim().ToUpperInvariant() ?? string.Empty;
        company.Email = dto.Email?.Trim() ?? string.Empty;
        company.Phone = dto.Phone?.Trim() ?? string.Empty;
        company.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "₹" : dto.Currency.Trim();
        company.IsActive = dto.IsActive;
        if (!string.IsNullOrWhiteSpace(dto.CompanyNumber))
        {
            company.CompanyNumber = dto.CompanyNumber.Trim();
        }
        if (!string.IsNullOrWhiteSpace(dto.DataDirectory))
        {
            company.DataDirectory = dto.DataDirectory.Trim();
            try { Directory.CreateDirectory(company.DataDirectory); } catch { }
        }
        if (dto.RemovePassword)
        {
            company.PasswordHash = null;
            company.PasswordSalt = null;
        }
        else if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            var (h, s) = HashPassword(dto.NewPassword.Trim());
            company.PasswordHash = h;
            company.PasswordSalt = s;
        }
        company.AutoBackupOnExit = dto.AutoBackupOnExit;
        company.UpdatedAt = DateTime.Now;

        _companyRepo.Update(company);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_companyContext.CurrentCompany?.CompanyId == company.CompanyId)
        {
            _companyContext.SetActiveCompany(company, _companyContext.CurrentFinancialYear);
        }

        return company;
    }

    public async Task<Core.Entities.Company?> GetCompanyByIdAsync(int companyId, CancellationToken ct = default)
    {
        return await _companyRepo.GetCompanyWithDetailsAsync(companyId, ct);
    }

    public async Task<IReadOnlyList<CompanySummaryDto>> GetAllCompaniesAsync(CancellationToken ct = default)
    {
        var companies = await _companyRepo.GetAllAsync(ct);
        return companies
            .OrderBy(c => c.CompanyName)
            .Select(c => new CompanySummaryDto
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                State = c.State,
                FinancialYearFrom = c.FinancialYearFrom,
                BooksBeginningFrom = c.BooksBeginningFrom,
                Currency = c.Currency,
                CompanyNumber = string.IsNullOrWhiteSpace(c.CompanyNumber) ? $"01{c.CompanyId:D4}" : c.CompanyNumber,
                DataDirectory = c.DataDirectory ?? string.Empty,
                IsPasswordProtected = c.IsPasswordProtected,
                AutoBackupOnExit = c.AutoBackupOnExit,
                IsActive = c.IsActive
            })
            .ToList();
    }

    public async Task<bool> VerifyCompanyPasswordAsync(int companyId, string password, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct);
        if (company == null) return false;
        if (!company.IsPasswordProtected) return true;
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(company.PasswordSalt) || string.IsNullOrEmpty(company.PasswordHash)) return false;
        return VerifyHash(password, company.PasswordHash, company.PasswordSalt);
    }

    private static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        string salt = Convert.ToBase64String(saltBytes);
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, saltBytes, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hashBytes = pbkdf2.GetBytes(32);
        string hash = Convert.ToBase64String(hashBytes);
        return (hash, salt);
    }

    private static bool VerifyHash(string password, string storedHash, string storedSalt)
    {
        byte[] saltBytes = Convert.FromBase64String(storedSalt);
        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(password, saltBytes, 10000, System.Security.Cryptography.HashAlgorithmName.SHA256);
        byte[] hashBytes = pbkdf2.GetBytes(32);
        string computedHash = Convert.ToBase64String(hashBytes);
        return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
    }

    public async Task<bool> DeleteCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct);
        if (company == null) return false;

        // Soft delete
        company.IsActive = false;
        company.UpdatedAt = DateTime.Now;
        _companyRepo.Update(company);
        await _unitOfWork.SaveChangesAsync(ct);

        if (_companyContext.CurrentCompany?.CompanyId == companyId)
        {
            _companyContext.CloseCompany();
        }

        return true;
    }

    public async Task<bool> OpenCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct);
        if (company == null || !company.IsActive)
        {
            return false;
        }

        var currentFY = await _financialYearRepo.GetCurrentFYAsync(companyId, DateTime.Today, ct)
            ?? (await _financialYearRepo.GetByCompanyIdAsync(companyId, ct)).FirstOrDefault();

        _companyContext.SetActiveCompany(company, currentFY);
        return true;
    }

    public void CloseCompany()
    {
        _companyContext.CloseCompany();
    }

    private async Task<Dictionary<string, MoneyFlow.Core.Entities.Group>> SeedDefaultGroupsAsync(int companyId, CancellationToken ct)
    {
        var groupsMap = new Dictionary<string, MoneyFlow.Core.Entities.Group>(StringComparer.OrdinalIgnoreCase);

        // Primary Groups (Section 13)
        var primaryGroups = new List<MoneyFlow.Core.Entities.Group>
        {
            new() { CompanyId = companyId, GroupName = "Capital Account", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Current Assets", Nature = GroupNature.Assets, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Current Liabilities", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Fixed Assets", Nature = GroupNature.Assets, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Investments", Nature = GroupNature.Assets, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Loans", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Direct Expenses", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Indirect Expenses", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Direct Income", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Indirect Income", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Sales Accounts", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Purchase Accounts", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true },
            new() { CompanyId = companyId, GroupName = "Duties & Taxes", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false }
        };

        foreach (var pg in primaryGroups)
        {
            await _groupRepo.AddAsync(pg, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var pg in primaryGroups)
        {
            groupsMap[pg.GroupName] = pg;
        }

        // Subgroups (Section 13)
        var currentAssets = groupsMap["Current Assets"];
        var currentLiabilities = groupsMap["Current Liabilities"];

        var subGroups = new List<MoneyFlow.Core.Entities.Group>
        {
            new() { CompanyId = companyId, GroupName = "Bank Accounts", ParentGroupId = currentAssets.GroupId, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Cash-in-Hand", ParentGroupId = currentAssets.GroupId, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Sundry Debtors", ParentGroupId = currentAssets.GroupId, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false },
            new() { CompanyId = companyId, GroupName = "Sundry Creditors", ParentGroupId = currentLiabilities.GroupId, Nature = GroupNature.Liabilities, PrimaryGroup = false, AffectProfitLoss = false }
        };

        foreach (var sg in subGroups)
        {
            await _groupRepo.AddAsync(sg, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        foreach (var sg in subGroups)
        {
            groupsMap[sg.GroupName] = sg;
        }

        return groupsMap;
    }

    private async Task SeedDefaultLedgersAsync(int companyId, Dictionary<string, MoneyFlow.Core.Entities.Group> groupsMap, CancellationToken ct)
    {
        // Default Ledgers (Section 15)
        var defaultLedgers = new List<(string Name, string GroupName)>
        {
            ("Cash", "Cash-in-Hand"),
            ("Profit & Loss A/c", "Capital Account"),
            ("Capital Account", "Capital Account"),
            ("Sales", "Sales Accounts"),
            ("Purchase", "Purchase Accounts"),
            ("Sundry Debtors", "Sundry Debtors"),
            ("Sundry Creditors", "Sundry Creditors"),
            ("Direct Expenses", "Direct Expenses"),
            ("Indirect Expenses", "Indirect Expenses"),
            ("Direct Income", "Direct Income"),
            ("Indirect Income", "Indirect Income")
        };

        foreach (var (name, groupName) in defaultLedgers)
        {
            if (groupsMap.TryGetValue(groupName, out var group))
            {
                var ledger = new MoneyFlow.Core.Entities.Ledger
                {
                    CompanyId = companyId,
                    GroupId = group.GroupId,
                    LedgerName = name,
                    OpeningBalance = 0m,
                    OpeningBalanceType = BalanceType.Debit,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                await _ledgerRepo.AddAsync(ledger, ct);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
