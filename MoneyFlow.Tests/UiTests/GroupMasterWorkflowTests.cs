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
using MoneyFlow.Desktop.Controls.Fields;
using MoneyFlow.Desktop.Controls.Keyboard;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class GroupMasterWorkflowTests
{
    [Fact]
    public void KeyboardNavigationController_EnterKey_AdvancesInTabIndexOrder()
    {
        using var form = new Form();
        var txt1 = new TextBox { TabIndex = 0, Name = "txt1" };
        var txt2 = new TextBox { TabIndex = 1, Name = "txt2" };
        var txt3 = new TextBox { TabIndex = 2, Name = "txt3" };

        form.Controls.AddRange(new Control[] { txt3, txt1, txt2 }); // Added in mixed order

        bool submitted = false;
        var controller = new MoneyFlowKeyboardNavigationController(form, onSubmit: () => submitted = true);

        // Advance from txt1 to txt2
        controller.NavigateNext(txt1);
        controller.CurrentlyFocusedControl.Should().Be(txt2);

        // Advance from txt2 to txt3
        controller.NavigateNext(txt2);
        controller.CurrentlyFocusedControl.Should().Be(txt3);

        // On last field (txt3) -> triggers submit
        controller.NavigateNext(txt3);
        submitted.Should().BeTrue();
    }

    [Fact]
    public void KeyboardNavigationController_ShiftEnter_NavigatesPrevious()
    {
        using var form = new Form();
        var txt1 = new TextBox { TabIndex = 0, Name = "txt1" };
        var txt2 = new TextBox { TabIndex = 1, Name = "txt2" };

        form.Controls.AddRange(new Control[] { txt1, txt2 });
        var controller = new MoneyFlowKeyboardNavigationController(form);

        // Previous from txt2 to txt1
        controller.NavigatePrevious(txt2);
        controller.CurrentlyFocusedControl.Should().Be(txt1);
    }

    [Fact]
    public void MoneyFlowTextLookup_AutoOpenOnFocus_PropertyDefaultsToTrue()
    {
        var lookup = new MoneyFlowTextLookup();
        lookup.AutoOpenOnFocus.Should().BeTrue();
    }

    [Fact]
    public async Task GroupMasterList_DefaultSortOrder_StrictlyNewestFirst_GroupIdDesc()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);

        var dbRecords = new List<GroupSummaryDto>
        {
            new() { GroupId = 1, GroupName = "Group A (Oldest)", Nature = GroupNature.Assets },
            new() { GroupId = 2, GroupName = "Group B", Nature = GroupNature.Liabilities },
            new() { GroupId = 3, GroupName = "Group C", Nature = GroupNature.Income },
            new() { GroupId = 4, GroupName = "Group D (Newest)", Nature = GroupNature.Expenses }
        };

        var fakeGroupService = new FakeGroupService { GroupsToReturn = dbRecords };

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        await groupListForm.LoadGroupsFromDatabaseAsync();

        // Find the DataGridView inside the form
        var grid = groupListForm.Grid;

        grid.Should().NotBeNull();
        grid!.Rows.Count.Should().Be(4);

        // Newest (GroupId = 4) MUST appear at the TOP (row 0)
        grid.Rows[0].Cells["GroupId"].Value.Should().Be(4);
        grid.Rows[0].Cells["GroupName"].Value.Should().Be("Group D (Newest)");

        // Oldest (GroupId = 1) MUST appear at the BOTTOM
        grid.Rows[3].Cells["GroupId"].Value.Should().Be(1);
        grid.Rows[3].Cells["GroupName"].Value.Should().Be("Group A (Oldest)");

        grid.ColumnHeadersVisible.Should().BeTrue();
        grid.ColumnHeadersHeight.Should().Be(36);
        grid.ColumnHeadersDefaultCellStyle.BackColor.Should().Be(Color.FromArgb(27, 54, 93));

        grid.Columns["GroupName"]!.HeaderText.Should().Be("GROUP NAME");
        grid.Columns["UnderGroup"]!.HeaderText.Should().Be("UNDER");
        grid.Columns["Nature"]!.HeaderText.Should().Be("NATURE");
        grid.Columns["Status"]!.HeaderText.Should().Be("STATUS");

        grid.Columns["GroupName"]!.FillWeight.Should().Be(42);
        grid.Columns["UnderGroup"]!.FillWeight.Should().Be(23);
        grid.Columns["Nature"]!.FillWeight.Should().Be(23);
        grid.Columns["Status"]!.FillWeight.Should().Be(12);
    }

    [Fact]
    public void GroupCreation_FullWorkflow_SwitchesViewsInsideSamePanel()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        // Initially list container is active, creation view is not
        groupListForm.IsCreationViewActive.Should().BeFalse();

        // Trigger OpenCreateGroupView
        groupListForm.OpenCreateGroupView();

        // List must disappear and full-screen creation view must occupy the dynamic space
        groupListForm.IsCreationViewActive.Should().BeTrue();

        var creationForm = groupListForm.ActiveCreationForm;
        creationForm.Should().NotBeNull();
        creationForm!.FormBorderStyle.Should().Be(FormBorderStyle.None);
        creationForm.Dock.Should().Be(DockStyle.Fill);
    }

    [Fact]
    public void MoneyFlowField_TallyStyleDisplay_ActiveShowsEditor_InactiveShowsPlainValue()
    {
        var field = new MoneyFlowField("Under", labelWidth: 260);
        var txt = new TextBox { Text = "Capital Account" };
        field.SetEditor(txt, () => txt.Text);

        // Inactive by default
        field.IsActive.Should().BeFalse();
        var lblDisplay = field.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "Capital Account");
        lblDisplay.Should().NotBeNull();
        lblDisplay!.Visible.Should().BeTrue("Inactive field must display plain text value");

        // When active
        field.SetActive(true);
        field.IsActive.Should().BeTrue();
        lblDisplay.Visible.Should().BeFalse("Active field must hide the plain text value");

        // When deactivated again
        txt.Text = "Current Assets";
        field.SetActive(false);
        field.IsActive.Should().BeFalse();
        lblDisplay.Text.Should().Be("Current Assets");
        lblDisplay.Visible.Should().BeTrue();
    }

    [Fact]
    public void GroupCreationForm_ActiveFieldTransition_OnlyOneFieldActiveAtATime()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var creationForm = new GroupCreateEditForm(
            fakeGroupService,
            companyId: 1);

        // Find all MoneyFlowField rows in the form
        var fields = creationForm.Controls
            .OfType<Panel>()
            .SelectMany(p => p.Controls.OfType<Panel>())
            .SelectMany(p => p.Controls.OfType<MoneyFlowField>())
            .ToList();

        fields.Should().HaveCount(9, "9 Group Creation fields must be present");

        // Activate field 0 (Name)
        creationForm.SetActiveField(0);
        fields[0].IsActive.Should().BeTrue("Field 0 (Name) must be active");
        fields.Skip(1).All(f => !f.IsActive).Should().BeTrue("All other fields must be inactive");

        // Activate field 2 (Under)
        creationForm.SetActiveField(2);
        fields[2].IsActive.Should().BeTrue("Field 2 (Under) must be active");
        fields.Where((f, i) => i != 2).All(f => !f.IsActive).Should().BeTrue("Only field 2 must be active");
    }

    [Fact]
    public void GroupMaster_RightActionPanel_Exists_AndIsDockedToRight()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        groupListForm.RightActionPanel.Should().NotBeNull();
        groupListForm.RightActionPanel.Dock.Should().Be(DockStyle.Right);
        groupListForm.RightActionPanel.Width.Should().BeInRange(180, 215, "Width must match compact Reference Image 1 sidebar (180px–210px)");
        groupListForm.RightActionPanel.BackColor.Should().Be(Color.FromArgb(224, 237, 253), "Panel must have light blue #E0EDFD background matching Reference Image 1");
    }

    [Fact]
    public void GroupMaster_NoBottomHorizontalActionBar_Exists()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        // Verify there is NO bottom-docked panel anywhere in the Group Master List
        var bottomPanels = groupListForm.ListContainer.Controls
            .OfType<Panel>()
            .Where(p => p.Dock == DockStyle.Bottom)
            .ToList();

        bottomPanels.Should().BeEmpty("Bottom action bar is strictly forbidden");
    }

    [Fact]
    public void GroupMaster_RightActionPanel_Buttons_VerticalOrder_Colors_AndShortcuts()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        var pnl = groupListForm.RightActionPanel;
        var btnCreate = groupListForm.ButtonCreate;
        var btnAlter = groupListForm.ButtonAlter;
        var btnDelete = groupListForm.ButtonDelete;
        var btnSearch = groupListForm.ButtonSearch;
        var btnClose = groupListForm.ButtonClose;

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

        // Verify Image 2 Corporate Navy Color (#1B365D / RGB 27, 54, 93) for all buttons
        var image2Navy = Color.FromArgb(27, 54, 93);

        btnCreate.BackColor.Should().Be(image2Navy);
        btnCreate.ForeColor.Should().Be(Color.White);

        btnAlter.BackColor.Should().Be(image2Navy);
        btnAlter.ForeColor.Should().Be(Color.White);

        btnDelete.BackColor.Should().Be(image2Navy);
        btnDelete.ForeColor.Should().Be(Color.White);

        btnSearch.BackColor.Should().Be(image2Navy);
        btnSearch.ForeColor.Should().Be(Color.White);

        btnClose.BackColor.Should().Be(image2Navy);
        btnClose.ForeColor.Should().Be(Color.White);
    }

    [Fact]
    public void GroupMaster_FindButton_AndAltF_FocusesSearchTextBox()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        // Trigger SEARCH / FIND button
        groupListForm.RightActionPanel.TriggerSearch();

        // Search text box should receive focus/be focused
        groupListForm.FocusSearchBox();
        groupListForm.ButtonSearch.Should().NotBeNull();
        groupListForm.ButtonFind.Should().NotBeNull();
    }

    [Fact]
    public void GroupMaster_RightActionPanel_CreateButton_TriggersViewSwitch()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        groupListForm.IsCreationViewActive.Should().BeFalse();

        // Trigger right action panel's CREATE button
        groupListForm.RightActionPanel.TriggerCreate();

        groupListForm.IsCreationViewActive.Should().BeTrue();
        groupListForm.ActiveCreationForm.Should().NotBeNull();
    }

    [Fact]
    public async Task GroupMaster_DynamicCompanyTitle_AndFooterCounts()
    {
        var testCompany = new Company { CompanyId = 42, CompanyName = "Parth inc." };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);

        var dbRecords = new List<GroupSummaryDto>
        {
            new() { GroupId = 10, GroupName = "Unsecured Loans", Nature = GroupNature.Liabilities },
            new() { GroupId = 20, GroupName = "Sundry Debtors", Nature = GroupNature.Assets },
            new() { GroupId = 30, GroupName = "Sundry Creditors", Nature = GroupNature.Liabilities }
        };

        var fakeGroupService = new FakeGroupService { GroupsToReturn = dbRecords };

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        await groupListForm.LoadGroupsFromDatabaseAsync();

        // Check header title displays dynamic company name
        var titleLabel = groupListForm.TitleLabel;

        titleLabel.Should().NotBeNull();
        titleLabel!.Text.Should().Be("GROUP MASTER — Parth inc.");

        // Check footer displays dynamic count
        var footerLabel = groupListForm.FooterLabel;

        footerLabel.Should().NotBeNull();
        footerLabel!.Text.Should().Contain("Total Groups: 3").And.Contain("Showing: 1–3 of 3");
    }

    [Fact]
    public void GroupMaster_EscapeKeyNavigation_FromCreationView_ReturnsToList_AndNextEscape_SignalsShellBack()
    {
        var prevDialogProvider = MoneyFlowEscController.ConfirmationDialogProvider;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => true;
        try
        {
            var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
            var fakeCompanyContext = new FakeCompanyContext(testCompany);
            var fakeGroupService = new FakeGroupService();

            using var groupListForm = new GroupListForm(
                fakeGroupService,
                null!,
                null!,
                fakeCompanyContext);

            // Step 1: Initially on Group Master List
            groupListForm.IsCreationViewActive.Should().BeFalse();

            // Step 2: Open Create Group View
            groupListForm.OpenCreateGroupView();
            groupListForm.IsCreationViewActive.Should().BeTrue();

            // Step 3: First ESC while in Create View -> Must go back to Group Master List (NOT Dashboard)
            var handledInCreation = groupListForm.HandleBackNavigation();
            handledInCreation.Should().BeTrue("ESC in Creation View must be handled internally to return to List");
            groupListForm.IsCreationViewActive.Should().BeFalse();

            // Step 4: Second ESC while on Group Master List -> Must signal host to return to Dashboard/Gateway
            var handledOnList = groupListForm.HandleBackNavigation();
            handledOnList.Should().BeFalse("ESC on Group Master List must not be consumed so shell navigates back to Dashboard");
        }
        finally
        {
            MoneyFlowEscController.ConfirmationDialogProvider = prevDialogProvider;
        }
    }

    [Fact]
    public async Task GroupMaster_AlterAndDeleteButtons_EnabledOnlyWhenRowSelected()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService { GroupsToReturn = new List<GroupSummaryDto>() };

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        await groupListForm.LoadGroupsFromDatabaseAsync();

        // When no rows are present in the grid: ALTER and DELETE must be disabled
        groupListForm.Grid.Rows.Count.Should().Be(0);
        groupListForm.ButtonAlter.Enabled.Should().BeFalse("ALTER must be disabled when no row is selected");
        groupListForm.ButtonDelete.Enabled.Should().BeFalse("DELETE must be disabled when no row is selected");
        groupListForm.ButtonCreate.Enabled.Should().BeTrue("CREATE must always be enabled");
        groupListForm.ButtonSearch.Enabled.Should().BeTrue("SEARCH must always be enabled");
        groupListForm.ButtonFind.Enabled.Should().BeTrue("FIND alias must always be enabled");
        groupListForm.ButtonClose.Enabled.Should().BeTrue("CLOSE must always be enabled");

        // Now populate with 1 row -> 1 row is selected by default
        fakeGroupService.GroupsToReturn = new List<GroupSummaryDto>
        {
            new() { GroupId = 1, GroupName = "Group A", Nature = GroupNature.Assets }
        };
        await groupListForm.LoadGroupsFromDatabaseAsync();
        groupListForm.Grid.Rows.Count.Should().Be(1);
        groupListForm.Grid.SelectedRows.Count.Should().Be(1);

        // When row is selected: ALTER and DELETE must be enabled
        groupListForm.ButtonAlter.Enabled.Should().BeTrue("ALTER must be enabled when a row is selected");
        groupListForm.ButtonDelete.Enabled.Should().BeTrue("DELETE must be enabled when a row is selected");

        // When selection is cleared: ALTER and DELETE must become disabled
        groupListForm.Grid.ClearSelection();
        groupListForm.UpdateActionButtonsEnabledState();
        groupListForm.ButtonAlter.Enabled.Should().BeFalse("ALTER must become disabled when row selection is cleared");
        groupListForm.ButtonDelete.Enabled.Should().BeFalse("DELETE must become disabled when row selection is cleared");
    }

    [Fact]
    public async Task GroupMaster_ScrollBarsNone_AndAutoScrollMethods()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService
        {
            GroupsToReturn = Enumerable.Range(1, 30)
                .Select(i => new GroupSummaryDto { GroupId = i, GroupName = $"Group {i}", Nature = GroupNature.Assets })
                .ToList()
        };

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        await groupListForm.LoadGroupsFromDatabaseAsync();

        // Native scrollbar must be hidden per user requirement
        groupListForm.Grid.ScrollBars.Should().Be(ScrollBars.None);

        // Initially row 0 selected
        groupListForm.Grid.SelectedRows[0].Index.Should().Be(0);

        // ScrollDownOneRow increments selected row
        groupListForm.ScrollDownOneRow();
        groupListForm.Grid.SelectedRows[0].Index.Should().Be(1);

        // ScrollUpOneRow decrements selected row
        groupListForm.ScrollUpOneRow();
        groupListForm.Grid.SelectedRows[0].Index.Should().Be(0);
    }

    [Fact]
    public async Task GroupMaster_DefaultFocus_IsOnGridFirstRow_AndListBorderIsRemoved()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService
        {
            GroupsToReturn = new List<GroupSummaryDto>
            {
                new() { GroupId = 10, GroupName = "Bank Accounts", Nature = GroupNature.Assets },
                new() { GroupId = 20, GroupName = "Cash", Nature = GroupNature.Assets }
            }
        };

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        await groupListForm.LoadGroupsFromDatabaseAsync();

        // 1. Grid first row selected by default
        groupListForm.Grid.SelectedRows.Count.Should().Be(1);
        groupListForm.Grid.SelectedRows[0].Index.Should().Be(0);

        // 2. FocusFirstRowInGrid selects row 0
        groupListForm.FocusFirstRowInGrid();
        groupListForm.Grid.SelectedRows[0].Index.Should().Be(0);

        // 3. Grid border style is none and tooltips disabled
        groupListForm.Grid.BorderStyle.Should().Be(BorderStyle.None);
        groupListForm.Grid.ColumnHeadersBorderStyle.Should().Be(DataGridViewHeaderBorderStyle.None);
        groupListForm.Grid.ShowCellToolTips.Should().BeFalse();
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
        public List<GroupSummaryDto> GroupsToReturn { get; set; } = new();

        public Task<Group> CreateGroupAsync(int companyId, GroupCreateDto dto, CancellationToken ct = default)
        {
            var g = new Group { GroupId = 99, CompanyId = companyId, GroupName = dto.GroupName, Nature = dto.Nature };
            return Task.FromResult(g);
        }

        public Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default) =>
            Task.FromResult<Group?>(new Group { GroupId = groupId, GroupName = "Test Group" });

        public Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupSummaryDto>>(GroupsToReturn);

        public Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupTreeNodeDto>>(new List<GroupTreeNodeDto>());

        public Task<IReadOnlyList<Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Group>>(new List<Group>());

        public Task<Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = dto.GroupId, GroupName = dto.GroupName });
    }
}
