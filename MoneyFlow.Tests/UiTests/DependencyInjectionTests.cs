using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Desktop.Configuration;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class DependencyInjectionTests
{
    [Fact]
    public void ServiceCollectionExtensions_RegistersAllRequiredServicesAndResolvesMainForm()
    {
        // Arrange
        var services = new ServiceCollection();

        var configValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=MoneyFlowTestDB;Trusted_Connection=True;"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // Register using modular extension methods
        services.AddLogging();
        services.AddDatabaseServices(configuration);
        services.AddDataRepositories();
        services.AddDomainServices();
        services.AddNavigationServices();
        services.AddDesktopForms();

        // Override DbContext to use InMemory for testing without real SQL Server
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("DITestDb_" + Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        // Act & Assert: Verify Navigation & Factory resolution
        var formFactory = provider.GetService<IFormFactory>();
        formFactory.Should().NotBeNull();

        var navService = provider.GetService<INavigationService>();
        navService.Should().NotBeNull();

        // Act & Assert: Verify MainForm resolution with slimmed-down constructor
        var mainForm = provider.GetService<MainForm>();
        mainForm.Should().NotBeNull();
    }
}
