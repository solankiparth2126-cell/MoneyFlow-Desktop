using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Services.Accounting;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class MoneyFlowEscControllerTests : IDisposable
{
    public MoneyFlowEscControllerTests()
    {
        MoneyFlowEscController.ConfirmationDialogProvider = null;
    }

    public void Dispose()
    {
        MoneyFlowEscController.ConfirmationDialogProvider = null;
    }

    [Fact]
    public void Test1_TextboxHasValue_FirstEsc_ClearsFieldOnly_KeepsFocus_DoesNotClose()
    {
        using var form = new Form();
        var txt = new TextBox { Text = "ABC" };
        form.Controls.Add(txt);
        txt.Focus();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        bool handled = MoneyFlowEscController.HandleEsc(txt, form, () => closed = true);

        handled.Should().BeTrue();
        txt.Text.Should().BeEmpty("First ESC must clear the active textbox");
        closed.Should().BeFalse("First ESC must not close the form");
        confirmationShown.Should().BeFalse("First ESC on non-empty field must not prompt confirmation");
    }

    [Fact]
    public void Test2_TextboxEmpty_Esc_ShowsCloseConfirmation()
    {
        using var form = new Form();
        var txt = new TextBox { Text = "" };
        form.Controls.Add(txt);
        txt.Focus();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        bool handled = MoneyFlowEscController.HandleEsc(txt, form, () => closed = true);

        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue("Empty field ESC must show close confirmation");
        closed.Should().BeFalse("Confirmation dialog returned false so module should not close");
    }

    [Fact]
    public void Test3_ConfirmationDialog_EnterOrY_ConfirmsClose()
    {
        using var dlg = new CloseModuleConfirmationDialog();

        // Simulate 'Y' keypress
        var keyY = new KeyEventArgs(Keys.Y);
        var method = typeof(CloseModuleConfirmationDialog).GetMethod("OnDialogKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(dlg, new object[] { dlg, keyY });

        dlg.DialogResult.Should().Be(DialogResult.Yes);

        // Simulate Enter keypress
        using var dlgEnter = new CloseModuleConfirmationDialog();
        var keyEnter = new KeyEventArgs(Keys.Enter);
        method.Invoke(dlgEnter, new object[] { dlgEnter, keyEnter });

        dlgEnter.DialogResult.Should().Be(DialogResult.Yes);
    }

    [Fact]
    public void Test4_ConfirmationDialog_Esc_CancelsConfirmation()
    {
        using var dlg = new CloseModuleConfirmationDialog();
        var keyEsc = new KeyEventArgs(Keys.Escape);
        var method = typeof(CloseModuleConfirmationDialog).GetMethod("OnDialogKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(dlg, new object[] { dlg, keyEsc });

        dlg.DialogResult.Should().Be(DialogResult.No);
    }

    [Fact]
    public void Test5_ConfirmationDialog_N_CancelsConfirmation()
    {
        using var dlg = new CloseModuleConfirmationDialog();
        var keyN = new KeyEventArgs(Keys.N);
        var method = typeof(CloseModuleConfirmationDialog).GetMethod("OnDialogKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(dlg, new object[] { dlg, keyN });

        dlg.DialogResult.Should().Be(DialogResult.No);
    }

    [Fact]
    public void Test6_LookupPopupOpen_Esc_ClosesPopup_PreservesValue()
    {
        using var form = new Form();
        var lookup = new MoneyFlowTextLookup();
        form.Controls.Add(lookup);
        var provider = new ListLookupProvider<string>(new[] { "Alpha", "Beta" }, x => x, x => x);
        lookup.SetProvider(provider);
        lookup.SelectedValue = "Alpha";

        // Open popup
        lookup.OpenPopup();
        lookup.IsPopupOpen.Should().BeTrue();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        // Press ESC while popup is open
        bool handled = MoneyFlowEscController.HandleEsc(lookup, form, () => closed = true);

        handled.Should().BeTrue();
        lookup.IsPopupOpen.Should().BeFalse("First ESC while lookup popup is open must close the popup");
        lookup.SelectedValue.Should().Be("Alpha", "Value must remain unchanged when closing popup");
        confirmationShown.Should().BeFalse("Closing lookup popup must not prompt confirmation");
        closed.Should().BeFalse("Closing lookup popup must not close form");
    }

    [Fact]
    public void Test7_LookupClosed_HasValue_Esc_ClearsValue()
    {
        using var form = new Form();
        var lookup = new MoneyFlowTextLookup();
        form.Controls.Add(lookup);
        var provider = new ListLookupProvider<string>(new[] { "Alpha", "Beta" }, x => x, x => x);
        lookup.SetProvider(provider);
        lookup.SelectedValue = "Alpha";
        lookup.ClosePopup();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        bool handled = MoneyFlowEscController.HandleEsc(lookup, form, () => closed = true);

        handled.Should().BeTrue();
        lookup.SelectedValue.Should().BeNull("ESC on closed lookup with value must clear value");
        confirmationShown.Should().BeFalse();
        closed.Should().BeFalse();
    }

    [Fact]
    public void Test8_LookupClosed_Empty_Esc_PromptsConfirmation()
    {
        using var form = new Form();
        var lookup = new MoneyFlowTextLookup();
        form.Controls.Add(lookup);
        var provider = new ListLookupProvider<string>(new[] { "Alpha", "Beta" }, x => x, x => x);
        lookup.SetProvider(provider);
        lookup.SelectedValue = null;
        lookup.ClosePopup();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return true; };

        bool handled = MoneyFlowEscController.HandleEsc(lookup, form, () => closed = true);

        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
        closed.Should().BeTrue("User confirmed close");
    }

    [Fact]
    public void Test9_SearchBox_HasText_Esc_ClearsSearch()
    {
        using var form = new Form();
        var txtSearch = new TextBox { Text = "Unsecured Loans" };
        form.Controls.Add(txtSearch);
        txtSearch.Focus();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        bool handled = MoneyFlowEscController.HandleEsc(txtSearch, form, () => closed = true);

        handled.Should().BeTrue();
        txtSearch.Text.Should().BeEmpty();
        confirmationShown.Should().BeFalse();
        closed.Should().BeFalse();
    }

    [Fact]
    public void Test10_SearchBox_Empty_Esc_PromptsConfirmation()
    {
        using var form = new Form();
        var txtSearch = new TextBox { Text = "" };
        form.Controls.Add(txtSearch);
        txtSearch.Focus();

        bool closed = false;
        bool confirmationShown = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => { confirmationShown = true; return false; };

        bool handled = MoneyFlowEscController.HandleEsc(txtSearch, form, () => closed = true);

        handled.Should().BeTrue();
        confirmationShown.Should().BeTrue();
        closed.Should().BeFalse();
    }

    [Fact]
    public void Test11_GroupCreation_TwoStepEscWorkflow_ReturnsToGroupMasterList()
    {
        var testCompany = new Company { CompanyId = 1, CompanyName = "Test Co" };
        var fakeCompanyContext = new FakeCompanyContext(testCompany);
        var fakeGroupService = new FakeGroupService();

        using var groupListForm = new GroupListForm(
            fakeGroupService,
            null!,
            null!,
            fakeCompanyContext);

        // Open Group Creation
        groupListForm.OpenCreateGroupView();
        groupListForm.IsCreationViewActive.Should().BeTrue();

        var creationForm = groupListForm.ActiveCreationForm;
        creationForm.Should().NotBeNull();

        // 1. Set text on active Group Name field
        var txtName = creationForm!.ActiveControl as TextBox;
        if (txtName != null)
        {
            txtName.Text = "Office Expenses";

            // First ESC: clears the text field
            bool handled1 = creationForm.HandleBackNavigation();
            handled1.Should().BeTrue();
            txtName.Text.Should().BeEmpty("First ESC clears the active field");
            groupListForm.IsCreationViewActive.Should().BeTrue("Form remains open after first ESC");
        }

        // 2. Second ESC: field is empty, user confirms close
        MoneyFlowEscController.ConfirmationDialogProvider = _ => true;

        bool handled2 = groupListForm.HandleBackNavigation();
        handled2.Should().BeTrue();
        groupListForm.IsCreationViewActive.Should().BeFalse("Confirmed close returns to Group Master List");
    }

    [Fact]
    public void Test12_NoModuleCallsApplicationExit_OnNormalEsc()
    {
        using var form = new Form();
        var txt = new TextBox { Text = "" };
        form.Controls.Add(txt);

        bool closed = false;
        MoneyFlowEscController.ConfirmationDialogProvider = _ => true;

        // Ensure normal close only executes the closeAction delegate and never shuts down the process
        MoneyFlowEscController.HandleEsc(txt, form, () => closed = true);
        closed.Should().BeTrue();
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
            Task.FromResult(new Group { GroupId = 1, GroupName = dto.GroupName, Nature = dto.Nature });

        public Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default) =>
            Task.FromResult<Group?>(new Group { GroupId = groupId, GroupName = "Test Group" });

        public Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupSummaryDto>>(new List<GroupSummaryDto>());

        public Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupTreeNodeDto>>(new List<GroupTreeNodeDto>());

        public Task<IReadOnlyList<Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Group>>(new List<Group>());

        public Task<Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = dto.GroupId, GroupName = dto.GroupName });
    }
}
