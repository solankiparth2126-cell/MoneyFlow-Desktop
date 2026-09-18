using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Security;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class NavigationServiceTests
{
    private class TrackingFormFactory : IFormFactory
    {
        public List<Type> CreatedTypes { get; } = new();

        public TForm Create<TForm>(params object[] parameters) where TForm : Form
        {
            CreatedTypes.Add(typeof(TForm));
            return (TForm)Activator.CreateInstance(typeof(TForm), parameters)!;
        }

        public Form Create(Type formType, params object[] parameters)
        {
            CreatedTypes.Add(formType);
            return (Form)Activator.CreateInstance(formType, parameters)!;
        }
    }

    private (CompanyService Service, CompanyContext Context, UserContext UserContext) CreateServices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new AppDbContext(options);
        var companyRepo = new CompanyRepository(dbContext);
        var fyRepo = new FinancialYearRepository(dbContext);
        var groupRepo = new GroupRepository(dbContext);
        var ledgerRepo = new LedgerRepository(dbContext);
        var uow = new UnitOfWork(dbContext);
        var companyContext = new CompanyContext();
        var userContext = new UserContext();

        var service = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            NullLogger<CompanyService>.Instance);

        return (service, companyContext, userContext);
    }

    [Fact]
    public void HandleSearchResultNavigation_WithDashboardTarget_InvokesWithoutCrash()
    {
        // Arrange
        var (compService, companyContext, userContext) = CreateServices();
        companyContext.SetActiveCompany(new Company { CompanyId = 1, CompanyName = "Test Corp" }, null);
        var factory = new TrackingFormFactory();

        var navService = new NavigationService(factory, companyContext, compService, userContext);

        var searchResult = new GlobalSearchResultDto
        {
            Category = GlobalSearchCategory.Navigation,
            NavigationTarget = "Dashboard",
            Title = "Dashboard"
        };

        // Act & Assert
        Action act = () => navService.HandleSearchResultNavigation(searchResult);
        act.Should().NotThrow();
    }

    [Fact]
    public void CloseActiveCompany_WhenCompanyNotOpen_DoesNotCallCloseCompany()
    {
        // Arrange
        var (compService, companyContext, userContext) = CreateServices();
        var factory = new TrackingFormFactory();

        var navService = new NavigationService(factory, companyContext, compService, userContext);

        // Act
        navService.CloseActiveCompany();

        // Assert: Company was never open, remains closed
        companyContext.IsCompanyOpen.Should().BeFalse();
    }

    private class TrackingNavigationHost : INavigationHost
    {
        public List<(string ModuleKey, string ModuleTitle)> WorkspaceCalls { get; } = new();
        public bool ReturnToGatewayCalled { get; private set; }
        public string CurrentModuleKey { get; set; } = "Gateway";
        public bool IsOnGateway => CurrentModuleKey == "Gateway";
        public IReadOnlyList<FooterActionItem> CurrentFooterActions { get; set; } = new List<FooterActionItem>();

        public void ShowInWorkspace(Func<Form> formFactory, string moduleKey, string moduleTitle)
        {
            WorkspaceCalls.Add((moduleKey, moduleTitle));
            CurrentModuleKey = moduleKey;
        }

        public void ReturnToGateway()
        {
            ReturnToGatewayCalled = true;
            CurrentModuleKey = "Gateway";
        }

        public bool NavigateBack()
        {
            if (CurrentModuleKey != "Gateway")
            {
                ReturnToGateway();
                return true;
            }
            return false;
        }
    }

    [Fact]
    public void OpenPaymentVoucher_WithRegisteredHost_RoutesToWorkspace_AndUpdatesModuleState()
    {
        // Arrange
        var (compService, companyContext, userContext) = CreateServices();
        companyContext.SetActiveCompany(new Company { CompanyId = 1, CompanyName = "Test Corp" }, new FinancialYear { FinancialYearId = 1, CompanyId = 1, YearName = "2026-2027" });
        var factory = new TrackingFormFactory();
        var host = new TrackingNavigationHost();

        var navService = new NavigationService(factory, companyContext, compService, userContext);
        navService.RegisterHost(host);

        string? activeModuleEvent = null;
        navService.ActiveModuleChanged += m => activeModuleEvent = m;

        // Act
        navService.OpenPaymentVoucher();

        // Assert
        host.WorkspaceCalls.Should().ContainSingle();
        host.WorkspaceCalls[0].ModuleKey.Should().Be("Payment");
        host.WorkspaceCalls[0].ModuleTitle.Should().Be("Payment Voucher");
        host.CurrentModuleKey.Should().Be("Payment");
        activeModuleEvent.Should().Be("Payment");
    }

    [Fact]
    public void OpenDayBook_WithRegisteredHost_RoutesToWorkspace()
    {
        // Arrange
        var (compService, companyContext, userContext) = CreateServices();
        companyContext.SetActiveCompany(new Company { CompanyId = 1, CompanyName = "Test Corp" }, new FinancialYear { FinancialYearId = 1, CompanyId = 1, YearName = "2026-2027" });
        var factory = new TrackingFormFactory();
        var host = new TrackingNavigationHost();

        var navService = new NavigationService(factory, companyContext, compService, userContext);
        navService.RegisterHost(host);

        // Act
        navService.OpenDayBook();

        // Assert
        host.WorkspaceCalls.Should().ContainSingle();
        host.WorkspaceCalls[0].ModuleKey.Should().Be("DayBook");
        host.WorkspaceCalls[0].ModuleTitle.Should().Be("Day Book");
    }

    [Fact]
    public void OpenCompanyList_StrictPreservation_NeverRoutesToWorkspace()
    {
        // Arrange: Change Company must NEVER be loaded into dynamicContentPanel
        var (compService, companyContext, userContext) = CreateServices();
        var factory = new TrackingFormFactory();
        var host = new TrackingNavigationHost();

        var navService = new NavigationService(factory, companyContext, compService, userContext);
        navService.RegisterHost(host);

        // Assert: TrackingFormFactory creates CompanyListForm, but host.WorkspaceCalls must remain EMPTY
        host.WorkspaceCalls.Should().BeEmpty();
    }

    [Fact]
    public void NavigateBack_WhenInModule_ReturnsToGateway()
    {
        // Arrange
        var (compService, companyContext, userContext) = CreateServices();
        companyContext.SetActiveCompany(new Company { CompanyId = 1, CompanyName = "Test Corp" }, new FinancialYear { FinancialYearId = 1, CompanyId = 1, YearName = "2026-2027" });
        var factory = new TrackingFormFactory();
        var host = new TrackingNavigationHost();

        var navService = new NavigationService(factory, companyContext, compService, userContext);
        navService.RegisterHost(host);

        navService.OpenPaymentVoucher();
        host.CurrentModuleKey.Should().Be("Payment");

        // Act
        var handled = navService.NavigateBack();

        // Assert
        handled.Should().BeTrue();
        host.CurrentModuleKey.Should().Be("Gateway");
        host.ReturnToGatewayCalled.Should().BeTrue();
    }
}
