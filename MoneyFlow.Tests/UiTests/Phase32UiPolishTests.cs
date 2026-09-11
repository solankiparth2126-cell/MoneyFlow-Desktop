using System;
using System.Collections.Generic;
using System.Drawing;
using FluentAssertions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Styling;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class Phase32UiPolishTests
{
    [Fact]
    public void ThemeManager_Themes_ConfigureValidDistinctPalettes()
    {
        // 1. Classic Teal Theme
        ThemeManager.SetTheme("ClassicTeal");
        ThemeManager.CurrentTheme.Should().Be(AppTheme.ClassicTeal);
        var tealColors = ThemeManager.Colors;
        tealColors.HeaderBg.Should().NotBe(Color.Empty);
        tealColors.HeaderFg.Should().Be(Color.White);
        tealColors.SuccessFg.Should().NotBe(tealColors.DangerFg);

        // 2. Dark Slate Theme
        ThemeManager.SetTheme("DarkSlate");
        ThemeManager.CurrentTheme.Should().Be(AppTheme.DarkSlate);
        var darkColors = ThemeManager.Colors;
        darkColors.HeaderBg.Should().NotBe(tealColors.HeaderBg);
        darkColors.WindowBg.Should().Be(Color.FromArgb(30, 41, 59));

        // 3. Light Neutral Theme
        ThemeManager.SetTheme("LightNeutral");
        ThemeManager.CurrentTheme.Should().Be(AppTheme.LightNeutral);
        var lightColors = ThemeManager.Colors;
        lightColors.HeaderBg.Should().NotBe(darkColors.HeaderBg);
        lightColors.WindowBg.Should().Be(Color.FromArgb(248, 250, 252));
    }

    [Theory]
    [InlineData("Compact")]
    [InlineData("Comfortable")]
    public void ThemeManager_DensitySettings_ArePersisted(string density)
    {
        // Act
        ThemeManager.SetTheme("ClassicTeal", density);

        // Assert
        ThemeManager.CurrentDensity.Should().Be(density);
    }

    [Fact]
    public void VoucherEntryControl_InitialState_IsNotBalancedWithZeroTotals()
    {
        // Arrange & Act
        using var control = new VoucherEntryControl();

        // Assert
        control.TotalDebit.Should().Be(0m);
        control.TotalCredit.Should().Be(0m);
        control.Difference.Should().Be(0m);
        control.IsBalanced.Should().BeFalse("Zero totals must not be marked balanced");
    }

    [Fact]
    public void VoucherEntryControl_GetVoucherData_ExtractsValidDoubleEntryPayload()
    {
        // Arrange
        using var control = new VoucherEntryControl();
        var types = new List<VoucherType>
        {
            new() { VoucherTypeId = 1, Name = "Payment", Code = "PMT", IsActive = true }
        };
        var ledgers = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 10, LedgerName = "Cash", IsActive = true },
            new() { LedgerId = 20, LedgerName = "Rent Expense", IsActive = true }
        };

        control.SetVoucherTypes(types, 1);
        control.SetLedgers(ledgers);
        control.SetVoucherNumber("PMT-0001");
        control.SetVoucherDate(new DateTime(2026, 5, 1));

        // Act
        var voucherData = control.GetVoucherData(financialYearId: 5);

        // Assert
        voucherData.Should().NotBeNull();
        voucherData!.FinancialYearId.Should().Be(5);
        voucherData.VoucherTypeId.Should().Be(1);
        voucherData.VoucherDate.Should().Be(new DateTime(2026, 5, 1));
    }
}
