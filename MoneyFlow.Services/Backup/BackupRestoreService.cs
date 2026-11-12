using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
<<<<<<< Updated upstream
using MoneyFlow.Data;
=======
using MoneyFlow.Data.Repositories;

>>>>>>> Stashed changes
namespace MoneyFlow.Services.Backup;

using Group = MoneyFlow.Core.Entities.Group;
using Ledger = MoneyFlow.Core.Entities.Ledger;

public class BackupRestoreService : IBackupRestoreService
{
<<<<<<< Updated upstream
    private readonly AppDataContext _context;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BackupRestoreService> _logger;

    public BackupRestoreService(AppDataContext context, IUnitOfWork uow, ILogger<BackupRestoreService> logger)
=======
    private readonly ICompanyStorageEngine _storageEngine;
    private readonly ICompanyManager _companyManager;
    private readonly ICompanyRepository _companyRepo;
    private readonly ICompanyDataStore _dataStore;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BackupRestoreService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public BackupRestoreService(
        IUnitOfWork uow,
        ILogger<BackupRestoreService> logger,
        ICompanyStorageEngine? storageEngine = null,
        ICompanyManager? companyManager = null,
        ICompanyRepository? companyRepo = null,
        ICompanyDataStore? dataStore = null)
>>>>>>> Stashed changes
    {
        _storageEngine = storageEngine ?? new MoneyFlow.Data.Storage.CompanyStorageEngine(new MoneyFlow.Data.Storage.CompanyDataCrypto());
        _companyManager = companyManager!;
        _companyRepo = companyRepo!;
        _dataStore = dataStore!;
        _uow = uow;
        _logger = logger;
    }

    internal BackupRestoreService(
        object context,
        IUnitOfWork uow,
        ILogger<BackupRestoreService> logger)
        : this(uow, logger)
    {
    }

    public string GetDefaultBackupDirectory()
    {
<<<<<<< Updated upstream
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(docs, "MoneyFlow", "Backups");
        Directory.CreateDirectory(path);
        return path;
=======
        return _storageEngine.GetBackupsDirectory();
>>>>>>> Stashed changes
    }

