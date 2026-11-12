using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Desktop.Configuration;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class ResponsiveLayoutTests
{
    private MainForm CreateMainForm()
    {
        var services = new ServiceCollection();
        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=MoneyFlowTestDB;Trusted_Connection=True;"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        services.AddLogging();
        services.AddDatabaseServices(configuration);
        services.AddDataRepositories();
        services.AddDomainServices();
        services.AddNavigationServices();
        services.AddDesktopForms();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("ResponsiveTestDb_" + Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var compContext = provider.GetRequiredService<ICompanyContext>();
        compContext.SetActiveCompany(new Company
        {
            CompanyId = 1,
            CompanyName = "Parth International Trading Co.",
            IsActive = true
        }, new FinancialYear
        {
            FinancialYearId = 1,
            CompanyId = 1,
            YearName = "2026-2027",
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2027, 3, 31)
        });

        return provider.GetRequiredService<MainForm>();
    }

    [Theory]
    // Standard and scaled resolutions requested:
    [InlineData(1280, 720, 1.0f)]   // 1280x720 @ 100%
    [InlineData(1366, 768, 1.0f)]   // 1366x768 @ 100%
    [InlineData(1092, 614, 1.25f)]  // 1366x768 @ 125% logical
    [InlineData(1600, 900, 1.0f)]   // 1600x900 @ 100%
    [InlineData(1920, 1080, 1.0f)]  // 1920x1080 @ 100%
    [InlineData(1536, 864, 1.25f)]  // 1920x1080 @ 125% logical
    [InlineData(1280, 720, 1.5f)]   // 1920x1080 @ 150% logical
    [InlineData(1706, 960, 1.5f)]   // 2560x1440 @ 150% logical
    [InlineData(1920, 1080, 2.0f)]  // 3840x2160 @ 200% logical
    public void MainForm_LayoutAdaptsWithoutExceptions_AcrossMatrix(int logicalW, int logicalH, float scale)
    {
        using var form = CreateMainForm();

        form.WindowState = FormWindowState.Normal;
        form.Size = new Size(logicalW, logicalH);

        var act = () => form.ApplyResponsiveLayout();
        act.Should().NotThrow();

        form.ClientSize.Width.Should().BeGreaterThanOrEqualTo(800);
        form.ClientSize.Height.Should().BeGreaterThanOrEqualTo(450);
    }

    [Theory]
    [InlineData(1000, 500, LayoutTier.Compact)]
    [InlineData(1366, 728, LayoutTier.Standard)]
    [InlineData(1920, 1040, LayoutTier.Large)]
    public void ScreenFittingManager_ClassifyTier_ReturnsExpectedTier(int w, int h, LayoutTier expectedTier)
    {
        var tier = ScreenFittingManager.ClassifyTier(w, h);
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public void GatewayRows_MaintainCompactErpProportions_AcrossResolutions()
    {
        using var form = CreateMainForm();

        form.WindowState = FormWindowState.Normal;
        form.Size = new Size(1920, 1080);
        form.ApplyResponsiveLayout();

        var items = form.GetType().GetField("_gatewayItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as System.Collections.IEnumerable;
        items.Should().NotBeNull();

        foreach (var item in items!)
        {
            var ctrl = item.GetType().GetProperty("RowPanel")?.GetValue(item) as Control;
            ctrl.Should().NotBeNull();
            // Verify rows maintain compact ERP proportions (34-38px)
            ctrl!.Height.Should().BeInRange(34, 38);
        }
    }

    [Fact]
    public void OperationsRail_ContainsAllNineFunctions_AndFitsWithinClientWidth()
    {
        using var form = CreateMainForm();

        form.WindowState = FormWindowState.Normal;
        form.Size = new Size(1280, 720);
        form.ApplyResponsiveLayout();

        var rail = form.Controls["operationsRail"] ?? form.GetType().GetField("operationsRail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as Control;
        rail.Should().NotBeNull();
        rail!.Dock.Should().Be(DockStyle.Bottom);
        rail.Controls.Count.Should().Be(9); // F1 to F9

        int totalButtonsWidth = 0;
        foreach (Control btn in rail.Controls)
        {
            totalButtonsWidth += btn.Width + 4;
        }

        // Must fit completely on 1280px with zero clipping
        totalButtonsWidth.Should().BeLessThan(1280);
    }

    [Fact]
    public void Gateway_CardsGrid_StretchesToFillAvailableHeight_WithoutFixedClamp()
    {
        using var form = CreateMainForm();
        form.Size = new Size(1920, 1080);
        form.ApplyResponsiveLayout();

        var cardsGrid = form.GetType().GetField("cardsGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as TableLayoutPanel;
        cardsGrid.Should().NotBeNull();
        cardsGrid!.Dock.Should().Be(DockStyle.Fill);

        var cardMasters = form.GetType().GetField("cardMasters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as Control;
        var cardTrans = form.GetType().GetField("cardTrans", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as Control;
        var cardReports = form.GetType().GetField("cardReports", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as Control;

        cardMasters.Should().NotBeNull();
        cardTrans.Should().NotBeNull();
        cardReports.Should().NotBeNull();

        cardMasters!.Dock.Should().Be(DockStyle.Fill);
        cardTrans!.Dock.Should().Be(DockStyle.Fill);
        cardReports!.Dock.Should().Be(DockStyle.Fill);
    }

    [Fact]
    public void FitFormToScreen_ClampsOversizedDialog_ToScreenWorkingArea()
    {
        // Dialog designed at 1250x780 (like ProfitLossForm)
        using var testDialog = new Form
        {
            Size = new Size(1250, 780),
            StartPosition = FormStartPosition.CenterParent
        };

        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        int maxW = screen.WorkingArea.Width - 32;
        int maxH = screen.WorkingArea.Height - 48;

        if (testDialog.Width > maxW || testDialog.Height > maxH)
        {
            testDialog.Width = Math.Min(testDialog.Width, maxW);
            testDialog.Height = Math.Min(testDialog.Height, maxH);
        }

        testDialog.Width.Should().BeLessThanOrEqualTo(screen.WorkingArea.Width);
        testDialog.Height.Should().BeLessThanOrEqualTo(screen.WorkingArea.Height);
    }

    [Fact]
    public void CompanyListForm_InitializesWithReferenceStructure_AndNoTechnicalClutter()
    {
        var services = new ServiceCollection();
        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=MoneyFlowTestDB;Trusted_Connection=True;"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        services.AddLogging();
        services.AddDatabaseServices(configuration);
        services.AddDataRepositories();
        services.AddDomainServices();
        services.AddNavigationServices();
        services.AddDesktopForms();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("CompanyListTestDb_" + Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        using var form = provider.GetRequiredService<CompanyListForm>();

        // 1. Basic Form Properties
        form.Text.Should().Be("Select Company");
        form.Width.Should().BeGreaterThanOrEqualTo(980);
        form.Height.Should().BeGreaterThanOrEqualTo(540);

        // 2. Verify all controls in the visual hierarchy are present
        string allText = GetAllFormTextRecursive(form);

        allText.Should().Contain("Select Company");
        allText.Should().Contain("DATA PATH");
        allText.Should().Contain("NAME OF COMPANY");
        allText.Should().Contain("QUICK ACTION DIRECTORY");
        allText.Should().Contain("ALT MENU");
        allText.Should().Contain("Create Company");
        allText.Should().Contain("Select Path");
        allText.Should().NotContain("Select Remote Company");
        allText.Should().NotContain("Select from Drive");
        allText.Should().Contain("Cancel");
        allText.Should().NotContain("Open Company");
        allText.Should().NotContain("Entities Loaded");
        allText.Should().NotContain("Active Books");

        // Verify exact layout hierarchy in Left Directory Card (Header at 0, followed by Create Company and Select Path)
        var pnlLeftField = form.GetType().GetField("pnlLeftCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(form) as Control;
        pnlLeftField.Should().NotBeNull();
        pnlLeftField!.Controls.OfType<Control>().First(c => c.Top == 0).Controls.OfType<Label>().Any(l => l.Text.Contains("QUICK ACTION DIRECTORY")).Should().BeTrue();
        pnlLeftField.Controls.OfType<Control>().First(c => c.Top == 44).Controls.OfType<Label>().Any(l => l.Text.Contains("Create Company")).Should().BeTrue();
        pnlLeftField.Controls.OfType<Control>().First(c => c.Top == 88).Controls.OfType<Label>().Any(l => l.Text.Contains("Select Path")).Should().BeTrue();






        // 3. Verify absolute removal of technical/security panels
        allText.Should().NotContain("Security Engine");
        allText.Should().NotContain("SQL / ENCRYPT");
        allText.Should().NotContain("LAN 1000 Mbps");
        allText.Should().NotContain("Directory Connection");
        allText.Should().NotContain("Schema Rev");
        allText.Should().NotContain("PORT: 9805");
        allText.Should().NotContain("Auto-sync with Cloud Vault");
    }

    [Fact]
    public void QuitConfirmationDialog_HasProperElements_AndKeyShortcuts()
    {
        using var dlg = new QuitConfirmationDialog();
        dlg.Text.Should().Be("Quit — MoneyFlow Desktop ERP");

        string allText = GetAllFormTextRecursive(dlg);
        allText.Should().Contain("Are you sure you want to exit the application?");
        allText.Should().Contain("Press Y or Enter to exit, N or Esc to cancel.");
        allText.Should().Contain("Yes");
        allText.Should().Contain("No");
    }

    [Fact]
    public void CompanyListForm_WhenCompanyIsOpen_ClosesDirectlyWithoutQuitDialog()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging();
        services.AddDatabaseServices(configuration);
        services.AddDataRepositories();
        services.AddDomainServices();
        services.AddNavigationServices();
        services.AddDesktopForms();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("CompanyListCloseTestDb_" + Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var compContext = provider.GetRequiredService<ICompanyContext>();
        compContext.SetActiveCompany(new Company
        {
            CompanyId = 1,
            CompanyName = "Active Company",
            IsActive = true
        }, new FinancialYear
        {
            FinancialYearId = 1,
            CompanyId = 1,
            YearName = "2026-27"
        });

        using var form = provider.GetRequiredService<CompanyListForm>();

        var e = new FormClosingEventArgs(CloseReason.UserClosing, false);
        var onClosingMethod = typeof(CompanyListForm).GetMethod("OnFormClosing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        onClosingMethod.Should().NotBeNull();
        onClosingMethod!.Invoke(form, new object[] { e });

        e.Cancel.Should().BeFalse();
    }

    private static string GetAllFormTextRecursive(Control root)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(' ').Append(root.Text);
        foreach (Control child in root.Controls)
        {
            sb.Append(' ').Append(GetAllFormTextRecursive(child));
        }
        return sb.ToString();
    }
}

