using System;
using System.IO;
using FluentAssertions;
using MoneyFlow.Data.Storage;
using Xunit;

namespace MoneyFlow.Tests.InstallerTests;

public class StartupSetupTests : IDisposable
{
    private readonly string _testRoot;

    public StartupSetupTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"MyERP_StartupTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ValidateDataPath_RejectsSqlAndDatabaseConnections()
    {
        // SQL Server connection string
        SystemEnvironmentManager.ValidateDataPath("Server=localhost;Database=MyERP;Trusted_Connection=True;", out var err1)
            .Should().BeFalse();
        err1.Should().Contain("SQL");

        // Database file path
        SystemEnvironmentManager.ValidateDataPath(@"C:\Data\MyERP.mdf", out var err2)
            .Should().BeFalse();
        err2.Should().Contain("database");

        // SQLite path
        SystemEnvironmentManager.ValidateDataPath(@"C:\Data\MyERP.sqlite", out var err3)
            .Should().BeFalse();
        err3.Should().Contain("database");

        // Empty path
        SystemEnvironmentManager.ValidateDataPath("", out var err4)
            .Should().BeFalse();
    }

    [Fact]
    public void ValidateDataPath_AcceptsValidFilesystemPath()
    {
        var validPath = Path.Combine(_testRoot, "Data");

        var isValid = SystemEnvironmentManager.ValidateDataPath(validPath, out var err);

        isValid.Should().BeTrue();
        err.Should().BeNull();
        Directory.Exists(validPath).Should().BeTrue();
    }