    public async Task<BackupFileInfo> CreateBackupAsync(BackupCreateOptionsDto options, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var company = _context.Companies.FirstOrDefault(c => c.CompanyId == options.CompanyId);
        if (company == null) throw new InvalidOperationException("Company not found.");

        string dir = string.IsNullOrWhiteSpace(options.TargetDirectory) ? GetDefaultBackupDirectory() : options.TargetDirectory;
        Directory.CreateDirectory(dir);

        string safeName = string.Concat(company.CompanyName.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        string ts = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
        string fullPath = Path.Combine(dir, $"{safeName}_{ts}.mfb");
        int c2 = 1;
        while (File.Exists(fullPath)) { fullPath = Path.Combine(dir, $"{safeName}_{ts}_{c2++:D2}.mfb"); }

        await CreateCompanyArchiveAsync(company, fullPath, options.Comment, ct);

        var fi = new FileInfo(fullPath);
        return new BackupFileInfo { FileName = fi.Name, FullPath = fi.FullName, BackupType = BackupType.CompanyArchive, FileSizeBytes = fi.Length, CreatedAt = fi.CreationTime, CompanyName = company.CompanyName, IsValid = true };
=======
        var company = await _companyRepo.GetByIdAsync(options.CompanyId, ct);
        string companyNumber = company?.CompanyNumber ?? $"COMP-{options.CompanyId:D4}";

        string backupPath = await _storageEngine.CreateBackupAsync(
            companyNumber,
            customBackupDir: options.TargetDirectory,
            reason: options.Comment,
            ct: ct);

        var fi = new FileInfo(backupPath);
        return new BackupFileInfo
        {
            FileName = fi.Name,
            FullPath = fi.FullName,
            BackupType = options.BackupType,
            FileSizeBytes = fi.Length,
            CreatedAt = fi.CreationTime,
            CompanyName = company?.CompanyName ?? companyNumber,
            IsValid = true
        };
>>>>>>> Stashed changes
    }

    public async Task<BackupManifestDto?> ReadManifestAsync(string backupFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath)) return null;
<<<<<<< Updated upstream
        try
        {
            using var archive = ZipFile.OpenRead(backupFilePath);
            var entry = archive.GetEntry("manifest.json");
            if (entry == null) return null;
            using var stream = entry.Open();
            return await JsonSerializer.DeserializeAsync<BackupManifestDto>(stream, cancellationToken: ct);
=======

        try
        {
            var header = await _storageEngine.ReadHeaderAsync(backupFilePath, ct);
            return new BackupManifestDto
            {
                AppVersion = header.DataVersion.ToString(),
                CreatedAt = header.CreatedAtUtc,
                CompanyName = header.HeaderMetadata.TryGetValue("CompanyName", out var n) ? n : header.CompanyId,
                Comment = $"Encrypted Company Backup ({header.CompanyId})",
                ChecksumSha256 = "AUTHENTICATED_AES_GCM"
            };
        }
        catch
        {
            return null;
>>>>>>> Stashed changes
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read manifest."); return null; }
    }

    public async Task<RestoreResultDto> RestoreBackupAsync(RestoreOptionsDto options, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        try
        {
            if (!File.Exists(options.BackupFilePath))
                return new RestoreResultDto { Success = false, Message = "Backup file not found." };

            using var archive = ZipFile.OpenRead(options.BackupFilePath);
            var dataEntry = archive.GetEntry("company_data.json");
            if (dataEntry == null) return new RestoreResultDto { Success = false, Message = "Invalid backup." };

            using var stream = dataEntry.Open();
            var payload = await JsonSerializer.DeserializeAsync<CompanyBackupPayloadDto>(stream, cancellationToken: ct);
            if (payload == null) return new RestoreResultDto { Success = false, Message = "Deserialization failed." };

            int companyId = options.TargetCompanyId ?? payload.Company.CompanyId;
            int count = 0;
            foreach (var g in payload.Groups) { _context.Add(new Group { CompanyId = companyId, GroupName = g.GroupName, Nature = g.Nature, PrimaryGroup = g.PrimaryGroup, AffectProfitLoss = g.AffectProfitLoss, IsActive = g.IsActive, CreatedAt = DateTime.Now }); count++; }
            foreach (var l in payload.Ledgers) { _context.Add(new Ledger { CompanyId = companyId, LedgerName = l.LedgerName, GroupId = l.GroupId, OpeningBalance = l.OpeningBalance, OpeningBalanceType = l.OpeningBalanceType, IsActive = true, CreatedAt = DateTime.Now }); count++; }
            await _context.SaveChangesAsync(ct);
            return new RestoreResultDto { Success = true, Message = $"Restored {count} records.", RestoredCompanyId = companyId, RecordsRestoredCount = count };
=======
        if (!File.Exists(options.BackupFilePath))
        {
            return new RestoreResultDto { Success = false, Message = "Backup file not found." };
        }

        try
        {
            var header = await _storageEngine.ReadHeaderAsync(options.BackupFilePath, ct);
            string targetCompId = options.TargetCompanyId.HasValue
                ? $"COMP-{options.TargetCompanyId.Value:D4}"
                : header.CompanyId;

            await _storageEngine.RestoreBackupAsync(options.BackupFilePath, targetCompId, "MyERP_Default_Secret_2026", ct);
            return new RestoreResultDto { Success = true, Message = $"Company '{header.CompanyId}' successfully restored." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed restoring backup from {Path}", options.BackupFilePath);
            return new RestoreResultDto { Success = false, Message = $"Restore failed: {ex.Message}" };
>>>>>>> Stashed changes
        }
        catch (Exception ex) { _logger.LogError(ex, "Restore failed."); return new RestoreResultDto { Success = false, Message = ex.Message }; }
    }

    public Task<IReadOnlyList<BackupFileInfo>> GetBackupHistoryAsync(string? directory = null, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        string dir = directory ?? GetDefaultBackupDirectory();
        var results = new List<BackupFileInfo>();
        if (!Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<BackupFileInfo>>(results.AsReadOnly());

        foreach (var file in Directory.GetFiles(dir, "*.mfb"))
        {
            var fi = new FileInfo(file);
            results.Add(new BackupFileInfo { FileName = fi.Name, FullPath = fi.FullName, BackupType = BackupType.CompanyArchive, FileSizeBytes = fi.Length, CreatedAt = fi.CreationTime, IsValid = true });
=======
        string dir = string.IsNullOrWhiteSpace(directory) ? GetDefaultBackupDirectory() : directory;
        var list = new List<BackupFileInfo>();

        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<BackupFileInfo>>(list);
        }

        var files = Directory.GetFiles(dir, "*.bak", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(dir, "*.mfb", SearchOption.AllDirectories));

        foreach (var f in files)
        {
            var fi = new FileInfo(f);
            list.Add(new BackupFileInfo
            {
                FileName = fi.Name,
                FullPath = fi.FullName,
                BackupType = BackupType.CompanyArchive,
                FileSizeBytes = fi.Length,
                CreatedAt = fi.CreationTime,
                CompanyName = fi.Name,
                IsValid = true
            });
>>>>>>> Stashed changes
        }
        return Task.FromResult<IReadOnlyList<BackupFileInfo>>(results.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly());
    }

<<<<<<< Updated upstream
    private async Task CreateCompanyArchiveAsync(MoneyFlow.Core.Entities.Company company, string outputPath, string comment, CancellationToken ct)
    {
        var ledgers = _context.Ledgers.Where(l => l.CompanyId == company.CompanyId && l.IsActive).ToList();
        var vouchers = _context.Vouchers.Where(v => v.CompanyId == company.CompanyId && !v.IsDeleted).ToList();
        var groups = _context.Groups.Where(g => g.CompanyId == company.CompanyId && g.IsActive).ToList();
        var fys = _context.FinancialYears.Where(fy => fy.CompanyId == company.CompanyId).ToList();

        var manifest = new BackupManifestDto { AppVersion = "1.0.0", CreatedAt = DateTime.Now, CompanyId = company.CompanyId, CompanyName = company.CompanyName, FinancialYearLabel = fys.FirstOrDefault()?.YearName ?? "", LedgerCount = ledgers.Count, VoucherCount = vouchers.Count, Comment = comment };
        var payload = new CompanyBackupPayloadDto
        {
            Manifest = manifest,
            Company = new CompanyDto { CompanyId = company.CompanyId, CompanyName = company.CompanyName, Address = company.Address },
            FinancialYears = fys.Select(fy => new FinancialYearDto { FinancialYearId = fy.FinancialYearId, CompanyId = fy.CompanyId, YearName = fy.YearName, StartDate = fy.StartDate, EndDate = fy.EndDate }).ToList(),
            Groups = groups.Select(g => new GroupDto { GroupId = g.GroupId, CompanyId = g.CompanyId, GroupName = g.GroupName, ParentGroupId = g.ParentGroupId, Nature = g.Nature, PrimaryGroup = g.PrimaryGroup, AffectProfitLoss = g.AffectProfitLoss, IsActive = g.IsActive }).ToList(),
            Ledgers = ledgers.Select(l => new LedgerSummaryDto { LedgerId = l.LedgerId, LedgerName = l.LedgerName, GroupId = l.GroupId, OpeningBalance = l.OpeningBalance, OpeningBalanceType = l.OpeningBalanceType }).ToList()
        };

        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, payload, cancellationToken: ct);
        var jsonBytes = ms.ToArray();
        manifest.ChecksumSha256 = Convert.ToHexString(SHA256.HashData(jsonBytes));

        using var zipStream = new FileStream(outputPath, FileMode.Create);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);
        var mEntry = zip.CreateEntry("manifest.json");
        using (var mStream = mEntry.Open()) { await JsonSerializer.SerializeAsync(mStream, manifest, cancellationToken: ct); }
        var dEntry = zip.CreateEntry("company_data.json", CompressionLevel.Optimal);
        using (var dStream = dEntry.Open()) { await dStream.WriteAsync(jsonBytes, ct); }
        _logger.LogInformation("Backup created: {Path}", outputPath);
=======
        IReadOnlyList<BackupFileInfo> result = list.OrderByDescending(b => b.CreatedAt).ToList();
        return Task.FromResult(result);
>>>>>>> Stashed changes
    }
}
