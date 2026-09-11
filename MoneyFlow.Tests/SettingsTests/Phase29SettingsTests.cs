using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Data;
using MoneyFlow.Services.Settings;
using Xunit;

namespace MoneyFlow.Tests.SettingsTests;

public class Phase29SettingsTests
{
    private (AppDbContext Context, SettingsService SettingsSvc) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var settingsService = new SettingsService(context);

        return (context, settingsService);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultSettings_WhenTableIsEmpty()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();

        // Act
        var settings = await settingsSvc.GetSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.DefaultCompanyId.Should().BeNull();
        settings.VoucherLockDate.Should().BeNull();
        settings.DateFormat.Should().Be("dd-MM-yyyy");
        settings.NumberFormat.Should().Be("Indian");
        settings.CurrencySymbol.Should().Be("₹");
        settings.DecimalPrecision.Should().Be(2);
        settings.Theme.Should().Be("ClassicTeal");
        settings.GridDensity.Should().Be("Compact");
        settings.DefaultBackupPath.Should().Contain("MoneyFlow");
        settings.PromptBackupOnExit.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsAllSettings_AndReloadsCorrectly()
    {
        // Arrange
        var (context, settingsSvc) = CreateTestSetup();
        var customSettings = new ApplicationSettingsDto
        {
            DefaultCompanyId = 42,
            VoucherLockDate = new DateTime(2026, 3, 31),
            AutoRoundOffVouchers = true,
            PrintVoucherAfterSave = true,
            DefaultBackupPath = @"D:\AccountingBackups",
            PromptBackupOnExit = false,
            DefaultPrinterName = "HP LaserJet Pro",
            PaperSize = "Letter",
            DirectPrintWithoutPreview = true,
            DateFormat = "yyyy-MM-dd",
            NumberFormat = "Western",
            DecimalPrecision = 3,
            CurrencySymbol = "$",
            Theme = "DarkSlate",
            GridDensity = "Comfortable"
        };

        // Act
        await settingsSvc.SaveSettingsAsync(customSettings);
        var reloaded = await settingsSvc.GetSettingsAsync();

        // Assert
        reloaded.DefaultCompanyId.Should().Be(42);
        reloaded.VoucherLockDate.Should().Be(new DateTime(2026, 3, 31));
        reloaded.AutoRoundOffVouchers.Should().BeTrue();
        reloaded.PrintVoucherAfterSave.Should().BeTrue();
        reloaded.DefaultBackupPath.Should().Be(@"D:\AccountingBackups");
        reloaded.PromptBackupOnExit.Should().BeFalse();
        reloaded.DefaultPrinterName.Should().Be("HP LaserJet Pro");
        reloaded.PaperSize.Should().Be("Letter");
        reloaded.DirectPrintWithoutPreview.Should().BeTrue();
        reloaded.DateFormat.Should().Be("yyyy-MM-dd");
        reloaded.NumberFormat.Should().Be("Western");
        reloaded.DecimalPrecision.Should().Be(3);
        reloaded.CurrencySymbol.Should().Be("$");
        reloaded.Theme.Should().Be("DarkSlate");
        reloaded.GridDensity.Should().Be("Comfortable");
    }

    [Fact]
    public async Task GetSettingValueAsync_And_SetSettingValueAsync_WorkIndividually()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();

        // Act
        var initial = await settingsSvc.GetSettingValueAsync("Custom.TaxMode", "Standard");
        initial.Should().Be("Standard");

        await settingsSvc.SetSettingValueAsync("Custom.TaxMode", "Exempt", "Custom tax category");
        var updated = await settingsSvc.GetSettingValueAsync("Custom.TaxMode");

        // Assert
        updated.Should().Be("Exempt");
    }

    [Fact]
    public async Task IsDateLockedAsync_ReturnsTrue_WhenDateOnOrBeforeLockDate()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto
        {
            VoucherLockDate = new DateTime(2026, 3, 31)
        });

        // Act & Assert
        (await settingsSvc.IsDateLockedAsync(new DateTime(2026, 3, 31))).Should().BeTrue();
        (await settingsSvc.IsDateLockedAsync(new DateTime(2026, 1, 15))).Should().BeTrue();
        (await settingsSvc.IsDateLockedAsync(new DateTime(2026, 4, 1))).Should().BeFalse();
        (await settingsSvc.IsDateLockedAsync(new DateTime(2026, 6, 30))).Should().BeFalse();
    }

    [Fact]
    public async Task IsDateLockedAsync_ReturnsFalse_WhenNoLockDateConfigured()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();

        // Act & Assert
        (await settingsSvc.IsDateLockedAsync(new DateTime(2025, 1, 1))).Should().BeFalse();
        (await settingsSvc.IsDateLockedAsync(new DateTime(2026, 4, 1))).Should().BeFalse();
    }

    [Fact]
    public async Task FormatDate_UsesConfiguredDateFormat()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();
        var date = new DateTime(2026, 9, 11);

        // Act & Assert default dd-MM-yyyy
        await settingsSvc.GetSettingsAsync();
        settingsSvc.FormatDate(date).Should().Be("11-09-2026");

        // Change format to yyyy-MM-dd
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto { DateFormat = "yyyy-MM-dd" });
        settingsSvc.FormatDate(date).Should().Be("2026-09-11");
    }

    [Fact]
    public async Task FormatCurrency_IndianGrouping_FormatsWithLakhsAndCrores()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto
        {
            NumberFormat = "Indian",
            CurrencySymbol = "₹",
            DecimalPrecision = 2
        });

        // Act
        var formatted = settingsSvc.FormatCurrency(1234567.89m);

        // Assert
        formatted.Should().Be("₹ 12,34,567.89");
    }

    [Fact]
    public async Task FormatCurrency_WesternGrouping_FormatsWithMillions()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto
        {
            NumberFormat = "Western",
            CurrencySymbol = "₹",
            DecimalPrecision = 2
        });

        // Act
        var formatted = settingsSvc.FormatCurrency(1234567.89m);

        // Assert
        formatted.Should().Be("₹ 1,234,567.89");
    }

    [Fact]
    public async Task FormatCurrency_HandlesNegativeAmounts_WithSymbol()
    {
        // Arrange
        var (_, settingsSvc) = CreateTestSetup();
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto
        {
            NumberFormat = "Indian",
            CurrencySymbol = "₹",
            DecimalPrecision = 2
        });

        // Act
        var formatted = settingsSvc.FormatCurrency(-50000.50m);

        // Assert
        formatted.Should().Be("-₹ 50,000.50");
    }

    [Fact]
    public async Task SaveSettingsAsync_UpdatesExistingKeys_WithoutDuplicates()
    {
        // Arrange
        var (context, settingsSvc) = CreateTestSetup();

        // Act
        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto { DecimalPrecision = 2 });
        var countFirst = await context.Settings.CountAsync();

        await settingsSvc.SaveSettingsAsync(new ApplicationSettingsDto { DecimalPrecision = 4 });
        var countSecond = await context.Settings.CountAsync();

        var reloaded = await settingsSvc.GetSettingsAsync();

        // Assert
        countSecond.Should().Be(countFirst);
        reloaded.DecimalPrecision.Should().Be(4);
    }
}
