using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Data;
using MoneyFlow.Data.Encryption;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Data.Storage;
using MoneyFlow.Services.Company;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class CompanyPersistenceAndConfigTests : IDisposable
{
    private readonly string _tempTestDir;

    public CompanyPersistenceAndConfigTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"MoneyFlowTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task GetAllCompaniesAsync_WhenNoCompaniesExist_ReturnsEmptyList_WithoutGhostRow()
    {
        var storageEngine = new StorageEngine();
        var keyManager = new KeyManager();
        var session = new CompanySession(storageEngine, keyManager);
        var uow = new FileUnitOfWork(session);
        var companyRepo = new FileCompanyRepository(session);
        var fyRepo = new FileFinancialYearRepository(session);
        var groupRepo = new FileGroupRepository(session);
        var ledgerRepo = new FileLedgerRepository(session);
        var companyContext = new CompanyContext();
        var logger = NullLogger<CompanyService>.Instance;

        var systemConfig = new SystemConfiguration
        {
            CompanyDataPath = _tempTestDir
        };

        var service = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            logger,
            companySession: session,
            systemConfig: systemConfig);

        var companies = await service.GetAllCompaniesAsync();

        companies.Should().NotBeNull();
        companies.Should().BeEmpty("because no companies have been created, so no ghost (010000) or blank row should be returned");
    }

    [Fact]
    public async Task CreateCompanyAsync_PersistsDirectlyToConfiguredCompaniesFolder()
    {
        var storageEngine = new StorageEngine();
        var keyManager = new KeyManager();
        var session = new CompanySession(storageEngine, keyManager);
        var uow = new FileUnitOfWork(session);
        var companyRepo = new FileCompanyRepository(session);
        var fyRepo = new FileFinancialYearRepository(session);
        var groupRepo = new FileGroupRepository(session);
        var ledgerRepo = new FileLedgerRepository(session);
        var companyContext = new CompanyContext();
        var logger = NullLogger<CompanyService>.Instance;

        var companiesDir = Path.Combine(_tempTestDir, "Companies");
        var companyManager = new CompanyManager(_tempTestDir, storageEngine, keyManager);

        var systemConfig = new SystemConfiguration
        {
            CompanyDataPath = _tempTestDir,
            CurrencySymbol = "₹",
            CurrencyCode = "INR",
            Country = "India"
        };

        var service = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            logger,
            companySession: session,
            systemConfig: systemConfig,
            storageEngine: storageEngine,
            companyManager: companyManager);

        var dto = new CompanyCreateDto
        {
            CompanyName = "Tata Motors Ltd",
            Country = "India",
            Currency = "₹",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1)
        };

        var created = await service.CreateCompanyAsync(dto);

        created.Should().NotBeNull();
        created.CompanyName.Should().Be("Tata Motors Ltd");

        // Verify folder and company.data exist under _tempTestDir/Companies
        Directory.Exists(companiesDir).Should().BeTrue();
        var subDirs = Directory.GetDirectories(companiesDir);
        subDirs.Should().NotBeEmpty("company folder should have been created under Companies directory");

        var companyFolder = subDirs[0];
        var companyDataFile = Path.Combine(companyFolder, "company.data");
        File.Exists(companyDataFile).Should().BeTrue("company.data must be saved in the company folder on disk");
        new FileInfo(companyDataFile).Length.Should().BeGreaterThan(0);

        var companyJsonFile = Path.Combine(companyFolder, "company.json");
        File.Exists(companyJsonFile).Should().BeFalse("company.json must NOT be created; all storage is strictly company.data");

        // Verify discovery by GetAllCompaniesAsync
        var list = await service.GetAllCompaniesAsync();
        list.Should().HaveCount(1);
        list[0].CompanyName.Should().Be("Tata Motors Ltd");
        list[0].CompanyId.Should().Be(10001);
    }

    [Fact]
    public async Task GetAllCompaniesAsync_DiscoversExistingCompanyDataInFolder()
    {
        var storageEngine = new StorageEngine();
        var keyManager = new KeyManager();
        var session = new CompanySession(storageEngine, keyManager);
        var uow = new FileUnitOfWork(session);
        var companyRepo = new FileCompanyRepository(session);
        var fyRepo = new FileFinancialYearRepository(session);
        var groupRepo = new FileGroupRepository(session);
        var ledgerRepo = new FileLedgerRepository(session);
        var companyContext = new CompanyContext();
        var logger = NullLogger<CompanyService>.Instance;

        var systemConfig = new SystemConfiguration
        {
            CompanyDataPath = @"C:\Users\solan\MoneyFlow\Data"
        };

        var service = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            logger,
            companySession: session,
            systemConfig: systemConfig,
            storageEngine: storageEngine);

        var companies = await service.GetAllCompaniesAsync();

        companies.Should().NotBeNull();
        if (File.Exists(@"C:\Users\solan\MoneyFlow\Data\Companies\010001\company.data"))
        {
            companies.Should().NotBeEmpty();
            var comp = companies.FirstOrDefault(c => c.CompanyNumber == "010001");
            comp.Should().NotBeNull();
            comp!.CompanyNumber.Should().Be("010001");
            comp.CompanyName.Should().NotBeNullOrWhiteSpace();
            comp.FinancialYearFrom.Should().Be(new DateTime(2026, 4, 1));
        }


        // Must NEVER contain the dummy 010000 or blank row
        companies.Should().NotContain(c => c.CompanyName == "010000" || string.IsNullOrWhiteSpace(c.CompanyName));

        if (File.Exists(@"C:\Users\solan\MoneyFlow\Data\Companies\010001\company.data"))
        {
            bool opened = await service.OpenCompanyAsync(10001);
            opened.Should().BeTrue();
            File.Exists(@"C:\Users\solan\MoneyFlow\Data\Companies\010001\company.data").Should().BeTrue();
        }

    }
}
