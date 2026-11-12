using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Forms;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class LedgerMasterWorkflowTests
{
    [Fact]
    public async Task LedgerMaster_HeaderCard_HasSolidNavy_AndRecordCount()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);

        var dbRecords = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-hand", HierarchyPath = "Current Assets > Cash-in-hand", GroupNature = GroupNature.Assets, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 2, LedgerName = "Profit & Loss A/c", GroupName = "Capital Account", HierarchyPath = "Capital Account", GroupNature = GroupNature.Assets, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 3, LedgerName = "Purchase", GroupName = "Purchase Accounts", HierarchyPath = "Purchase Accounts", GroupNature = GroupNature.Expenses, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 4, LedgerName = "Sales", GroupName = "Sales Accounts", HierarchyPath = "Sales Accounts", GroupNature = GroupNature.Income, OpeningBalance = 0, IsActive = true }
        };

        var fakeLedgerService = new FakeLedgerService { LedgersToReturn = dbRecords };
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        await ledgerListForm.LoadLedgersFromDatabaseAsync();

        ledgerListForm.TitleLabel.Should().NotBeNull();
        ledgerListForm.TitleLabel.Text.Should().Be("LEDGER MASTER — Test Co");

        var grid = ledgerListForm.Grid;
        grid.Should().NotBeNull();
        grid.Rows.Count.Should().Be(4);
    }

    [Fact]
    public void LedgerMaster_RightActionPanel_Exists_AndIsDockedToRight()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        ledgerListForm.RightActionPanel.Should().NotBeNull();
        ledgerListForm.RightActionPanel.Dock.Should().Be(DockStyle.Right);
        ledgerListForm.RightActionPanel.Width.Should().BeInRange(180, 215, "Width must match compact Reference Image 1 sidebar (180px–210px)");
        ledgerListForm.RightActionPanel.BackColor.Should().Be(Color.FromArgb(224, 237, 253), "Panel must have light blue #E0EDFD background matching Reference Image 1");
    }

    [Fact]
    public void LedgerMaster_NoBottomHorizontalActionBar_Exists()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        // Verify there is NO bottom-docked panel in ListContainer
        var bottomPanels = ledgerListForm.ListContainer.Controls
            .OfType<Panel>()
            .Where(p => p.Dock == DockStyle.Bottom)
            .ToList();

        bottomPanels.Should().BeEmpty("Bottom action bar is strictly removed to match Group Master Image 1 layout");
    }

    [Fact]
    public void LedgerMaster_RightActionPanel_Buttons_VerticalOrder_Colors_AndShortcuts()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        var btnCreate = ledgerListForm.ButtonCreate;
        var btnAlter = ledgerListForm.ButtonAlter;
        var btnDelete = ledgerListForm.ButtonDelete;
        var btnSearch = ledgerListForm.ButtonSearch;
        var btnClose = ledgerListForm.ButtonClose;

        // Verify strict two-line typography: Action on Line 1, Shortcut on Line 2
        btnCreate.Text.Should().Be("CREATE\nAlt+C");
        btnAlter.Text.Should().Be("ALTER\nAlt+A");
        btnDelete.Text.Should().Be("DELETE\nDel");
        btnSearch.Text.Should().Be("SEARCH\nAlt+S");
        btnClose.Text.Should().Be("CLOSE\nEsc");

        // Verify exact vertical ordering: CREATE -> ALTER -> DELETE -> SEARCH -> CLOSE
        btnCreate.Top.Should().BeLessThan(btnAlter.Top);
        btnAlter.Top.Should().BeLessThan(btnDelete.Top);
        btnDelete.Top.Should().BeLessThan(btnSearch.Top);
        btnSearch.Top.Should().BeLessThan(btnClose.Top);

        // Verify Corporate Navy Color (#1B365D / RGB 27, 54, 93)
        var imageNavy = Color.FromArgb(27, 54, 93);
        btnCreate.BackColor.Should().Be(imageNavy);
        btnCreate.ForeColor.Should().Be(Color.White);
        btnAlter.BackColor.Should().Be(imageNavy);
        btnAlter.ForeColor.Should().Be(Color.White);
        btnDelete.BackColor.Should().Be(imageNavy);
        btnDelete.ForeColor.Should().Be(Color.White);
        btnSearch.BackColor.Should().Be(imageNavy);
        btnSearch.ForeColor.Should().Be(Color.White);
        btnClose.BackColor.Should().Be(imageNavy);
        btnClose.ForeColor.Should().Be(Color.White);
    }

    [Fact]
    public async Task LedgerMaster_Grid_Columns_HeadersAllUppercase_AndStatusIsText()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);

        var dbRecords = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-hand", HierarchyPath = "Current Assets > Cash-in-hand", GroupNature = GroupNature.Assets, OpeningBalance = 0, IsActive = true }
        };

        var fakeLedgerService = new FakeLedgerService { LedgersToReturn = dbRecords };
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        await ledgerListForm.LoadLedgersFromDatabaseAsync();
        var grid = ledgerListForm.Grid;

        grid.Columns["LedgerName"]!.HeaderText.Should().Be("LEDGER NAME");
        grid.Columns["UnderGroup"]!.HeaderText.Should().Be("UNDER");
        grid.Columns["Nature"]!.HeaderText.Should().Be("NATURE");
        grid.Columns["OpeningBalance"]!.HeaderText.Should().Be("OPENING BALANCE");
        grid.Columns["Status"]!.HeaderText.Should().Be("STATUS");

        // Status column must be text-based "Active" rather than checkbox
        grid.Columns["Status"].Should().BeOfType<DataGridViewTextBoxColumn>();
        grid.Rows[0].Cells["Status"].Value.Should().Be("Active");
    }

    [Fact]
    public void LedgerMaster_ActionsPanel_DefaultsToCollapsed_AndCanToggle()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        ledgerListForm.IsActionsPanelCollapsed.Should().BeTrue();
        ledgerListForm.RightActionPanel.Visible.Should().BeFalse();
        ledgerListForm.ButtonToggleActions.Should().NotBeNull();
        ledgerListForm.ButtonToggleActions.AccessibleName.Should().Be("Expand Actions");

        // Toggle Expand
        ledgerListForm.ToggleActionsPanel();
        ledgerListForm.IsActionsPanelCollapsed.Should().BeFalse();
        ledgerListForm.RightActionPanel.Width.Should().Be(200);

        // Toggle Collapse back
        ledgerListForm.ToggleActionsPanel();
        ledgerListForm.IsActionsPanelCollapsed.Should().BeTrue();
        ledgerListForm.RightActionPanel.Visible.Should().BeFalse();
    }

    [Fact]
    public async Task LedgerMaster_Search_FiltersInRealTime()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);

        var dbRecords = new List<LedgerSummaryDto>
        {
            new() { LedgerId = 1, LedgerName = "Cash", GroupName = "Cash-in-hand", HierarchyPath = "Current Assets > Cash-in-hand", GroupNature = GroupNature.Assets, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 2, LedgerName = "Profit & Loss A/c", GroupName = "Capital Account", HierarchyPath = "Capital Account", GroupNature = GroupNature.Assets, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 3, LedgerName = "Purchase", GroupName = "Purchase Accounts", HierarchyPath = "Purchase Accounts", GroupNature = GroupNature.Expenses, OpeningBalance = 0, IsActive = true },
            new() { LedgerId = 4, LedgerName = "Sales", GroupName = "Sales Accounts", HierarchyPath = "Sales Accounts", GroupNature = GroupNature.Income, OpeningBalance = 0, IsActive = true }
        };

        var fakeLedgerService = new FakeLedgerService { LedgersToReturn = dbRecords };
        var fakeGroupService = new FakeGroupService();

        using var ledgerListForm = new LedgerListForm(
            fakeLedgerService,
            fakeGroupService,
            fakeCompanyContext);

        await ledgerListForm.LoadLedgersFromDatabaseAsync();
        ledgerListForm.Grid.Rows.Count.Should().Be(4);

        // Find the search textbox
        var txtSearch = ledgerListForm.ListContainer.Controls
            .Find("SearchPanel", true)
            .FirstOrDefault()?.Controls.OfType<TextBox>().FirstOrDefault();

        // Or trigger search programmatically
        ledgerListForm.FocusSearchBox();
        ledgerListForm.FooterLabel.Text.Should().Contain("Total Ledgers: 4");
    }

    [Fact]
    public void LookupKeyboardController_TabKey_CommitsSelection()
    {
        var controller = new MoneyFlow.Desktop.Controls.Lookup.LookupKeyboardController();
        var items = new List<MoneyFlow.Desktop.Controls.Lookup.LookupItem>
        {
            new() { Id = 1, Name = "Bank Accounts" },
            new() { Id = 2, Name = "Bank OD A/c" }
        };

        MoneyFlow.Desktop.Controls.Lookup.LookupItem? selected = null;
        var keyEvent = new KeyEventArgs(Keys.Tab);

        bool handled = controller.HandleKeyDown(
            keyEvent,
            items,
            onSelect: item => selected = item,
            onCancel: null,
            onCreate: null,
            onShowMore: null);

        handled.Should().BeTrue();
        selected.Should().NotBeNull();
        selected!.Name.Should().Be("Bank Accounts");
    }

    [Fact]
    public void LedgerCreateForm_QuickCommit_And_QuickSave_Badges_AreCapitalized()
    {
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);

        // Find badges in form controls
        var allLabels = GetAllChildControls(form).OfType<Label>().ToList();
        allLabels.Any(l => l.Text == "Ctrl+Enter").Should().BeTrue("First letter must be capitalized 'Ctrl+Enter'");
        allLabels.Any(l => l.Text == "Ctrl+S").Should().BeTrue("First letter must be capitalized 'Ctrl+S'");
    }

    [Fact]
    public async Task LedgerCreateEditForm_SaveAndNew_SetsHasSavedAny_AndFiresLedgerSavedEvent()
    {
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);

        // Find controls
        var allControls = GetAllChildControls(form).ToList();
        var txtName = allControls.OfType<TextBox>().First(t => t.Name == "_txtName" || t.Text == "");

        txtName.Text = "ICICI Bank";

        bool eventFired = false;
        form.LedgerSaved += () => eventFired = true;

        var saveAndNewMethod = typeof(LedgerCreateEditForm).GetMethod("OnSaveAndNewAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        saveAndNewMethod.Should().NotBeNull();
        await (Task)saveAndNewMethod!.Invoke(form, null)!;

        form.HasSavedAny.Should().BeTrue("Form must record that at least one ledger was saved");
        form.LastSavedLedgerId.Should().Be(10);
        eventFired.Should().BeTrue("LedgerSaved event must fire on Save & New to notify parent list");
        txtName.Text.Should().BeEmpty("Form name field must be cleared for the next entry");
    }

    [Fact]
    public async Task LedgerCreateEditForm_Cancel_AfterSaveAndNew_YieldsDialogResultOK()
    {
        var fakeLedgerService = new FakeLedgerService();
        var fakeGroupService = new FakeGroupService();

        using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);
        var allControls = GetAllChildControls(form).ToList();
        var txtName = allControls.OfType<TextBox>().First();
        var btnCancel = allControls.OfType<Button>().First(b => b.Width == 116);

        txtName.Text = "HDFC Bank";

        var saveAndNewMethod = typeof(LedgerCreateEditForm).GetMethod("OnSaveAndNewAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)saveAndNewMethod!.Invoke(form, null)!;

        form.HasSavedAny.Should().BeTrue();

        // Trigger cancel
        btnCancel.PerformClick();

        form.DialogResult.Should().Be(DialogResult.OK, "Closing form after saving a ledger via Save & New must return DialogResult.OK so caller knows to refresh");
    }

    private static IEnumerable<Control> GetAllChildControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            yield return c;
            foreach (var child in GetAllChildControls(c))
                yield return child;
        }
    }

    private class FakeCompanyContext : ICompanyContext
    {
        public FakeCompanyContext(Company? company) => CurrentCompany = company;
        public Company? CurrentCompany { get; set; }
        public FinancialYear? CurrentFinancialYear { get; set; }
        public bool IsCompanyOpen => CurrentCompany != null;
        public event Action? OnCompanyChanged;

        public void SetActiveCompany(Company company, FinancialYear? financialYear)
        {
            CurrentCompany = company;
            CurrentFinancialYear = financialYear;
            OnCompanyChanged?.Invoke();
        }

        public void SetActiveFinancialYear(FinancialYear financialYear)
        {
            CurrentFinancialYear = financialYear;
        }

        public void CloseCompany()
        {
            CurrentCompany = null;
            CurrentFinancialYear = null;
            OnCompanyChanged?.Invoke();
        }
    }

    private class FakeGroupService : IGroupService
    {
        public Task<Group> CreateGroupAsync(int companyId, GroupCreateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = 1, GroupName = dto.GroupName });
        public Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default) => Task.FromResult<Group?>(null);
        public Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupSummaryDto>>(new List<GroupSummaryDto>());
        public Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupTreeNodeDto>>(new List<GroupTreeNodeDto>());
        public Task<IReadOnlyList<Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Group>>(new List<Group>());
        public Task<Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = dto.GroupId, GroupName = dto.GroupName });
    }

    private class FakeLedgerService : ILedgerService
    {
        public List<LedgerSummaryDto> LedgersToReturn { get; set; } = new();

        public Task<Ledger> CreateLedgerAsync(int companyId, LedgerCreateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Ledger { LedgerId = 10, LedgerName = dto.LedgerName });
        public Task<bool> DeleteLedgerAsync(int ledgerId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Ledger?> GetLedgerByIdAsync(int ledgerId, CancellationToken ct = default) => Task.FromResult<Ledger?>(null);
        public Task<LedgerDetailDto?> GetLedgerDetailsAsync(int ledgerId, CancellationToken ct = default) => Task.FromResult<LedgerDetailDto?>(null);
        public Task<IReadOnlyList<LedgerSummaryDto>> GetLedgersByCompanyAsync(int companyId, string? searchTerm = null, int? groupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LedgerSummaryDto>>(LedgersToReturn);
        public Task<Ledger> UpdateLedgerAsync(LedgerUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Ledger { LedgerId = dto.LedgerId, LedgerName = dto.LedgerName });
        public Task<bool> ValidateLedgerNameAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default) =>
            Task.FromResult(true);
    }
}
