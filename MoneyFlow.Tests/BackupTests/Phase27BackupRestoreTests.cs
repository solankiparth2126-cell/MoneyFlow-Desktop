using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services.Backup;
using MoneyFlow.Services.Company;
using Xunit;

namespace MoneyFlow.Tests.BackupTests;

public class Phase27BackupRestoreTests : IDisposable
{
    private readonly string _tempBackupDir;

    public Phase27BackupRestoreTests()
    {
        _tempBackupDir = Path.Combine(Path.GetTempPath(), "MoneyFlow_BackupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempBackupDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempBackupDir))
            {
                Directory.Delete(_tempBackupDir, true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    private (AppDbContext Context, CompanyService CompService, BackupRestoreService BackupService) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var companyRepo = new CompanyRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var groupRepo = new GroupRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var uow = new UnitOfWork(context);
        var companyContext = new CompanyContext();

        var compService = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            NullLogger<CompanyService>.Instance);

        var backupService = new BackupRestoreService(
            context,
            uow,
            NullLogger<BackupRestoreService>.Instance);

        return (context, compService, backupService);
    }

    private async Task<int> SeedSampleCompanyWithDataAsync(
        CompanyService compService,
        AppDbContext context,
        string companyName = "Test Backup Corp")
    {
        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = companyName,
            Address = "100 Industrial Area, Mumbai",
            State = "Maharashtra",
            Country = "India",
            FinancialYearFrom = new DateTime(2025, 4, 1),
            BooksBeginningFrom = new DateTime(2025, 4, 1),
            Currency = "₹"
        });

        // Add a unit and stock item
        var unit = new Unit
        {
            CompanyId = company.CompanyId,
            UnitName = "PCS",
            FormalName = "Pieces",
            DecimalPlaces = 0,
            IsActive = true
        };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var item = new StockItem
        {
            CompanyId = company.CompanyId,
            ItemName = "Widget Pro",
            UnitId = unit.UnitId,
            OpeningQuantity = 50,
            OpeningRate = 200,
            OpeningValue = 10000,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        context.StockItems.Add(item);
        await context.SaveChangesAsync();

        // Add a voucher (Cash to Capital or Sales)
        var cashLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");
        var pnlLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Profit & Loss A/c");

        var vType = await context.VoucherTypes.FirstOrDefaultAsync(t => t.Type == VoucherTypeEnum.Receipt);
        if (vType == null)
        {
            vType = new VoucherType { Name = "Receipt", Code = "RCP", Type = VoucherTypeEnum.Receipt, Prefix = "RCP-", NextNumber = 1, IsActive = true };
            context.VoucherTypes.Add(vType);
            await context.SaveChangesAsync();
        }

        var voucher = new Voucher
        {
            CompanyId = company.CompanyId,
            VoucherTypeId = vType.VoucherTypeId,
            VoucherNumber = "RCP-001",
            VoucherDate = new DateTime(2025, 4, 15),
            Narration = "Initial seed investment receipt",
            CreatedAt = DateTime.Now
        };
        context.Vouchers.Add(voucher);
        await context.SaveChangesAsync();

        context.VoucherEntries.Add(new VoucherEntry
        {
            VoucherId = voucher.VoucherId,
            LedgerId = cashLedger.LedgerId,
            Debit = 50000,
            Credit = 0,
            Narration = "Cash received"
        });
        context.VoucherEntries.Add(new VoucherEntry
        {
            VoucherId = voucher.VoucherId,
            LedgerId = pnlLedger.LedgerId,
            Debit = 0,
            Credit = 50000,
            Narration = "To Capital"
        });
        await context.SaveChangesAsync();

        return company.CompanyId;
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesValidPortableZipArchive_WithManifestAndChecksum()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int companyId = await SeedSampleCompanyWithDataAsync(compService, context);

        var options = new BackupCreateOptionsDto
        {
            CompanyId = companyId,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive,
            Comment = "Pre-Audit Backup"
        };

        // Act
        var result = await backupService.CreateBackupAsync(options);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().EndWith(".mfb");
        File.Exists(result.FullPath).Should().BeTrue();
        result.FileSizeBytes.Should().BeGreaterThan(0);
        result.CompanyName.Should().Be("Test Backup Corp");

        // Inspect ZIP structure
        using var zip = ZipFile.OpenRead(result.FullPath);
        zip.Entries.Should().Contain(e => e.FullName == "manifest.json");
        zip.Entries.Should().Contain(e => e.FullName == "data.json");

        var manifestEntry = zip.GetEntry("manifest.json")!;
        using var stream = manifestEntry.Open();
        using var reader = new StreamReader(stream);
        var manifest = JsonSerializer.Deserialize<BackupManifestDto>(await reader.ReadToEndAsync());

        manifest.Should().NotBeNull();
        manifest!.CompanyName.Should().Be("Test Backup Corp");
        manifest.ChecksumSha256.Should().NotBeNullOrWhiteSpace();
        manifest.LedgerCount.Should().BeGreaterThan(0);
        manifest.VoucherCount.Should().Be(1);
        manifest.StockItemCount.Should().Be(1);
        manifest.Comment.Should().Be("Pre-Audit Backup");
    }

    [Fact]
    public async Task ReadManifestAsync_ExtractsArchiveSummaryWithoutFullExtraction()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int companyId = await SeedSampleCompanyWithDataAsync(compService, context, "Alpha Logistics Ltd");

        var backup = await backupService.CreateBackupAsync(new BackupCreateOptionsDto
        {
            CompanyId = companyId,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive
        });

        // Act
        var manifest = await backupService.ReadManifestAsync(backup.FullPath);

        // Assert
        manifest.Should().NotBeNull();
        manifest!.CompanyName.Should().Be("Alpha Logistics Ltd");
        manifest.FinancialYearLabel.Should().Be("2025-26");
        manifest.VoucherCount.Should().Be(1);
        manifest.StockItemCount.Should().Be(1);
        manifest.ChecksumSha256.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RestoreBackupAsync_AsNewCompany_RestoresAllEntitiesWithRemappedKeys()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int originalCompId = await SeedSampleCompanyWithDataAsync(compService, context, "Original Enterprises");

        var backup = await backupService.CreateBackupAsync(new BackupCreateOptionsDto
        {
            CompanyId = originalCompId,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive
        });

        var restoreOptions = new RestoreOptionsDto
        {
            BackupFilePath = backup.FullPath,
            RestoreAsNewCompany = true,
            NewCompanyName = "Original Enterprises (Restored 2026)"
        };

        // Act
        var restoreResult = await backupService.RestoreBackupAsync(restoreOptions);

        // Assert
        restoreResult.Success.Should().BeTrue();
        restoreResult.RestoredCompanyId.Should().NotBeNull();
        restoreResult.RestoredCompanyId.Value.Should().NotBe(originalCompId);

        int newCompanyId = restoreResult.RestoredCompanyId.Value;
        var restoredComp = await context.Companies.FindAsync(newCompanyId);
        restoredComp.Should().NotBeNull();
        restoredComp!.CompanyName.Should().Be("Original Enterprises (Restored 2026)");

        // Check restored records
        var restoredGroups = await context.Groups.Where(g => g.CompanyId == newCompanyId).ToListAsync();
        restoredGroups.Should().HaveCount(17);

        var restoredLedgers = await context.Ledgers.Where(l => l.CompanyId == newCompanyId).ToListAsync();
        restoredLedgers.Should().HaveCount(11); // Standard 11 default ledgers

        var restoredItems = await context.StockItems.Where(s => s.CompanyId == newCompanyId).ToListAsync();
        restoredItems.Should().HaveCount(1);
        restoredItems[0].ItemName.Should().Be("Widget Pro");

        var restoredVouchers = await context.Vouchers.Include(v => v.VoucherEntries).Where(v => v.CompanyId == newCompanyId).ToListAsync();
        restoredVouchers.Should().HaveCount(1);
        restoredVouchers[0].VoucherNumber.Should().Be("RCP-001");
        restoredVouchers[0].VoucherEntries.Should().HaveCount(2);

        // Verify voucher entry ledgers belong to the NEW company (remapped correctly)
        foreach (var entry in restoredVouchers[0].VoucherEntries)
        {
            var entryLedger = await context.Ledgers.FindAsync(entry.LedgerId);
            entryLedger.Should().NotBeNull();
            entryLedger!.CompanyId.Should().Be(newCompanyId);
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_TamperedArchive_FailsChecksumVerification()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int companyId = await SeedSampleCompanyWithDataAsync(compService, context, "Tamper Proof Ltd");

        var backup = await backupService.CreateBackupAsync(new BackupCreateOptionsDto
        {
            CompanyId = companyId,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive
        });

        // Corrupt data.json inside the ZIP
        string tamperedZipPath = Path.Combine(_tempBackupDir, "tampered.mfb");
        File.Copy(backup.FullPath, tamperedZipPath);

        using (var archive = ZipFile.Open(tamperedZipPath, ZipArchiveMode.Update))
        {
            var dataEntry = archive.GetEntry("data.json")!;
            dataEntry.Delete();

            var newEntry = archive.CreateEntry("data.json");
            using var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8);
            writer.Write("{\"Tampered\": true, \"Company\": null}");
        }

        var restoreOptions = new RestoreOptionsDto
        {
            BackupFilePath = tamperedZipPath,
            RestoreAsNewCompany = true,
            NewCompanyName = "Tampered Restore"
        };

        // Act
        var result = await backupService.RestoreBackupAsync(restoreOptions);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Checksum mismatch");
    }

