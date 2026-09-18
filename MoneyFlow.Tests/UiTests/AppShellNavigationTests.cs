using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class AppShellNavigationTests
{
    private (MainForm Form, INavigationService NavService, ICompanyContext CompContext) CreateShell()
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
            options.UseInMemoryDatabase("AppShellTestDb_" + Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();
        var compContext = provider.GetRequiredService<ICompanyContext>();
        compContext.SetActiveCompany(new Company
        {
            CompanyId = 1,
            CompanyName = "Parth inc.",
            IsActive = true
        }, new FinancialYear
        {
            FinancialYearId = 1,
            CompanyId = 1,
            YearName = "2026-2027",
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2027, 3, 31)
        });

        var navService = provider.GetRequiredService<INavigationService>();
        var mainForm = provider.GetRequiredService<MainForm>();

        return (mainForm, navService, compContext);
    }

    [Fact]
    public void AppShell_FixedShellControls_AllPresentAndCorrectlyDocked()
    {
        // Arrange & Act
        var (form, _, _) = CreateShell();

        // Assert: Menu bar is removed as requested
        form.Controls.OfType<MenuStrip>().Should().BeEmpty("Menu bar has been removed as requested");
        
        // Assert: Controls docked to Top on MainForm must only be TitleBar (Toolbar is inside Gateway panel)
        var topControls = form.Controls.Cast<Control>().Where(c => c.Dock == DockStyle.Top).ToList();
        topControls.Should().HaveCount(1, "Only TitleBar is fixed at the top of MainForm; Toolbar buttons are inside the Gateway panel");

        // Assert: Dynamic content panel fills entire workspace below TitleBar down to window bottom
        var fillControls = form.Controls.Cast<Control>().Where(c => c.Dock == DockStyle.Fill).ToList();
        fillControls.Should().ContainSingle("Dynamic content panel must fill workspace");
        var mainContainer = fillControls[0];
        mainContainer.Padding.Should().Be(Padding.Empty, "Dynamic content panel must have 0 padding");

        form.IsOnGateway.Should().BeTrue();
        form.CurrentModuleKey.Should().Be("Gateway");
    }

    [Fact]
    public void AppShell_LoadModuleInWorkspace_ChildFormOccupiesEntireWorkspace_WithoutExternalHeaderOrFooter()
    {
        // Arrange
        var (form, navService, _) = CreateShell();
        var dummyForm = new Form { Text = "Dummy Ledger" };

        // Act: Open module in dynamicContentPanel
        form.ShowInWorkspace(() => dummyForm, "Ledgers", "Ledgers Master");

        // Assert: Child form is mounted inside mainContainer filling 100% of the space below the fixed toolbar
        var fillControls = form.Controls.Cast<Control>().Where(c => c.Dock == DockStyle.Fill).ToList();
        var mainContainer = fillControls[0];
        mainContainer.Controls.Cast<Control>().Should().Contain(dummyForm);
        dummyForm.Dock.Should().Be(DockStyle.Fill);

        // Assert: State is updated to module
        form.IsOnGateway.Should().BeFalse();
        form.CurrentModuleKey.Should().Be("Ledgers");

        // Act: Return to Gateway
        form.ReturnToGateway();
        form.IsOnGateway.Should().BeTrue();
        form.CurrentModuleKey.Should().Be("Gateway");
    }

    [Fact]
    public void AppShell_LoadModuleInWorkspace_ReplacesOnlyCentralContent_AndUpdatesState()
    {
        // Arrange
        var (form, navService, _) = CreateShell();
        var dummyForm = new Form { Text = "Dummy Payment" };

        // Act: Show dummy form in workspace
        form.ShowInWorkspace(() => dummyForm, "Payment", "Payment Voucher");

        // Assert: Workspace updated, Gateway is not active, module key updated
        form.IsOnGateway.Should().BeFalse();
        form.CurrentModuleKey.Should().Be("Payment");

        // Act: Return to Gateway
        form.ReturnToGateway();

        // Assert: Back on Gateway
        form.IsOnGateway.Should().BeTrue();
        form.CurrentModuleKey.Should().Be("Gateway");
    }

    [Fact]
    public void AppShell_NavigateBack_ClosesChildFormAndReturnsToGateway()
    {
        // Arrange
        var (form, navService, _) = CreateShell();
        var dummyForm = new Form { Text = "Dummy DayBook" };

        form.ShowInWorkspace(() => dummyForm, "DayBook", "Day Book");
        form.IsOnGateway.Should().BeFalse();

        // Act: Navigate back (as triggered by ESC)
        var handled = form.NavigateBack();

        // Assert
        handled.Should().BeTrue();
        form.IsOnGateway.Should().BeTrue();
        form.CurrentModuleKey.Should().Be("Gateway");
    }

    [Fact]
    public void AppShell_GatewayFooter_HasShortcuts_AndChildFormReplacesWorkspace()
    {
        // Arrange
        var (form, _, _) = CreateShell();

        // 1. Gateway Footer: contains F1-F9 ERP shortcuts
        form.CurrentFooterActions.Should().NotBeEmpty();
        form.CurrentFooterActions.Select(a => a.KeyText).Should().Contain(new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9" });

        // 2. Child module form replaces Gateway workspace completely
        var paymentForm = new Form { Text = "Payment Voucher" };
        form.ShowInWorkspace(() => paymentForm, "Payment", "Payment Voucher");
        form.IsOnGateway.Should().BeFalse();
        form.CurrentModuleKey.Should().Be("Payment");

        // 3. Return to Gateway restores Gateway workspace
        form.ReturnToGateway();
        form.IsOnGateway.Should().BeTrue();
        form.CurrentModuleKey.Should().Be("Gateway");
        form.CurrentFooterActions.Select(a => a.KeyText).Should().Contain(new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9" });
    }
}
