using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Backup;

public class BackupRestoreService : IBackupRestoreService
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BackupRestoreService> _logger;

    public BackupRestoreService(
        AppDbContext context,
        IUnitOfWork uow,
        ILogger<BackupRestoreService> logger)
    {
        _context = context;
        _uow = uow;
        _logger = logger;
    }

    public string GetDefaultBackupDirectory()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(docs, "MoneyFlow", "Backups");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public async Task<BackupFileInfo> CreateBackupAsync(BackupCreateOptionsDto options, CancellationToken ct = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == options.CompanyId, ct);

        if (company == null)
        {
            throw new InvalidOperationException($"Company with ID {options.CompanyId} not found.");
        }

        string dir = string.IsNullOrWhiteSpace(options.TargetDirectory)
            ? GetDefaultBackupDirectory()
            : options.TargetDirectory;

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string safeCompName = string.Concat(company.CompanyName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        string ext = options.BackupType == BackupType.FullDatabase ? "bak" : "mfb";

        string baseFileName = $"{safeCompName}_{timestamp}.{ext}";
        string fullPath = Path.Combine(dir, baseFileName);

        // Never silently overwrite: append increment if file already exists
        int counter = 1;
        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(dir, $"{safeCompName}_{timestamp}_{counter:D2}.{ext}");
            counter++;
        }

        if (options.BackupType == BackupType.FullDatabase && _context.Database.IsRelational())
        {
            // Native SQL Server database backup
            string dbName = _context.Database.GetDbConnection().Database;
            string sql = $"BACKUP DATABASE [{dbName}] TO DISK = N'{fullPath.Replace("'", "''")}' WITH FORMAT, INIT, COMPRESSION;";
            await _context.Database.ExecuteSqlRawAsync(sql, ct);
        }
        else
        {
            // Portable Company Archive (.mfb)
            await CreateCompanyArchiveAsync(company, fullPath, options.Comment, ct);
        }

        var fi = new FileInfo(fullPath);
        return new BackupFileInfo
        {
            FileName = fi.Name,
            FullPath = fi.FullName,
            BackupType = options.BackupType,
            FileSizeBytes = fi.Length,
            CreatedAt = fi.CreationTime,
            CompanyName = company.CompanyName,
            IsValid = true
        };
    }

    private async Task CreateCompanyArchiveAsync(Core.Entities.Company company, string fullPath, string comment, CancellationToken ct)
    {
        int companyId = company.CompanyId;

        var fyList = await _context.FinancialYears
            .AsNoTracking()
            .Where(f => f.CompanyId == companyId)
            .Select(f => new FinancialYearDto
            {
                FinancialYearId = f.FinancialYearId,
                CompanyId = f.CompanyId,
                YearName = f.YearName,
                StartDate = f.StartDate,
                EndDate = f.EndDate,
                IsClosed = f.IsClosed
            })
            .ToListAsync(ct);

        var groupList = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId)
            .OrderBy(g => g.ParentGroupId)
            .Select(g => new GroupDto
            {
                GroupId = g.GroupId,
                CompanyId = g.CompanyId,
                GroupName = g.GroupName,
                ParentGroupId = g.ParentGroupId,
                Nature = g.Nature,
                PrimaryGroup = g.PrimaryGroup,
                AffectProfitLoss = g.AffectProfitLoss,
                IsActive = g.IsActive
            })
            .ToListAsync(ct);

        var ledgerList = await _context.Ledgers
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .Select(l => new LedgerSummaryDto
            {
                LedgerId = l.LedgerId,
                GroupId = l.GroupId,
                LedgerName = l.LedgerName,
                OpeningBalance = l.OpeningBalance,
                OpeningBalanceType = l.OpeningBalanceType,
                IsActive = l.IsActive
            })
            .ToListAsync(ct);

        var unitList = await _context.Units
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .Select(u => new UnitDto
            {
                UnitId = u.UnitId,
                CompanyId = u.CompanyId,
                UnitName = u.UnitName,
                FormalName = u.FormalName,
                DecimalPlaces = u.DecimalPlaces,
                IsActive = u.IsActive
            })
            .ToListAsync(ct);

        var itemList = await _context.StockItems
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => new StockItemDto
            {
                StockItemId = s.StockItemId,
                CompanyId = s.CompanyId,
                ItemName = s.ItemName,
                UnitId = s.UnitId,
                OpeningQuantity = s.OpeningQuantity,
                OpeningRate = s.OpeningRate,
                OpeningValue = s.OpeningValue,
                IsActive = s.IsActive
            })
            .ToListAsync(ct);

        var voucherList = await _context.Vouchers
            .AsNoTracking()
            .Include(v => v.VoucherEntries)
            .Where(v => v.CompanyId == companyId && !v.IsDeleted)
            .OrderBy(v => v.VoucherDate)
            .Select(v => new VoucherDto
            {
                VoucherId = v.VoucherId,
                CompanyId = v.CompanyId,
                VoucherTypeId = v.VoucherTypeId,
                VoucherNumber = v.VoucherNumber,
                VoucherDate = v.VoucherDate,
                ReferenceNumber = v.ReferenceNumber,
                Narration = v.Narration,
                IsDeleted = v.IsDeleted,
                VoucherEntries = v.VoucherEntries.Select(e => new VoucherEntryDto
                {
                    VoucherEntryId = e.VoucherEntryId,
                    VoucherId = e.VoucherId,
                    LedgerId = e.LedgerId,
                    Debit = e.Debit,
                    Credit = e.Credit,
                    Narration = e.Narration
                }).ToList()
            })
            .ToListAsync(ct);

        var payload = new CompanyBackupPayloadDto
        {
            Company = new CompanyDto
            {
                CompanyId = company.CompanyId,
                CompanyName = company.CompanyName,
                Address = company.Address,
                State = company.State,
                Country = company.Country,
                Phone = company.Phone,
                Email = company.Email,
                PAN = company.PAN,
                Currency = company.Currency,
                FinancialYearFrom = company.FinancialYearFrom,
                BooksBeginningFrom = company.BooksBeginningFrom,
                IsActive = company.IsActive
            },
            FinancialYears = fyList,
            Groups = groupList,
            Ledgers = ledgerList,
            Units = unitList,
            StockItems = itemList,
            Vouchers = voucherList
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
        string dataJson = JsonSerializer.Serialize(payload, jsonOptions);

        string hash;
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataJson));
            hash = Convert.ToHexString(hashBytes);
        }

        var manifest = new BackupManifestDto
        {
            CompanyId = company.CompanyId,
            CompanyName = company.CompanyName,
            FinancialYearLabel = fyList.FirstOrDefault(f => !f.IsClosed)?.YearName ?? fyList.FirstOrDefault()?.YearName ?? "Default",
            LedgerCount = ledgerList.Count,
            VoucherCount = voucherList.Count,
            StockItemCount = itemList.Count,
            ChecksumSha256 = hash,
            Comment = comment,
            CreatedAt = DateTime.Now
        };

        string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        // Package into ZIP Archive (.mfb)
        using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using (var entryStream = manifestEntry.Open())
        using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
        {
            await writer.WriteAsync(manifestJson);
        }

        var dataEntry = archive.CreateEntry("data.json", CompressionLevel.Optimal);
        using (var entryStream = dataEntry.Open())
        using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
        {
            await writer.WriteAsync(dataJson);
        }
    }

    public async Task<BackupManifestDto?> ReadManifestAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath)) return null;

        if (backupFilePath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            var fi = new FileInfo(backupFilePath);
            return new BackupManifestDto
            {
                CompanyName = "SQL Server Full Database Backup",
                FinancialYearLabel = "All Periods",
                Comment = $"Native SQL Server binary backup ({fi.Length / (1024.0 * 1024.0):F2} MB)",
                CreatedAt = fi.CreationTime
            };
        }

        try
        {
            using var fileStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            var entry = archive.GetEntry("manifest.json");
            if (entry == null) return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string json = await reader.ReadToEndAsync(ct);
            return JsonSerializer.Deserialize<BackupManifestDto>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed reading backup manifest from {Path}", backupFilePath);
            return null;
        }
    }

    public async Task<RestoreResultDto> RestoreBackupAsync(RestoreOptionsDto options, CancellationToken ct = default)
    {
        if (!File.Exists(options.BackupFilePath))
        {
            return new RestoreResultDto { Success = false, Message = "Backup file not found." };
        }

        if (options.BackupFilePath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
        {
            if (!_context.Database.IsRelational())
            {
                return new RestoreResultDto { Success = false, Message = "SQL Server .bak files can only be restored on a live relational SQL Server database." };
            }

            try
            {
                string dbName = _context.Database.GetDbConnection().Database;
                string sql = $"USE master; ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [{dbName}] FROM DISK = N'{options.BackupFilePath.Replace("'", "''")}' WITH REPLACE; ALTER DATABASE [{dbName}] SET MULTI_USER;";
                await _context.Database.ExecuteSqlRawAsync(sql, ct);
                return new RestoreResultDto { Success = true, Message = "Full SQL Server database successfully restored." };
            }
            catch (Exception ex)
            {
                return new RestoreResultDto { Success = false, Message = $"Restore failed: {ex.Message}" };
            }
        }

        // Restore .mfb Company Archive
        try
        {
            using var fileStream = new FileStream(options.BackupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry("manifest.json");
            var dataEntry = archive.GetEntry("data.json");
            if (manifestEntry == null || dataEntry == null)
            {
                return new RestoreResultDto { Success = false, Message = "Invalid backup archive: missing manifest or data." };
            }

            string dataJson;
            using (var stream = dataEntry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                dataJson = await reader.ReadToEndAsync(ct);
            }

            // Verify Checksum
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string manifestJson = await reader.ReadToEndAsync(ct);
                var manifest = JsonSerializer.Deserialize<BackupManifestDto>(manifestJson);
                if (manifest != null && !string.IsNullOrWhiteSpace(manifest.ChecksumSha256))
                {
                    using var sha = SHA256.Create();
                    string computed = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(dataJson)));
                    if (!string.Equals(computed, manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return new RestoreResultDto { Success = false, Message = "Backup integrity verification failed: Checksum mismatch." };
                    }
                }
            }

            var payload = JsonSerializer.Deserialize<CompanyBackupPayloadDto>(dataJson);
            if (payload == null || payload.Company == null)
            {
                return new RestoreResultDto { Success = false, Message = "Failed deserializing company data payload." };
            }

            int targetCompanyId;
            string restoredName = options.NewCompanyName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(restoredName))
            {
                restoredName = options.RestoreAsNewCompany ? $"{payload.Company.CompanyName} (Restored)" : payload.Company.CompanyName;
            }

            if (options.RestoreAsNewCompany || !options.TargetCompanyId.HasValue)
            {
                // Create New Company
                var newCompany = new Core.Entities.Company
                {
                    CompanyName = restoredName,
                    Address = payload.Company.Address,
                    State = payload.Company.State,
                    Country = payload.Company.Country,
                    Phone = payload.Company.Phone,
                    Email = payload.Company.Email,
                    PAN = payload.Company.PAN,
                    Currency = payload.Company.Currency,
                    FinancialYearFrom = payload.Company.FinancialYearFrom,
                    BooksBeginningFrom = payload.Company.BooksBeginningFrom,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _context.Companies.Add(newCompany);
                await _context.SaveChangesAsync(ct);
                targetCompanyId = newCompany.CompanyId;
            }
            else
            {
                targetCompanyId = options.TargetCompanyId.Value;
                // Delete existing child data for targetCompanyId
                var oldVouchers = await _context.Vouchers.Include(v => v.VoucherEntries).Where(v => v.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.Vouchers.RemoveRange(oldVouchers);

                var oldStock = await _context.StockItems.Where(s => s.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.StockItems.RemoveRange(oldStock);

                var oldUnits = await _context.Units.Where(u => u.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.Units.RemoveRange(oldUnits);

                var oldLedgers = await _context.Ledgers.Where(l => l.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.Ledgers.RemoveRange(oldLedgers);

                var oldGroups = await _context.Groups.Where(g => g.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.Groups.RemoveRange(oldGroups);

                var oldFys = await _context.FinancialYears.Where(f => f.CompanyId == targetCompanyId).ToListAsync(ct);
                _context.FinancialYears.RemoveRange(oldFys);

                await _context.SaveChangesAsync(ct);
            }

            // Restore Financial Years
            foreach (var fy in payload.FinancialYears)
            {
                _context.FinancialYears.Add(new Core.Entities.FinancialYear
                {
                    CompanyId = targetCompanyId,
                    YearName = fy.YearName,
                    StartDate = fy.StartDate,
                    EndDate = fy.EndDate,
                    IsClosed = fy.IsClosed,
                    CreatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync(ct);

            // Restore Groups (with hierarchy mapping)
            var groupMap = new Dictionary<int, int>();
            // 1. Primary groups
            foreach (var g in payload.Groups.Where(g => !g.ParentGroupId.HasValue))
            {
                var newGrp = new Core.Entities.Group
                {
                    CompanyId = targetCompanyId,
                    GroupName = g.GroupName,
                    ParentGroupId = null,
                    Nature = g.Nature,
                    PrimaryGroup = g.PrimaryGroup,
                    AffectProfitLoss = g.AffectProfitLoss,
                    IsActive = g.IsActive,
                    CreatedAt = DateTime.Now
                };
                _context.Groups.Add(newGrp);
                await _context.SaveChangesAsync(ct);
                groupMap[g.GroupId] = newGrp.GroupId;
            }
            // 2. Sub groups
            foreach (var g in payload.Groups.Where(g => g.ParentGroupId.HasValue))
            {
                int? parentId = null;
                if (groupMap.TryGetValue(g.ParentGroupId!.Value, out int mappedParent))
                {
                    parentId = mappedParent;
                }

                var newGrp = new Core.Entities.Group
                {
                    CompanyId = targetCompanyId,
                    GroupName = g.GroupName,
                    ParentGroupId = parentId,
                    Nature = g.Nature,
                    PrimaryGroup = g.PrimaryGroup,
                    AffectProfitLoss = g.AffectProfitLoss,
                    IsActive = g.IsActive,
                    CreatedAt = DateTime.Now
                };
                _context.Groups.Add(newGrp);
                await _context.SaveChangesAsync(ct);
                groupMap[g.GroupId] = newGrp.GroupId;
            }

            // Restore Units
            var unitMap = new Dictionary<int, int>();
            foreach (var u in payload.Units)
            {
                var newUnit = new Unit
                {
                    CompanyId = targetCompanyId,
                    UnitName = u.UnitName,
                    FormalName = u.FormalName,
                    DecimalPlaces = u.DecimalPlaces,
                    IsActive = u.IsActive
                };
                _context.Units.Add(newUnit);
                await _context.SaveChangesAsync(ct);
                unitMap[u.UnitId] = newUnit.UnitId;
            }

            // Restore Stock Items
            foreach (var s in payload.StockItems)
            {
                int? unitId = null;
                if (s.UnitId.HasValue && unitMap.TryGetValue(s.UnitId.Value, out int mappedUnit))
                {
                    unitId = mappedUnit;
                }

                _context.StockItems.Add(new StockItem
                {
                    CompanyId = targetCompanyId,
                    ItemName = s.ItemName,
                    UnitId = unitId,
                    OpeningQuantity = s.OpeningQuantity,
                    OpeningRate = s.OpeningRate,
                    OpeningValue = s.OpeningValue,
                    IsActive = s.IsActive,
                    CreatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync(ct);

            // Restore Ledgers
            var ledgerMap = new Dictionary<int, int>();
            foreach (var l in payload.Ledgers)
            {
                int grpId = groupMap.TryGetValue(l.GroupId, out int mappedGrp) ? mappedGrp : groupMap.Values.FirstOrDefault();
                var newLedger = new Core.Entities.Ledger
                {
                    CompanyId = targetCompanyId,
                    GroupId = grpId,
                    LedgerName = l.LedgerName,
                    OpeningBalance = l.OpeningBalance,
                    OpeningBalanceType = l.OpeningBalanceType,
                    IsActive = l.IsActive,
                    CreatedAt = DateTime.Now
                };
                _context.Ledgers.Add(newLedger);
                await _context.SaveChangesAsync(ct);
                ledgerMap[l.LedgerId] = newLedger.LedgerId;
            }

            // Restore Vouchers & Entries
            var voucherTypes = await _context.VoucherTypes.ToListAsync(ct);
            int totalRecords = payload.Ledgers.Count + payload.StockItems.Count + payload.Vouchers.Count;

            foreach (var v in payload.Vouchers)
            {
                // Voucher type resolution
                var vType = voucherTypes.FirstOrDefault(t => t.VoucherTypeId == v.VoucherTypeId)
                            ?? voucherTypes.FirstOrDefault()
                            ?? new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-", NextNumber = 1, IsActive = true };

                var voucher = new Voucher
                {
                    CompanyId = targetCompanyId,
                    VoucherTypeId = vType.VoucherTypeId,
                    VoucherNumber = v.VoucherNumber,
                    VoucherDate = v.VoucherDate,
                    ReferenceNumber = v.ReferenceNumber,
                    Narration = v.Narration,
                    IsDeleted = v.IsDeleted,
                    CreatedAt = DateTime.Now
                };
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync(ct);

                foreach (var entry in v.VoucherEntries)
                {
                    if (ledgerMap.TryGetValue(entry.LedgerId, out int mappedLedger))
                    {
                        _context.VoucherEntries.Add(new VoucherEntry
                        {
                            VoucherId = voucher.VoucherId,
                            LedgerId = mappedLedger,
                            Debit = entry.Debit,
                            Credit = entry.Credit,
                            Narration = entry.Narration
                        });
                    }
                }
            }

            await _uow.SaveChangesAsync(ct);

            return new RestoreResultDto
            {
                Success = true,
                Message = $"Company '{restoredName}' restored successfully with {totalRecords} entities.",
                RestoredCompanyId = targetCompanyId,
                RecordsRestoredCount = totalRecords
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed restoring backup from {Path}", options.BackupFilePath);
            return new RestoreResultDto { Success = false, Message = $"Restoration error: {ex.Message}" };
        }
    }

    public async Task<IReadOnlyList<BackupFileInfo>> GetBackupHistoryAsync(string? directory = null, CancellationToken ct = default)
    {
        string dir = string.IsNullOrWhiteSpace(directory) ? GetDefaultBackupDirectory() : directory;
        var list = new List<BackupFileInfo>();
        if (!Directory.Exists(dir)) return list;

        var files = Directory.GetFiles(dir, "*.*")
            .Where(f => f.EndsWith(".mfb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => File.GetCreationTime(f));

        foreach (var file in files)
        {
            var fi = new FileInfo(file);
            var manifest = await ReadManifestAsync(file, ct);
            list.Add(new BackupFileInfo
            {
                FileName = fi.Name,
                FullPath = fi.FullName,
                BackupType = file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ? BackupType.FullDatabase : BackupType.CompanyArchive,
                FileSizeBytes = fi.Length,
                CreatedAt = fi.CreationTime,
                CompanyName = manifest?.CompanyName ?? "Unknown Company",
                IsValid = manifest != null
            });
        }

        return list;
    }
}
