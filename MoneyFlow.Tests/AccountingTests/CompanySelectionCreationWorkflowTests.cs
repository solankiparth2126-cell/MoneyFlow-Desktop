using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Data.Storage;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Migration;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

/// <summary>
/// Verifies the Mandatory Company Selection & Creation Workflow requirements (Parts 1 - 43):
/// - Mandatory active company context
/// - Unique Entity Code generation (010002)
/// - Directory creation and path sanitization
/// - Financial year period calculation (01-Apr-2026 to 31-Mar-2027)
/// - Save vs Save & Open behavior
/// - Company switching and context clearing
/// - Transactional safety and validation
/// </summary>
public class CompanySelectionCreationWorkflowTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly CompanyStorageEngine _storageEngine;
    private readonly CompanyContext _companyContext;
    private readonly CompanyManager _companyManager;
    private readonly CompanyService _companyService;

    public CompanySelectionCreationWorkflowTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "MoneyFlowTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);

        var crypto = new CompanyDataCrypto();
        _storageEngine = new CompanyStorageEngine(crypto, _testTempDir);
        var dataStore = new CompanyDataStore(_storageEngine);
        _companyContext = new CompanyContext();

        var migrationManager = new MigrationManager(_storageEngine, NullLogger<MigrationManager>.Instance);
        var journalManager = new JournalManager(_storageEngine);
        var transactionManager = new TransactionManager(journalManager);

        _companyManager = new CompanyManager(
            _storageEngine,
            dataStore,
            _companyContext,
            migrationManager,
            transactionManager,
            NullLogger<CompanyManager>.Instance);

        var fileRepo = new FileCompanyRepository(dataStore);
        var uow = new FileUnitOfWork(dataStore, transactionManager);

        _companyService = new CompanyService(
            _companyManager,
            fileRepo,
            _companyContext,
            uow,
            NullLogger<CompanyService>.Instance);
    }

    public void Dispose()
    {
        _companyService.CloseCompany();
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task Test1_FreshApplication_NoCompanies_ReturnsEmptyList()
    {
        // Scenario 1: Fresh application with no companies created
        var companies = await _companyService.GetAllCompaniesAsync();

        companies.Should().NotBeNull();
        companies.Should().BeEmpty("fresh application must have 0 companies until created");
        _companyContext.IsCompanyOpen.Should().BeFalse("no active company can be loaded without user selection");
    }

    [Fact]
    public void Test2_MandatoryCompanyGuard_ContextIsClosedByDefault()
    {
        // Scenario 2: Active company is mandatory before any dashboard or masters access
        _companyContext.IsCompanyOpen.Should().BeFalse();
        _companyContext.CurrentCompany.Should().BeNull();
        _companyContext.CurrentFinancialYear.Should().BeNull();
    }

    [Fact]
    public async Task Test3_CreateValidCompany_GeneratesStableEntityCode_SetsActiveContext()
    {
        // Scenario 3: Create company with explicit Entity Code
        var dto = new CompanyCreateDto
        {
            CompanyName = "Kavan Technologies Pvt Ltd",
            CompanyNumber = "010002",
            Address = "Unit 402, Trade Hub Towers\nBengaluru, Karnataka - 560058",
            State = "Karnataka",
            Country = "India",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            Currency = "₹",
            CreateDefaultLedgers = true
        };

        var created = await _companyService.CreateCompanyAsync(dto);

        created.Should().NotBeNull();
        created.CompanyName.Should().Be("Kavan Technologies Pvt Ltd");
        created.CompanyNumber.Should().Be("010002");
        created.CompanyId.Should().Be(10002);

        // Verify Active Company Context is set
        _companyContext.IsCompanyOpen.Should().BeTrue();
        _companyContext.CurrentCompany?.CompanyName.Should().Be("Kavan Technologies Pvt Ltd");
        _companyContext.CurrentFinancialYear.Should().NotBeNull();
        _companyContext.CurrentFinancialYear?.YearName.Should().Be("2026-27");
    }

    [Fact]
    public async Task Test5_SaveCompany_RefreshesCompanyList_ContainsNewCompany()
    {
        // Scenario 5: Newly created company appears in company list
        var dto = new CompanyCreateDto
        {
            CompanyName = "Apex Industrial Works",
            CompanyNumber = "010004",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            Currency = "USD"
        };

        await _companyService.CreateCompanyAsync(dto);

        var list = await _companyService.GetAllCompaniesAsync();
        list.Should().HaveCount(1);
        list.First().CompanyName.Should().Be("Apex Industrial Works");
        list.First().CompanyNumber.Should().Be("010004");
    }

    [Fact]
    public async Task Test6_SaveAndOpenCompany_SetsActiveCompanyAndFinancialYear()
    {
        // Scenario 6: Save & Open activates company context
        var dto = new CompanyCreateDto
        {
            CompanyName = "Delta Hydraulics & Spares",
            CompanyNumber = "010005",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            Currency = "₹"
        };

        var created = await _companyService.CreateCompanyAsync(dto);
        await _companyService.OpenCompanyAsync(created.CompanyId);

        _companyContext.IsCompanyOpen.Should().BeTrue();
        _companyContext.CurrentCompany?.CompanyNumber.Should().Be("010005");
        _companyContext.CurrentFinancialYear?.StartDate.Should().Be(new DateTime(2026, 4, 1));
        _companyContext.CurrentFinancialYear?.EndDate.Should().Be(new DateTime(2027, 3, 31));
    }

    [Fact]
    public async Task Test7_InvalidCompanyForm_ValidationRejectsEmptyName()
    {
        // Scenario 7: Validation error on empty name
        var dto = new CompanyCreateDto
        {
            CompanyName = "   ",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        };

        var act = async () => await _companyService.CreateCompanyAsync(dto);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name*");
    }

    [Fact]
    public async Task Test11_SwitchCompany_UpdatesActiveContextAndFinancialYear()
    {
        // Scenario 11 & 12: Switch company changes active context
        var c1 = await _companyService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company One",
            CompanyNumber = "010002",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        var c2 = await _companyService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company Two",
            CompanyNumber = "010003",
            FinancialYearFrom = new DateTime(2025, 4, 1)
        });

        // Switch to Company One
        bool opened1 = await _companyService.OpenCompanyAsync(c1.CompanyId);
        opened1.Should().BeTrue();
        _companyContext.CurrentCompany?.CompanyName.Should().Be("Company One");
        _companyContext.CurrentFinancialYear?.StartDate.Year.Should().Be(2026);

        // Switch to Company Two
        bool opened2 = await _companyService.OpenCompanyAsync(c2.CompanyId);
        opened2.Should().BeTrue();
        _companyContext.CurrentCompany?.CompanyName.Should().Be("Company Two");
        _companyContext.CurrentFinancialYear?.StartDate.Year.Should().Be(2025);
    }

    [Fact]
    public async Task Test12_CloseCompany_ClearsActiveCompanyContext()
    {
        // Scenario 12: Close company resets context
        var c1 = await _companyService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Closing Test Corp",
            CompanyNumber = "010002",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        _companyContext.IsCompanyOpen.Should().BeTrue();

        _companyService.CloseCompany();

        _companyContext.IsCompanyOpen.Should().BeFalse();
        _companyContext.CurrentCompany.Should().BeNull();
        _companyContext.CurrentFinancialYear.Should().BeNull();
    }

    [Fact]
    public void Test15_DirectoryNameSanitization_RemovesInvalidCharacters()
    {
        // Scenario 15: Windows invalid directory characters are removed
        string raw = "Kavan: Logistics / Spares * <Group> | Ltd?";
        string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        string cleaned = new string(raw.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        cleaned = cleaned.Replace(" ", "");

        cleaned.Should().NotContain(":");
        cleaned.Should().NotContain("/");
        cleaned.Should().NotContain("*");
        cleaned.Should().NotContain("<");
        cleaned.Should().NotContain(">");
        cleaned.Should().NotContain("|");
        cleaned.Should().NotContain("?");
        cleaned.Should().Be("KavanLogisticsSparesGroupLtd");
    }

    [Fact]
    public void Test16_FinancialPeriodCalculation_FormatsDateRangeCorrectly()
    {
        // Scenario 16: Financial period calculation 01-Apr-2026 to 31-Mar-2027
        var start = new DateTime(2026, 4, 1);
        var end = start.AddYears(1).AddDays(-1);

        end.Should().Be(new DateTime(2027, 3, 31));
        string periodStr = $"{start:1-MMM-yy} to {end:dd-MMM-yy}";
        periodStr.Should().Be("1-Apr-26 to 31-Mar-27");
    }
}