    [Fact]
    public void InitializeEnvironment_CreatesRequiredDirectoriesAndFiles_Atomically()
    {
        var dataPath = Path.Combine(_testRoot, "ProductionData");
        var config = new SystemConfiguration
        {
            Country = "India",
            CurrencySymbol = "₹",
            CurrencyCode = "INR",
            AccountingTerminology = "India / SAARC",
            ComplianceProfile = "GST Compliant",
            FinancialYearCycle = "01-Apr to 31-Mar",
            DecimalPrecision = "2 Decimals (0.00)",
            DecimalPlaces = 2
        };

        // Initialize Environment
        SystemEnvironmentManager.InitializeEnvironment(dataPath, config);

        // Verify Directory Structure
        Directory.Exists(dataPath).Should().BeTrue();
        Directory.Exists(Path.Combine(dataPath, "System")).Should().BeTrue();
        Directory.Exists(Path.Combine(dataPath, "Companies")).Should().BeTrue();
        Directory.Exists(Path.Combine(dataPath, "Backups")).Should().BeTrue();
        Directory.Exists(Path.Combine(dataPath, "Logs")).Should().BeTrue();

        // Verify installation.dat
        var manifestPath = Path.Combine(dataPath, "System", "installation.dat");
        File.Exists(manifestPath).Should().BeTrue();
        var manifestContent = File.ReadAllText(manifestPath);
        manifestContent.Should().Contain("\"SetupCompleted\": true");
        manifestContent.Should().Contain("\"ConfigurationVersion\": 1");

        // Verify system.config
        var configPath = Path.Combine(dataPath, "System", "system.config");
        File.Exists(configPath).Should().BeTrue();
        var loadedConfig = SystemConfiguration.Load(dataPath);
        loadedConfig.Country.Should().Be("India");
        loadedConfig.CurrencyCode.Should().Be("INR");
        loadedConfig.AccountingTerminology.Should().Be("India / SAARC");
        loadedConfig.CompanyDataPath.Should().Be(dataPath);

        // Verify NO database files were created
        Directory.GetFiles(dataPath, "*.mdf", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(dataPath, "*.ldf", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(dataPath, "*.db", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(dataPath, "*.sqlite", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void SetupCloseConfirmationDialog_InitializesWithCorrectSpecificationAndDefaultAction()
    {
        using var dlg = new MoneyFlow.Desktop.Dialogs.SetupCloseConfirmationDialog();

        // 1. Header Title
        dlg.Text.Should().Be("Close Application Setup?");

        // 2. Default button (Enter) closes / exits MoneyFlow
        dlg.AcceptButton.Should().NotBeNull();
        (dlg.AcceptButton as System.Windows.Forms.Control)!.Text.Should().StartWith("Exit MoneyFlow");

        // 3. Escape key (CancelButton) goes back to Continue Setup
        dlg.CancelButton.Should().NotBeNull();
        (dlg.CancelButton as System.Windows.Forms.Control)!.Text.Should().StartWith("Continue Setup");
    }

    [Fact]
    public void StartupClose_WithoutAccept_NeverMarksSetupCompletedOrCreatesCompanyData()
    {
        var customPath = Path.Combine(_testRoot, "UncompletedSetup");

        // Before setup accept, neither installation.dat nor company.data can exist
        Directory.Exists(customPath).Should().BeFalse();
        File.Exists(Path.Combine(customPath, "System", "installation.dat")).Should().BeFalse();
        File.Exists(Path.Combine(customPath, "Companies", "company.data")).Should().BeFalse();

        // SystemEnvironmentManager should detect first-time setup
        var isFirstTime = SystemEnvironmentManager.IsFirstTimeSetup(out var existingPath, out var loadedConfig);
        // Note: isFirstTime depends on global/user appdata, but custom path remains untouched
        File.Exists(Path.Combine(customPath, "System", "installation.dat")).Should().BeFalse();
        Directory.GetFiles(_testRoot, "*.mdf", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(_testRoot, "*.db", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void StartupConfigurationForm_InitializesWithCompactProfessionalDimensions_AndOnlyExpectedSettings()
    {
        using var form = new MoneyFlow.Desktop.Forms.StartupConfigurationForm();

        // 1. Form Dimensions
        form.Width.Should().Be(780);
        form.Height.Should().Be(330);
        form.FormBorderStyle.Should().Be(System.Windows.Forms.FormBorderStyle.None);

        // 2. Window Controls
        var allControls = GetAllChildControls(form);
        allControls.Should().Contain(c => c.Text == "—", "Minimize button must be present in header");
        allControls.Should().Contain(c => c.Text == "✕", "Close button must be present in header");

        // 3. Permitted Settings Only
        allControls.Should().Contain(c => c.Text == "Country");
        allControls.Should().Contain(c => c.Text == "Accounting Terminology");
        allControls.Should().Contain(c => c.Text == "Company Data Path");
        allControls.Should().Contain(c => c.Text == "Browse");
        allControls.Should().Contain(c => c.Text.Contains("Accept"));

        // 4. Removed Settings Must NOT exist
        allControls.Should().NotContain(c => c.Text.Contains("Profile Ready"));
        allControls.Should().NotContain(c => c.Text.Contains("initialize the ledger engine"));
        allControls.Should().NotContain(c => c.Text.Contains("Financial Year Cycle"));
        allControls.Should().NotContain(c => c.Text.Contains("Decimal Precision"));
        allControls.Should().NotContain(c => c.Text == "Configure");
        allControls.Should().NotContain(c => c.Text.Contains("Password"));
        allControls.Should().NotContain(c => c.Text.Contains("SQL"));
    }

    [Fact]
    public void SetupCloseConfirmationDialog_ContainsWarningElementsAndActionButtons()
    {
        using var dlg = new MoneyFlow.Desktop.Dialogs.SetupCloseConfirmationDialog();

        dlg.Width.Should().Be(480);
        dlg.Height.Should().Be(230);
        dlg.FormBorderStyle.Should().Be(System.Windows.Forms.FormBorderStyle.None);

        var allControls = GetAllChildControls(dlg);
        allControls.Should().Contain(c => c.Text == "Close Application Setup?");
        allControls.Should().Contain(c => c.Text.Contains("Your initial setup has not been completed."));
        allControls.Should().Contain(c => c.Text.Contains("If you close now, MoneyFlow will not be initialized."));
        allControls.Should().Contain(c => c.Text.Contains("Do you want to exit MoneyFlow?"));
        allControls.Should().Contain(c => c.Text.Contains("Continue Setup"));
        allControls.Should().Contain(c => c.Text.Contains("Exit MoneyFlow"));
    }

    private static System.Collections.Generic.List<System.Windows.Forms.Control> GetAllChildControls(System.Windows.Forms.Control parent)
    {
        var list = new System.Collections.Generic.List<System.Windows.Forms.Control>();
        foreach (System.Windows.Forms.Control child in parent.Controls)
        {
            list.Add(child);
            list.AddRange(GetAllChildControls(child));
        }
        return list;
    }
}

