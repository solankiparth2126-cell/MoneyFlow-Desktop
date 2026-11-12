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
using MoneyFlow.Core.Exceptions;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Core.Migration;
using MoneyFlow.Core.Migration.Validators;
using MoneyFlow.Data.Repositories;
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Services.Company;

public class CompanyManager : ICompanyManager
{
    private readonly ICompanyStorageEngine _storageEngine;
    private readonly ICompanyDataStore _dataStore;
    private readonly ICompanyContext _companyContext;
    private readonly IMigrationManager _migrationManager;
    private readonly ITransactionManager _transactionManager;
    private readonly ILogger<CompanyManager> _logger;

    private IDisposable? _activeCompanyLock;

    public CompanyManager(
        ICompanyStorageEngine storageEngine,
        ICompanyDataStore dataStore,
        ICompanyContext companyContext,
        IMigrationManager migrationManager,
        ITransactionManager transactionManager,
        ILogger<CompanyManager> logger)
    {
        _storageEngine = storageEngine;
        _dataStore = dataStore;
        _companyContext = companyContext;
        _migrationManager = migrationManager;
        _transactionManager = transactionManager;
        _logger = logger;
    }

    public async Task<CompanySummaryDto> CreateCompanyAsync(CompanyCreateDto dto, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            throw new ArgumentException("Company name is required.", nameof(dto.CompanyName));
        }

        string trimmedName = dto.CompanyName.Trim();
        string pwd = string.IsNullOrWhiteSpace(password) ? "MyERP_Default_Secret_2026" : password;

