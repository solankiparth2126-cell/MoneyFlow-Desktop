using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Guna.UI2.WinForms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Desktop.Configuration;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class VoucherLayoutTests
{
    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("VoucherLayoutTestDb_" + Guid.NewGuid().ToString()));
        services.AddStorageServices(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MF_VoucherTests_" + Guid.NewGuid().ToString("N")));
        services.AddDataRepositories();
        services.AddDomainServices();
        services.AddNavigationServices();
        services.AddDesktopForms();

        var provider = services.BuildServiceProvider();

        var compService = provider.GetRequiredService<ICompanyService>();
        var company = compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Executive Ledger Corp",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        }).GetAwaiter().GetResult();

        var dbContext = provider.GetRequiredService<AppDbContext>();
        var fy = dbContext.FinancialYears.First(f => f.CompanyId == company.CompanyId);

        var compContext = provider.GetRequiredService<ICompanyContext>();
        compContext.SetActiveCompany(company, fy);

        return provider;
    }

    [Theory]
    [InlineData(typeof(ContraVoucherForm), "Contra")]
    [InlineData(typeof(PaymentVoucherForm), "Payment")]
    [InlineData(typeof(ReceiptVoucherForm), "Receipt")]
    [InlineData(typeof(JournalVoucherForm), "Journal")]
    [InlineData(typeof(SalesVoucherForm), "Sales")]
    [InlineData(typeof(PurchaseVoucherForm), "Purchase")]
    public void AllVouchers_InheritFromBaseVoucherForm_AndOpenMaximized(Type voucherFormType, string activeVoucherKey)
    {
        var provider = CreateServiceProvider();
        using var form = (BaseVoucherForm)provider.GetRequiredService(voucherFormType);

        // 1. Inherits from BaseVoucherForm
        form.Should().BeAssignableTo<BaseVoucherForm>();

        // 2. MinimumSize and KeyPreview configured for desktop ERP
        form.MinimumSize.Width.Should().Be(950);
        form.MinimumSize.Height.Should().Be(600);
        form.KeyPreview.Should().BeTrue();

        // 3. Contains Top Header Bar (32px Navy)
        var topBar = form.Controls.OfType<TallyTopHeaderBar>().FirstOrDefault();
        topBar.Should().NotBeNull("every voucher must contain the master TallyTopHeaderBar");
        topBar!.Dock.Should().Be(DockStyle.Top);

        // 4. Contains Right Side Action Bar (120px F-key vertical panel)
        var sideBar = form.Controls.OfType<TallySideActionBar>().FirstOrDefault();
        sideBar.Should().NotBeNull("every voucher must contain the master TallySideActionBar");
        sideBar!.Dock.Should().Be(DockStyle.Right);
        sideBar.Width.Should().Be(120);

        // 5. Contains Ledger Flyout Panel
        var flyout = form.Controls.OfType<TallyLedgerFlyoutPanel>().FirstOrDefault();
        flyout.Should().NotBeNull("every voucher must contain the master TallyLedgerFlyoutPanel");

        // 6. Contains Center Work Panel with DockStyle.Fill
        var centerPanel = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Fill);
        centerPanel.Should().NotBeNull("every voucher must mount into CenterWorkPanel");

        // 7. Active voucher key matches expected F-key
        var activeVoucherField = typeof(TallySideActionBar).GetField("_activeVoucher", BindingFlags.NonPublic | BindingFlags.Instance);
        var activeVoucherValue = activeVoucherField?.GetValue(sideBar) as string;
        activeVoucherValue.Should().Be(activeVoucherKey);
    }

    [Theory]
    [InlineData(typeof(ContraVoucherForm))]
    [InlineData(typeof(PaymentVoucherForm))]
    [InlineData(typeof(ReceiptVoucherForm))]
    [InlineData(typeof(JournalVoucherForm))]
    [InlineData(typeof(SalesVoucherForm))]
    [InlineData(typeof(PurchaseVoucherForm))]
    public void AllVouchers_ContainStandardActionButtons(Type voucherFormType)
    {
        var provider = CreateServiceProvider();
        using var form = (BaseVoucherForm)provider.GetRequiredService(voucherFormType);

        // Find all Guna2Buttons across all controls in the form
        var allButtons = GetAllControlsRecurse<Guna2Button>(form).ToList();

        // Must have the standard 4 bottom action buttons
        allButtons.Should().Contain(b => b.Text.Contains("Accept", StringComparison.OrdinalIgnoreCase));
        allButtons.Should().Contain(b => b.Text.Contains("Quit", StringComparison.OrdinalIgnoreCase) || b.Text.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        allButtons.Should().Contain(b => b.Text.Contains("Clear", StringComparison.OrdinalIgnoreCase));
        allButtons.Should().Contain(b => b.Text.Contains("Print", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BaseVoucherForm_HasOtherVouchersAction()
    {
        var provider = CreateServiceProvider();
        using var form = provider.GetRequiredService<PaymentVoucherForm>();

        var sideBar = form.Controls.OfType<TallySideActionBar>().FirstOrDefault();
        sideBar.Should().NotBeNull();

        // Verify F10 button is present on SideActionBar
        var buttons = GetAllControlsRecurse<Guna2Button>(sideBar!).ToList();
        buttons.Should().Contain(b => b.Text.Contains("F10", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationService_FitFormToScreen_KeepsBaseVouchersMaximized()
    {
        var provider = CreateServiceProvider();
        using var form = provider.GetRequiredService<ReceiptVoucherForm>();

        // Call FitFormToScreen via reflection
        var method = typeof(NavigationService).GetMethod("FitFormToScreen", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        method!.Invoke(null, new object?[] { form, null });

        // Base voucher must remain Maximized and must NOT be shrunk to dialog dimensions
        form.WindowState.Should().Be(FormWindowState.Maximized);
    }

    [Fact]
    public async Task InstantVoucherSwitching_ForwardAndBackward_InSameWindow()
    {
        var provider = CreateServiceProvider();
        using var form = provider.GetRequiredService<AccountingVoucherForm>();

        // 1. Initial voucher is Payment (F5)
        form.CurrentView.Should().NotBeNull();
        form.CurrentView!.VoucherType.Should().Be(MoneyFlow.Core.Enums.VoucherTypeEnum.Payment);
        form.Text.Should().Be("Payment Voucher (F5)");

        var sideBar = form.Controls.OfType<TallySideActionBar>().First();
        var activeVoucherField = typeof(TallySideActionBar).GetField("_activeVoucher", BindingFlags.NonPublic | BindingFlags.Instance);
        activeVoucherField?.GetValue(sideBar).Should().Be("Payment");

        // Forward Sequence: F6 Receipt -> F7 Journal -> F8 Sales -> F9 Purchase -> F4 Contra -> F5 Payment
        var forwardOrder = new[]
        {
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Receipt, "Receipt Voucher (F6)", "Receipt"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Journal, "Journal Voucher (F7)", "Journal"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Sales, "Sales Voucher (F8)", "Sales"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Purchase, "Purchase Voucher (F9)", "Purchase"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Contra, "Contra Voucher (F4)", "Contra"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Payment, "Payment Voucher (F5)", "Payment")
        };

        foreach (var (targetType, expectedTitle, expectedKey) in forwardOrder)
        {
            var switched = await form.SwitchVoucherAsync(targetType);
            switched.Should().BeTrue();
            form.CurrentView!.VoucherType.Should().Be(targetType);
            form.Text.Should().Be(expectedTitle);
            activeVoucherField?.GetValue(sideBar).Should().Be(expectedKey);
        }

        // Backward Sequence: F9 Purchase -> F8 Sales -> F7 Journal -> F6 Receipt -> F5 Payment -> F4 Contra
        var backwardOrder = new[]
        {
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Purchase, "Purchase Voucher (F9)", "Purchase"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Sales, "Sales Voucher (F8)", "Sales"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Journal, "Journal Voucher (F7)", "Journal"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Receipt, "Receipt Voucher (F6)", "Receipt"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Payment, "Payment Voucher (F5)", "Payment"),
            (MoneyFlow.Core.Enums.VoucherTypeEnum.Contra, "Contra Voucher (F4)", "Contra")
        };

        foreach (var (targetType, expectedTitle, expectedKey) in backwardOrder)
        {
            var switched = await form.SwitchVoucherAsync(targetType);
            switched.Should().BeTrue();
            form.CurrentView!.VoucherType.Should().Be(targetType);
            form.Text.Should().Be(expectedTitle);
            activeVoucherField?.GetValue(sideBar).Should().Be(expectedKey);
        }
    }

    [Fact]
    public async Task SideActionBar_MouseClickButtons_SwitchVoucherInPlace()
    {
        var provider = CreateServiceProvider();
        using var form = provider.GetRequiredService<PaymentVoucherForm>();

        form.CurrentView!.VoucherType.Should().Be(MoneyFlow.Core.Enums.VoucherTypeEnum.Payment);

        var sideBar = form.Controls.OfType<TallySideActionBar>().First();

        // Simulate Receipt click on SideActionBar
        var receiptEvent = typeof(TallySideActionBar).GetField("ReceiptClicked", BindingFlags.NonPublic | BindingFlags.Instance);
        var receiptDelegate = receiptEvent?.GetValue(sideBar) as Action;
        receiptDelegate?.Invoke();

        // Allow any async transition to complete
        await Task.Delay(100);

        form.CurrentView!.VoucherType.Should().Be(MoneyFlow.Core.Enums.VoucherTypeEnum.Receipt);
        form.Text.Should().Be("Receipt Voucher (F6)");
    }

    [Fact]
    public void VoucherView_HasUnsavedChanges_DetectsPendingData()
    {
        var provider = CreateServiceProvider();
        using var form = provider.GetRequiredService<PaymentVoucherForm>();

        // Clean initial state
        form.CurrentView!.HasUnsavedChanges().Should().BeFalse();

        // Simulate user entering a narration in the payment view
        var narrationBox = GetAllControlsRecurse<TextBox>(form.CurrentView.ContentControl)
            .FirstOrDefault(t => t.Multiline == false && t.Width != 160); // Find narration textbox
        if (narrationBox != null)
        {
            narrationBox.Text = "Advance payment to supplier";
            form.CurrentView.HasUnsavedChanges().Should().BeTrue("typing a narration constitutes unsaved data");
        }
    }

    [Fact]
    public void TallyLedgerFlyoutPanel_FlatListAndOwnerDraw_RendersWithoutError()
    {
        var flyout = new TallyLedgerFlyoutPanel();
        var ledgers = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-Hand", GroupNature = MoneyFlow.Core.Enums.GroupNature.Assets, IsActive = true },
            new() { LedgerId = 2, LedgerName = "HDFC Bank", GroupName = "Bank Accounts", GroupNature = MoneyFlow.Core.Enums.GroupNature.Assets, IsActive = true }
        };

        flyout.LoadLedgers(ledgers, includeEndOfList: true);

        // ListBox should have sentinel + 2 ledgers = 3 items
        var listBox = GetAllControlsRecurse<ListBox>(flyout).First();
        listBox.Items.Count.Should().Be(3);
        listBox.SelectedIndex.Should().Be(0);

        // Simulate owner draw on all items to verify 0 GDI/NullReference exceptions
        using var bmp = new Bitmap(300, 200);
        using var g = Graphics.FromImage(bmp);

        var onDrawMethod = typeof(TallyLedgerFlyoutPanel).GetMethod("OnListDrawItem", BindingFlags.NonPublic | BindingFlags.Instance);
        onDrawMethod.Should().NotBeNull();

        for (int i = 0; i < listBox.Items.Count; i++)
        {
            var args = new DrawItemEventArgs(g, listBox.Font, new Rectangle(0, i * 22, 300, 22), i, DrawItemState.Default);
            var act = () => onDrawMethod!.Invoke(flyout, new object?[] { listBox, args });
            act.Should().NotThrow("owner-drawn rendering must be 100% resilient and never throw");
        }

        // Selected state drawing
        var selArgs = new DrawItemEventArgs(g, listBox.Font, new Rectangle(0, 0, 300, 22), 0, DrawItemState.Selected);
        var selAct = () => onDrawMethod!.Invoke(flyout, new object?[] { listBox, selArgs });
        selAct.Should().NotThrow();
    }

    [Fact]
    public void TallyLedgerFlyoutPanel_SelectionOfSentinel_ReturnsNull()
    {
        var flyout = new TallyLedgerFlyoutPanel();
        var ledgers = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-Hand", IsActive = true }
        };

        flyout.LoadLedgers(ledgers, includeEndOfList: true);

        bool callbackFired = false;
        LedgerSummaryDto? selectedLedger = new();
        flyout.LedgerSelected += l =>
        {
            callbackFired = true;
            selectedLedger = l;
        };

        // Item 0 is Sentinel
        var confirmMethod = typeof(TallyLedgerFlyoutPanel).GetMethod("ConfirmSelection", BindingFlags.NonPublic | BindingFlags.Instance);
        confirmMethod.Should().NotBeNull();
        confirmMethod!.Invoke(flyout, null);

        callbackFired.Should().BeTrue();
        selectedLedger.Should().BeNull("selecting '-- End of List --' must invoke callback with null to indicate end of list");
    }

    [Fact]
    public void TallyLedgerFlyoutPanel_SearchFilter_ReducesList()
    {
        var flyout = new TallyLedgerFlyoutPanel();
        var ledgers = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-Hand", IsActive = true },
            new() { LedgerId = 2, LedgerName = "HDFC Bank", GroupName = "Bank Accounts", IsActive = true },
            new() { LedgerId = 3, LedgerName = "Sales", GroupName = "Sales Accounts", IsActive = true }
        };

        flyout.LoadLedgers(ledgers, includeEndOfList: true);

        var filterMethod = typeof(TallyLedgerFlyoutPanel).GetMethod("ApplyFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        filterMethod.Should().NotBeNull();

        filterMethod!.Invoke(flyout, new object[] { "HDFC" });

        var listBox = GetAllControlsRecurse<ListBox>(flyout).First();
        listBox.Items.Count.Should().Be(1);
    }

    private static IEnumerable<T> GetAllControlsRecurse<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T typed)
                yield return typed;

            foreach (var grandChild in GetAllControlsRecurse<T>(child))
                yield return grandChild;
        }
    }
}
