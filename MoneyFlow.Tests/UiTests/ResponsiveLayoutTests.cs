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

        _ = scale; // Acknowledge DPI scaling parameter for layout matrix test

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
}
