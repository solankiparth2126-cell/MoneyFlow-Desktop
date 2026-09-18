using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Constants;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using System.IO;
using System.Text.Json;
using MoneyFlow.Data.Storage;
using SecurityMode = MoneyFlow.Data.Encryption.SecurityMode;
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
    private readonly CompanySession? _companySession;
    private readonly SystemConfiguration? _systemConfig;
    private readonly StorageEngine? _storageEngine;
    private readonly CompanyManager? _companyManager;

    public CompanyService(
        ICompanyRepository companyRepo,
        IFinancialYearRepository financialYearRepo,
        IGroupRepository groupRepo,
        ILedgerRepository ledgerRepo,
        IUnitOfWork unitOfWork,
        ICompanyContext companyContext,
        ILogger<CompanyService> logger,
        CompanySession? companySession = null,
        SystemConfiguration? systemConfig = null,
        StorageEngine? storageEngine = null,
        CompanyManager? companyManager = null)
    {
        _companyRepo = companyRepo;
        _financialYearRepo = financialYearRepo;
        _groupRepo = groupRepo;
        _ledgerRepo = ledgerRepo;
        _unitOfWork = unitOfWork;
        _companyContext = companyContext;
        _logger = logger;
        _companySession = companySession;
        _systemConfig = systemConfig;
        _storageEngine = storageEngine;
        _companyManager = companyManager;
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

            string configuredBasePath = _systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath();
            string defaultCompaniesDir = Path.Combine(configuredBasePath, "Companies");

            string baseDir = !string.IsNullOrWhiteSpace(dto.DataDirectory)
                ? dto.DataDirectory.Trim()
                : defaultCompaniesDir;

            string targetCompanyDir = baseDir.EndsWith(assignedCompanyNumber, StringComparison.OrdinalIgnoreCase)
                ? baseDir
                : Path.Combine(baseDir, assignedCompanyNumber);

            string targetDataFile = Path.Combine(targetCompanyDir, "company.data");

            // Ensure isolated company folder on disk
            try
            {
                Directory.CreateDirectory(targetCompanyDir);
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Backups"));
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Exports"));
                Directory.CreateDirectory(Path.Combine(targetCompanyDir, "Reports"));

                if (_companyManager != null)
                {
                    try
                    {
                        _companyManager.RegisterCompany(new CompanyRegistration
                        {
                            CompanyId = assignedCompanyNumber,
                            DisplayName = trimmedName,
                            FolderName = assignedCompanyNumber,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    catch (Exception exReg)
                    {
                        _logger.LogWarning(exReg, "Failed registering company in manifest: {Msg}", exReg.Message);
                    }
                }
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
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? (_systemConfig?.CurrencySymbol ?? "₹") : dto.Currency.Trim(),
                CompanyNumber = assignedCompanyNumber,
                DataDirectory = targetCompanyDir,
                PasswordHash = pwdHash,
                PasswordSalt = pwdSalt,
                AutoBackupOnExit = dto.AutoBackupOnExit,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            if (_companySession != null)
            {
                _companySession.SetTargetFilePath(targetDataFile, assignedCompanyNumber);
            }

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

            // 5. Guarantee atomic company.data file write to target directory
            try
            {
                Directory.CreateDirectory(targetCompanyDir);

                if (_companySession != null)
                {
                    _companySession.SetTargetFilePath(targetDataFile, assignedCompanyNumber);
                    if (_companySession.DataStore != null)
                    {
                        _companySession.DataStore.CompanyInfo = company;
                        if (!_companySession.DataStore.FinancialYears.Any(f => f.FinancialYearId == financialYear.FinancialYearId))
                        {
                            _companySession.DataStore.FinancialYears.Add(financialYear);
                        }
                    }
                    _companySession.Save();
                }

                if (!File.Exists(targetDataFile))
                {
                    var store = _companySession?.DataStore ?? new CompanyDataStore();
                    store.CompanyInfo = company;
                    if (!store.FinancialYears.Any(f => f.FinancialYearId == financialYear.FinancialYearId))
                    {
                        store.FinancialYears.Add(financialYear);
                    }
                    var engine = _storageEngine ?? new StorageEngine();
                    engine.CreateCompanyFile(targetDataFile, store, assignedCompanyNumber, dto.VaultPassword ?? dto.Password);
                }
            }
            catch (Exception exSave)
            {
                _logger.LogWarning(exSave, "Explicit disk flush to {File}: {Msg}", targetDataFile, exSave.Message);
            }


            if (_systemConfig != null)
            {
                _systemConfig.LastCompanyId = company.CompanyNumber;
                _systemConfig.LastCompanyDisplayName = company.CompanyName;
                try { _systemConfig.SaveAtomic(_systemConfig.CompanyDataPath); } catch { }
            }

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
        var result = new List<CompanySummaryDto>();
        var seenCompanyNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Discover all companies from the configured Companies directory on disk
        string basePath = _systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath();
        string companiesDir = Path.Combine(basePath, "Companies");

        if (Directory.Exists(companiesDir))
        {
            foreach (var dir in Directory.GetDirectories(companiesDir))
            {
                var folderName = Path.GetFileName(dir);
                var dataFilePath = Path.Combine(dir, "company.data");

                if (!File.Exists(dataFilePath))
                {
                    string recoveredName = folderName;
                    if (_systemConfig?.LastCompanyId == folderName && !string.IsNullOrWhiteSpace(_systemConfig.LastCompanyDisplayName))
                    {
                        recoveredName = _systemConfig.LastCompanyDisplayName;
                    }
                    else
                    {
                        var reg = _companyManager?.GetRegistration(folderName);
                        if (reg != null && !string.IsNullOrWhiteSpace(reg.DisplayName))
                        {
                            recoveredName = reg.DisplayName;
                        }
                    }

                    if (recoveredName != folderName || Directory.GetDirectories(dir).Any(d => d.EndsWith("Backups") || d.EndsWith("Reports")))
                    {
                        try
                        {
                            var initialStore = new CompanyDataStore();
                            initialStore.CompanyInfo = new Core.Entities.Company
                            {
                                CompanyName = recoveredName,
                                CompanyNumber = folderName,
                                DataDirectory = dir,
                                FinancialYearFrom = new DateTime(DateTime.Today.Year, 4, 1),
                                BooksBeginningFrom = new DateTime(DateTime.Today.Year, 4, 1),
                                Currency = _systemConfig?.CurrencySymbol ?? "₹",
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            int fyStart = initialStore.CompanyInfo.FinancialYearFrom.Year;
                            initialStore.FinancialYears.Add(new Core.Entities.FinancialYear
                            {
                                FinancialYearId = 1,
                                YearName = $"{fyStart}-{(fyStart + 1) % 100:D2}",
                                StartDate = initialStore.CompanyInfo.FinancialYearFrom,
                                EndDate = initialStore.CompanyInfo.FinancialYearFrom.AddYears(1).AddDays(-1),
                                IsClosed = false
                            });
                            var engine = _storageEngine ?? new StorageEngine();
                            engine.CreateCompanyFile(dataFilePath, initialStore, folderName, null);
                        }
                        catch { }
                    }

                    if (!File.Exists(dataFilePath))
                        continue;
                }


                string companyName = folderName;
                string companyNumber = folderName;
                DateTime fyFrom = new DateTime(DateTime.Today.Year, 4, 1);
                string currency = _systemConfig?.CurrencySymbol ?? "₹";
                bool isPassword = false;

                if (_storageEngine != null)
                {
                    try
                    {
                        var (fileHeader, secHeader) = _storageEngine.ReadHeaders(dataFilePath);
                        if (!string.IsNullOrWhiteSpace(fileHeader.CompanyId))
                        {
                            companyNumber = fileHeader.CompanyId;
                        }

                        isPassword = secHeader.SecurityMode == SecurityMode.PasswordProtected;

                        // Check manifest registration first
                        var reg = _companyManager?.GetRegistration(fileHeader.CompanyId);
                        if (reg != null && !string.IsNullOrWhiteSpace(reg.DisplayName))
                        {
                            companyName = reg.DisplayName;
                        }

                        // If not password-protected, load data payload directly from company.data
                        if (!isPassword)
                        {
                            try
                            {
                                var (store, _, _) = _storageEngine.LoadCompany(dataFilePath, null);
                                if (!string.IsNullOrWhiteSpace(store.CompanyInfo?.CompanyName))
                                {
                                    companyName = store.CompanyInfo.CompanyName;
                                }
                                if (store.CompanyInfo?.FinancialYearFrom != default && store.CompanyInfo?.FinancialYearFrom.Year > 1900)
                                {
                                    fyFrom = store.CompanyInfo.FinancialYearFrom;
                                }
                                if (!string.IsNullOrWhiteSpace(store.CompanyInfo?.Currency))
                                {
                                    currency = store.CompanyInfo.Currency;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                if (companyName == folderName || string.IsNullOrWhiteSpace(companyName))
                {
                    if (_systemConfig?.LastCompanyId == folderName && !string.IsNullOrWhiteSpace(_systemConfig.LastCompanyDisplayName))
                    {
                        companyName = _systemConfig.LastCompanyDisplayName;
                    }
                    else
                    {
                        try
                        {
                            var diskCfg = SystemConfiguration.Load(basePath);
                            if (diskCfg?.LastCompanyId == folderName && !string.IsNullOrWhiteSpace(diskCfg.LastCompanyDisplayName))
                            {
                                companyName = diskCfg.LastCompanyDisplayName;
                            }
                        }
                        catch { }
                    }
                }

                int compId = 0;
                if (int.TryParse(companyNumber, out int parsedNum)) compId = parsedNum;
                else if (int.TryParse(folderName, out int parsedFolder)) compId = parsedFolder;

                if (string.IsNullOrWhiteSpace(companyName) || companyName == "010000")
                    continue;


                seenCompanyNumbers.Add(companyNumber);
                result.Add(new CompanySummaryDto
                {
                    CompanyId = compId,
                    CompanyName = companyName,
                    CompanyNumber = companyNumber,
                    FinancialYearFrom = fyFrom,
                    BooksBeginningFrom = fyFrom,
                    Currency = currency,
                    DataDirectory = dir,
                    IsPasswordProtected = isPassword,
                    IsActive = true
                });
            }
        }

        // 2. Also query active in-memory repository (filter out uninitialized ghost entries)
        try
        {
            var repoCompanies = await _companyRepo.GetAllAsync(ct);
            if (repoCompanies != null)
            {
                foreach (var c in repoCompanies)
                {
                    if (c.CompanyId <= 0 || string.IsNullOrWhiteSpace(c.CompanyName) || c.CompanyName == "010000")
                        continue;

                    string compNum = string.IsNullOrWhiteSpace(c.CompanyNumber) ? $"01{c.CompanyId:D4}" : c.CompanyNumber;
                    if (!seenCompanyNumbers.Contains(compNum))
                    {
                        seenCompanyNumbers.Add(compNum);
                        result.Add(new CompanySummaryDto
                        {
                            CompanyId = c.CompanyId,
                            CompanyName = c.CompanyName,
                            State = c.State,
                            FinancialYearFrom = c.FinancialYearFrom,
                            BooksBeginningFrom = c.BooksBeginningFrom,
                            Currency = c.Currency,
                            CompanyNumber = compNum,
                            DataDirectory = c.DataDirectory ?? string.Empty,
                            IsPasswordProtected = c.IsPasswordProtected,
                            AutoBackupOnExit = c.AutoBackupOnExit,
                            IsActive = c.IsActive
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Unable to load companies from repository.");
        }

        return result.OrderBy(c => c.CompanyName).ToList();
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
        // Try finding from active session first
        var company = await _companyRepo.GetByIdAsync(companyId, ct);

        // If not in active session or empty, find from disk
        if (company == null || company.CompanyId <= 0 || string.IsNullOrWhiteSpace(company.CompanyName))
        {
            var all = await GetAllCompaniesAsync(ct);
            var match = all.FirstOrDefault(c => c.CompanyId == companyId);
            if (match != null)
            {
                string dataFile = Path.Combine(match.DataDirectory, "company.data");
                if (File.Exists(dataFile) && _companySession != null)
                {
                    try
                    {
                        _companySession.Open(dataFile, null);
                        company = _companySession.DataStore?.CompanyInfo;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed opening company file at {Path}", dataFile);
                    }
                }

                if (company == null)
                {
                    company = new Core.Entities.Company
                    {
                        CompanyId = match.CompanyId,
                        CompanyName = match.CompanyName,
                        CompanyNumber = match.CompanyNumber,
                        DataDirectory = match.DataDirectory,
                        Currency = match.Currency,
                        FinancialYearFrom = match.FinancialYearFrom,
                        BooksBeginningFrom = match.BooksBeginningFrom,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                }

                if (!File.Exists(dataFile) && _storageEngine != null && _companySession != null)
                {
                    try
                    {
                        var initialStore = new CompanyDataStore();
                        initialStore.CompanyInfo = company;
                        _storageEngine.CreateCompanyFile(dataFile, initialStore, match.CompanyNumber, null);
                        _companySession.Open(dataFile, null);
                    }
                    catch (Exception exInit)
                    {
                        _logger?.LogWarning(exInit, "Failed creating initial company file at {Path}", dataFile);
                    }
                }
            }
        }

        if (company == null || !company.IsActive)
        {
            return false;
        }

        var currentFY = await _financialYearRepo.GetCurrentFYAsync(company.CompanyId, DateTime.Today, ct)
            ?? (await _financialYearRepo.GetByCompanyIdAsync(company.CompanyId, ct)).FirstOrDefault();

        if (currentFY == null)
        {
            currentFY = new Core.Entities.FinancialYear
            {
                FinancialYearId = 1,
                CompanyId = company.CompanyId,
                YearName = $"{company.FinancialYearFrom:yyyy}-{(company.FinancialYearFrom.AddYears(1).Year % 100):D2}",
                StartDate = company.FinancialYearFrom,
                EndDate = company.FinancialYearFrom.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        _companyContext.SetActiveCompany(company, currentFY);

        if (_systemConfig != null)
        {
            _systemConfig.LastCompanyId = company.CompanyNumber;
            _systemConfig.LastCompanyDisplayName = company.CompanyName;
            try { _systemConfig.SaveAtomic(_systemConfig.CompanyDataPath); } catch { }
        }

        return true;
    }

    public void CloseCompany()
    {
        _companyContext.CloseCompany();
    }

    private async Task<Dictionary<string, MoneyFlow.Core.Entities.Group>> SeedDefaultGroupsAsync(int companyId, CancellationToken ct)
    {
        var groupsMap = new Dictionary<string, MoneyFlow.Core.Entities.Group>(StringComparer.OrdinalIgnoreCase);

        // 1. Seed 15 Primary Groups
        foreach (var pgDef in PredefinedAccountingGroups.PrimaryGroups)
        {
            var pg = new MoneyFlow.Core.Entities.Group
            {
                CompanyId = companyId,
                GroupName = pgDef.Name,
                Nature = pgDef.Nature,
                PrimaryGroup = true,
                ParentGroupId = null,
                AffectProfitLoss = pgDef.AffectProfitLoss,
                IsPredefined = true,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _groupRepo.AddAsync(pg, ct);
            groupsMap[pg.GroupName] = pg;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // 2. Seed 13 Sub-Groups
        foreach (var sgDef in PredefinedAccountingGroups.SubGroups)
        {
            if (string.IsNullOrEmpty(sgDef.ParentGroupName) || !groupsMap.TryGetValue(sgDef.ParentGroupName, out var parentGroup))
            {
                _logger.LogWarning("Parent group '{ParentGroupName}' not found for sub-group '{SubGroupName}'.", sgDef.ParentGroupName, sgDef.Name);
                continue;
            }

            var sg = new MoneyFlow.Core.Entities.Group
            {
                CompanyId = companyId,
                GroupName = sgDef.Name,
                Nature = sgDef.Nature,
                PrimaryGroup = false,
                ParentGroupId = parentGroup.GroupId,
                AffectProfitLoss = sgDef.AffectProfitLoss,
                IsPredefined = true,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            await _groupRepo.AddAsync(sg, ct);
            groupsMap[sg.GroupName] = sg;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded 28 predefined accounting groups (15 Primary + 13 Sub-Groups) for Company ID {CompanyId}.", companyId);
        return groupsMap;
    }

    private async Task SeedDefaultLedgersAsync(int companyId, Dictionary<string, MoneyFlow.Core.Entities.Group> groupsMap, CancellationToken ct)
    {
        // Real predefined / reserved accounting ledgers only (DO NOT create Groups as Ledgers!)
        var defaultLedgers = new List<(string Name, string GroupName)>
        {
            ("Cash", "Cash-in-hand"),
            ("Profit & Loss A/c", "Capital Account"),
            ("Sales", "Sales Accounts"),
            ("Purchase", "Purchase Accounts")
        };

        foreach (var (name, groupName) in defaultLedgers)
        {
            // Support lookup with fallback for casing / variations
            MoneyFlow.Core.Entities.Group? group = null;
            if (!groupsMap.TryGetValue(groupName, out group))
            {
                if (groupName.Equals("Cash-in-hand", StringComparison.OrdinalIgnoreCase))
                {
                    groupsMap.TryGetValue("Cash-in-Hand", out group);
                }
            }

            if (group != null)
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
            else
            {
                _logger.LogWarning("Cannot seed default ledger '{LedgerName}' because group '{GroupName}' was not found.", name, groupName);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded canonical default ledgers (Cash, Profit & Loss A/c, Sales, Purchase) for Company ID {CompanyId}.", companyId);
    }
}