        // Determine immutable Company ID / Entity Code
        var existing = await GetCompaniesAsync(ct);
        int maxId = 0;
        foreach (var c in existing)
        {
            if (c.CompanyId > maxId) maxId = c.CompanyId;
            if (c.CompanyNumber.StartsWith("COMP-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(c.CompanyNumber.Substring(5), out int num) && num > maxId)
            {
                maxId = num;
            }
            else if (int.TryParse(c.CompanyNumber.Replace("(", "").Replace(")", "").Trim(), out int n) && n > maxId)
            {
                maxId = n;
            }
        }

        string companyId = string.IsNullOrWhiteSpace(dto.CompanyNumber)
            ? $"{(maxId + 1):D6}"
            : dto.CompanyNumber.Replace("(", "").Replace(")", "").Trim();

        int finalCompanyId = int.TryParse(companyId, out int parsedId) ? parsedId : (maxId + 1);

        string companyDir = _storageEngine.GetCompanyDirectory(companyId);
        string filePath = _storageEngine.GetCompanyFilePath(companyId);

        if (File.Exists(filePath))
        {
            throw new CompanyAlreadyExistsException(companyId);
        }

        Directory.CreateDirectory(companyDir);

        try
        {
            // 1. Build Initial Company Data
            var companyData = new CompanyData();
            companyData.Company = new Core.Entities.Company
            {
                CompanyId = finalCompanyId,
                CompanyNumber = companyId,
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
                PasswordHash = string.IsNullOrWhiteSpace(password) ? null : "PROTECTED",
                AutoBackupOnExit = dto.AutoBackupOnExit,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // 2. Initial Financial Year
            int startYear = companyData.Company.FinancialYearFrom.Year;
            int endYear = startYear + 1;
            var fy = new FinancialYearEntity
            {
                FinancialYearId = 1,
                CompanyId = companyData.Company.CompanyId,
                YearName = $"{startYear}-{(endYear % 100):D2}",
                StartDate = companyData.Company.FinancialYearFrom,
                EndDate = companyData.Company.FinancialYearFrom.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };
            companyData.FinancialYears.Add(fy);

            // 3. Seed Default Chart of Accounts Groups
            SeedDefaultGroups(companyData);

            // 4. Seed Standard Voucher Types
            SeedDefaultVoucherTypes(companyData);

            // 5. Seed Default Ledgers if requested
            if (dto.CreateDefaultLedgers)
            {
                SeedDefaultLedgers(companyData);
            }

            // 6. Write atomically to encrypted company.data
            await _storageEngine.CreateInitialCompanyFileAsync(filePath, companyData, pwd, ct);
            _logger.LogInformation("Company {CompanyId} ({CompanyName}) created at {FilePath}", companyId, trimmedName, filePath);

            return new CompanySummaryDto
            {
                CompanyId = companyData.Company.CompanyId,
                CompanyNumber = companyId,
                CompanyName = trimmedName,
                FinancialYearFrom = companyData.Company.FinancialYearFrom,
                BooksBeginningFrom = companyData.Company.BooksBeginningFrom,
                Currency = companyData.Company.Currency,
                IsActive = true,
                IsPasswordProtected = !string.IsNullOrWhiteSpace(password)
            };
        }
        catch
        {
            // Transactional rollback on error
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                if (Directory.Exists(companyDir) && !Directory.EnumerateFileSystemEntries(companyDir).Any())
                {
                    Directory.Delete(companyDir);
                }
            }
            catch { }
            throw;
        }
    }

    public async Task<bool> OpenCompanyAsync(string companyId, string password, CancellationToken ct = default)
    {
        string filePath = _storageEngine.GetCompanyFilePath(companyId);
        if (!File.Exists(filePath))
        {
            throw new CompanyNotFoundException(companyId);
        }

        // Close any currently opened company first
        CloseCompany();

        // 1. Acquire file lock
        _activeCompanyLock = _storageEngine.AcquireLock(companyId);

        try
        {
            string pwd = string.IsNullOrWhiteSpace(password) ? "MyERP_Default_Secret_2026" : password;

            // 2. Check and run migration if needed
            if (await _migrationManager.NeedsMigrationAsync(filePath, ct))
            {
                await _migrationManager.MigrateCompanyFileAsync(filePath, pwd, null, ct);
            }

            // 3. Load and decrypt company data
            var (data, dek) = await _storageEngine.LoadCompanyDataAsync(filePath, pwd, ct);

            // 4. Check/recover pending WAL
            await _transactionManager.RecoverPendingJournalAsync(companyId, data, ct);

            // 5. Validate integrity
            CompanyDataValidator.Validate(data);

            // 6. Establish active context
            _dataStore.SetActiveCompanyData(filePath, data, dek);

            var activeFy = data.FinancialYears.FirstOrDefault(f => !f.IsClosed)
                        ?? data.FinancialYears.OrderByDescending(f => f.StartDate).FirstOrDefault();

            _companyContext.SetActiveCompany(data.Company, activeFy);

            _logger.LogInformation("Company {CompanyId} ({CompanyName}) opened successfully.", companyId, data.Company.CompanyName);
            return true;
        }
        catch
        {
            _activeCompanyLock?.Dispose();
            _activeCompanyLock = null;
            _dataStore.Clear();
            _companyContext.CloseCompany();
            throw;
        }
    }

    public void CloseCompany()
    {
        if (_dataStore.IsLoaded)
        {
            try
            {
                _dataStore.SaveChangesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while saving changes on company close.");
            }
        }

        _dataStore.Clear();
        _companyContext.CloseCompany();

        _activeCompanyLock?.Dispose();
        _activeCompanyLock = null;

        _logger.LogInformation("Company closed and file locks released.");
    }

    public async Task<bool> DeleteCompanyAsync(string companyId, bool createSafetyBackup = true, CancellationToken ct = default)
    {
        if (_companyContext.CurrentCompany?.CompanyNumber == companyId)
        {
            CloseCompany();
        }

        string companyDir = _storageEngine.GetCompanyDirectory(companyId);
        if (!Directory.Exists(companyDir))
        {
            return false;
        }

        if (createSafetyBackup && File.Exists(_storageEngine.GetCompanyFilePath(companyId)))
        {
            try
            {
                await _storageEngine.CreateBackupAsync(companyId, reason: "pre_delete_safety", ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create safety backup before deleting company {CompanyId}", companyId);
            }
        }

        Directory.Delete(companyDir, recursive: true);
        _logger.LogInformation("Company {CompanyId} deleted from disk.", companyId);
        return true;
    }

    public async Task<bool> RenameCompanyAsync(string companyId, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New company name cannot be empty.", nameof(newName));
        }

        bool wasOpen = _companyContext.CurrentCompany?.CompanyNumber == companyId;

        if (wasOpen && _dataStore.CurrentData != null && _dataStore.CurrentDek != null && _dataStore.CurrentFilePath != null)
        {
            _dataStore.CurrentData.Company.CompanyName = newName.Trim();
            await _dataStore.SaveChangesAsync(ct);
            _companyContext.SetActiveCompany(_dataStore.CurrentData.Company, _companyContext.CurrentFinancialYear);
            return true;
        }

        string filePath = _storageEngine.GetCompanyFilePath(companyId);
        if (!File.Exists(filePath))
        {
            throw new CompanyNotFoundException(companyId);
        }

        using var compLock = _storageEngine.AcquireLock(companyId);
        // Note: renaming does NOT change immutable CompanyId
        var header = await _storageEngine.ReadHeaderAsync(filePath, ct);
        // Load with current secret/password
        var (data, dek) = await _storageEngine.LoadCompanyDataAsync(filePath, "MyERP_Default_Secret_2026", ct);
        data.Company.CompanyName = newName.Trim();
        await _storageEngine.SaveCompanyDataAsync(filePath, data, dek, null, ct);

        return true;
    }

    public async Task<IReadOnlyList<CompanySummaryDto>> GetCompaniesAsync(CancellationToken ct = default)
    {
        var list = new List<CompanySummaryDto>();
        var seenCompanyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dirsToScan = new List<string>();
        string primaryDir = _storageEngine.GetCompaniesRootDirectory();
        if (Directory.Exists(primaryDir))
        {
            dirsToScan.Add(primaryDir);
        }

        string legacySubDir = Path.Combine(primaryDir, "Companies");
        if (Directory.Exists(legacySubDir) && !legacySubDir.Equals(primaryDir, StringComparison.OrdinalIgnoreCase))
        {
            dirsToScan.Add(legacySubDir);
        }

        foreach (var companiesDir in dirsToScan)
        {
            var compDirs = Directory.GetDirectories(companiesDir);
            foreach (var dir in compDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("Backups", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("Logs", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("Companies", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string filePath = Path.Combine(dir, "company.data");
                if (!File.Exists(filePath)) continue;

                try
                {
                    var header = await _storageEngine.ReadHeaderAsync(filePath, ct);
                    if (!seenCompanyIds.Add(header.CompanyId)) continue;

                    string compName = header.HeaderMetadata.TryGetValue("CompanyName", out var name)
                        ? name
                        : Path.GetFileName(dir);

                    DateTime fyFrom = header.CreatedAtUtc;
                    if (header.HeaderMetadata.TryGetValue("FinancialYearFrom", out var fyStr) &&
                        DateTime.TryParse(fyStr, out var parsedFy))
                    {
                        fyFrom = parsedFy;
                    }

                    DateTime booksFrom = fyFrom;
                    if (header.HeaderMetadata.TryGetValue("BooksBeginningFrom", out var bbStr) &&
                        DateTime.TryParse(bbStr, out var parsedBb))
                    {
                        booksFrom = parsedBb;
                    }

                    string currency = header.HeaderMetadata.TryGetValue("Currency", out var curr) ? curr : "₹";

                    int compNumId = 1;
                    if (header.CompanyId.StartsWith("COMP-", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(header.CompanyId.Substring(5), out int n))
                    {
                        compNumId = n;
                    }
                    else if (int.TryParse(header.CompanyId, out int rawNum))
                    {
                        compNumId = rawNum;
                    }

                    list.Add(new CompanySummaryDto
                    {
                        CompanyId = compNumId,
                        CompanyNumber = header.CompanyId,
                        CompanyName = compName,
                        FinancialYearFrom = fyFrom,
                        BooksBeginningFrom = booksFrom,
                        Currency = currency,
                        IsActive = true,
                        IsPasswordProtected = false
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read company header for {Dir}", dir);
                }
            }
        }

        return list.OrderBy(c => c.CompanyName).ToList();
    }

    public async Task<CompanySummaryDto> ImportCompanyFileAsync(string sourceCompanyDataFilePath, string? targetCompanyId = null, CancellationToken ct = default)
    {
        if (!File.Exists(sourceCompanyDataFilePath))
        {
            throw new FileNotFoundException("Source company.data file not found.", sourceCompanyDataFilePath);
        }

        var header = await _storageEngine.ReadHeaderAsync(sourceCompanyDataFilePath, ct);
        string companyId = targetCompanyId ?? header.CompanyId;

        if (string.IsNullOrWhiteSpace(companyId))
        {
            var existing = await GetCompaniesAsync(ct);
            companyId = $"COMP-{(existing.Count + 1):D4}";
        }

        string targetDir = _storageEngine.GetCompanyDirectory(companyId);
        Directory.CreateDirectory(targetDir);
        string targetFilePath = _storageEngine.GetCompanyFilePath(companyId);

        File.Copy(sourceCompanyDataFilePath, targetFilePath, overwrite: true);

        string compName = header.HeaderMetadata.TryGetValue("CompanyName", out var name)
            ? name
            : companyId;

        return new CompanySummaryDto
        {
            CompanyId = 1,
            CompanyNumber = companyId,
            CompanyName = compName,
            FinancialYearFrom = header.CreatedAtUtc,
            BooksBeginningFrom = header.CreatedAtUtc,
            Currency = "₹",
            IsActive = true
        };
    }

    public Task<string> ExportCompanyFileAsync(string companyId, string targetDirectory, CancellationToken ct = default)
    {
        string sourceFile = _storageEngine.GetCompanyFilePath(companyId);
        if (!File.Exists(sourceFile))
        {
            throw new CompanyNotFoundException(companyId);
        }

        Directory.CreateDirectory(targetDirectory);
        string targetFile = Path.Combine(targetDirectory, $"{companyId}_company.data");
        File.Copy(sourceFile, targetFile, overwrite: true);

        return Task.FromResult(targetFile);
    }

    public CompanyFileValidationResult ValidateCompanyFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new CompanyFileValidationResult { IsValid = false, ErrorMessage = "File does not exist." };
        }

        try
        {
            var header = _storageEngine.ReadHeaderAsync(filePath).GetAwaiter().GetResult();
            bool requiresMigration = header.DataVersion < CompanyFileConstants.CurrentDataVersion;
            bool isNewer = header.DataVersion > CompanyFileConstants.CurrentDataVersion;

            return new CompanyFileValidationResult
            {
                IsValid = true,
                Header = header,
                RequiresMigration = requiresMigration,
                IsUnsupportedNewerVersion = isNewer
            };
        }
        catch (Exception ex)
        {
            return new CompanyFileValidationResult
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public bool CompanyExists(string companyId)
    {
        return File.Exists(_storageEngine.GetCompanyFilePath(companyId));
    }

    private static void SeedDefaultGroups(CompanyData data)
    {
        int cid = data.Company.CompanyId;
        var groups = new List<GroupEntity>
        {
            new() { GroupId = 1, CompanyId = cid, GroupName = "Capital Account", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 2, CompanyId = cid, GroupName = "Current Assets", Nature = GroupNature.Assets, PrimaryGroup = true, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 3, CompanyId = cid, GroupName = "Current Liabilities", Nature = GroupNature.Liabilities, PrimaryGroup = true, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 4, CompanyId = cid, GroupName = "Bank Accounts", ParentGroupId = 2, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 5, CompanyId = cid, GroupName = "Cash-in-hand", ParentGroupId = 2, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 6, CompanyId = cid, GroupName = "Sundry Debtors", ParentGroupId = 2, Nature = GroupNature.Assets, PrimaryGroup = false, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 7, CompanyId = cid, GroupName = "Sundry Creditors", ParentGroupId = 3, Nature = GroupNature.Liabilities, PrimaryGroup = false, AffectProfitLoss = false, IsActive = true },
            new() { GroupId = 8, CompanyId = cid, GroupName = "Sales Accounts", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true },
            new() { GroupId = 9, CompanyId = cid, GroupName = "Purchase Accounts", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true },
            new() { GroupId = 10, CompanyId = cid, GroupName = "Direct Expenses", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true },
            new() { GroupId = 11, CompanyId = cid, GroupName = "Indirect Expenses", Nature = GroupNature.Expenses, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true },
            new() { GroupId = 12, CompanyId = cid, GroupName = "Direct Incomes", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true },
            new() { GroupId = 13, CompanyId = cid, GroupName = "Indirect Incomes", Nature = GroupNature.Income, PrimaryGroup = true, AffectProfitLoss = true, IsActive = true }
        };

        data.Groups.AddRange(groups);
    }

    private static void SeedDefaultVoucherTypes(CompanyData data)
    {
        data.VoucherTypes.AddRange(new[]
        {
            new VoucherType { VoucherTypeId = 1, Name = "Payment", Code = "PYMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-", NextNumber = 1, IsActive = true },
            new VoucherType { VoucherTypeId = 2, Name = "Receipt", Code = "RCPT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-", NextNumber = 1, IsActive = true },
            new VoucherType { VoucherTypeId = 3, Name = "Contra", Code = "CNTR", Type = VoucherTypeEnum.Contra, Prefix = "CON-", NextNumber = 1, IsActive = true },
            new VoucherType { VoucherTypeId = 4, Name = "Journal", Code = "JRNL", Type = VoucherTypeEnum.Journal, Prefix = "JRN-", NextNumber = 1, IsActive = true },
            new VoucherType { VoucherTypeId = 5, Name = "Sales", Code = "SALE", Type = VoucherTypeEnum.Sales, Prefix = "SAL-", NextNumber = 1, IsActive = true },
            new VoucherType { VoucherTypeId = 6, Name = "Purchase", Code = "PURC", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-", NextNumber = 1, IsActive = true }
        });
    }

    private static void SeedDefaultLedgers(CompanyData data)
    {
        int cid = data.Company.CompanyId;
        data.Ledgers.Add(new LedgerEntity
        {
            LedgerId = 1,
            CompanyId = cid,
            GroupId = 5, // Cash-in-hand
            LedgerName = "Cash",
            OpeningBalance = 0,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        data.Ledgers.Add(new LedgerEntity
        {
            LedgerId = 2,
            CompanyId = cid,
            GroupId = 1, // Capital Account
            LedgerName = "Profit & Loss A/c",
            OpeningBalance = 0,
            OpeningBalanceType = BalanceType.Credit,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<bool> VerifyPasswordAsync(string companyId, string password, CancellationToken ct = default)
    {
        try
        {
            await _storageEngine.LoadCompanyDataAsync(companyId, password, ct);
            return true;
        }
        catch (InvalidPasswordException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password verification failed for company {CompanyId}", companyId);
            return false;
        }
    }
}
