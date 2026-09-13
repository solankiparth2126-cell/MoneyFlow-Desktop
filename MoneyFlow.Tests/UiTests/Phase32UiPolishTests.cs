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
        // Unified Executive Ledger Theme Palette
        var colors = ThemeManager.Colors;
        colors.HeaderBg.Should().Be(ExecLedgerTheme.PrimaryNavy);
        colors.HeaderFg.Should().Be(ExecLedgerTheme.WhiteText);
        colors.WindowBg.Should().Be(ExecLedgerTheme.ApplicationCanvas);
        colors.SurfaceBg.Should().Be(ExecLedgerTheme.WorkSurface);
        colors.SuccessFg.Should().NotBe(colors.DangerFg);
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
