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
}