    [Fact]
    public async Task CreateBackupAsync_DoesNotSilentlyOverwrite_AppendsIncrementalCounter()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int companyId = await SeedSampleCompanyWithDataAsync(compService, context, "Collision Corp");

        var options = new BackupCreateOptionsDto
        {
            CompanyId = companyId,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive
        };

        // Act: Create first backup
        var backup1 = await backupService.CreateBackupAsync(options);

        // Create second backup (same second/minute)
        var backup2 = await backupService.CreateBackupAsync(options);

        // Assert
        backup1.FullPath.Should().NotBe(backup2.FullPath);
        File.Exists(backup1.FullPath).Should().BeTrue();
        File.Exists(backup2.FullPath).Should().BeTrue();
        backup2.FileName.Should().Contain("_01.mfb");
    }

    [Fact]
    public async Task GetBackupHistoryAsync_EnumeratesFilesAccurately()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int comp1 = await SeedSampleCompanyWithDataAsync(compService, context, "Company A");
        int comp2 = await SeedSampleCompanyWithDataAsync(compService, context, "Company B");

        await backupService.CreateBackupAsync(new BackupCreateOptionsDto { CompanyId = comp1, TargetDirectory = _tempBackupDir });
        await backupService.CreateBackupAsync(new BackupCreateOptionsDto { CompanyId = comp2, TargetDirectory = _tempBackupDir });

        // Act
        var history = await backupService.GetBackupHistoryAsync(_tempBackupDir);

        // Assert
        history.Should().HaveCount(2);
        history.Should().Contain(h => h.CompanyName == "Company A");
        history.Should().Contain(h => h.CompanyName == "Company B");
        history.All(h => h.IsValid).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreBackupAsync_OverwriteExistingCompany_ReplacesExistingData()
    {
        // Arrange
        var (context, compService, backupService) = CreateTestSetup();
        int compOriginal = await SeedSampleCompanyWithDataAsync(compService, context, "Base Company");
        int compTarget = await SeedSampleCompanyWithDataAsync(compService, context, "Target Overwrite Company");

        // Backup Base Company
        var backup = await backupService.CreateBackupAsync(new BackupCreateOptionsDto
        {
            CompanyId = compOriginal,
            TargetDirectory = _tempBackupDir,
            BackupType = BackupType.CompanyArchive
        });

        // Add distinct custom ledger in target to verify it gets wiped during overwrite
        var targetCash = await context.Ledgers.FirstAsync(l => l.CompanyId == compTarget && l.LedgerName == "Cash");
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = compTarget,
            GroupId = targetCash.GroupId,
            LedgerName = "Old Custom Target Ledger",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var restoreOptions = new RestoreOptionsDto
        {
            BackupFilePath = backup.FullPath,
            RestoreAsNewCompany = false,
            TargetCompanyId = compTarget
        };

        // Act
        var result = await backupService.RestoreBackupAsync(restoreOptions);

        // Assert
        result.Success.Should().BeTrue();
        result.RestoredCompanyId.Should().Be(compTarget);

        // The old custom ledger should no longer exist
        var customLedger = await context.Ledgers.FirstOrDefaultAsync(l => l.CompanyId == compTarget && l.LedgerName == "Old Custom Target Ledger");
        customLedger.Should().BeNull();

        // Base company data should now be in Target
        var targetItems = await context.StockItems.Where(s => s.CompanyId == compTarget).ToListAsync();
        targetItems.Should().HaveCount(1);
        targetItems[0].ItemName.Should().Be("Widget Pro");
    }
}
