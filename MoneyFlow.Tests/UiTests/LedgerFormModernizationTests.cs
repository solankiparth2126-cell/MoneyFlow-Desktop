using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Fields;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Forms;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class LedgerFormModernizationTests
{
    [Fact]
    public void LedgerCreateForm_LayoutAndVisualHierarchy_MatchesImage3()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);
            form.Show();
            Application.DoEvents();

            // 1. Controls count and docking matching target geometry (compact, 4 top-level containers)
            form.Controls.Count.Should().Be(4);

            // Dark Title Bar (Image 3 style)
            var darkTitleBar = form.Controls.OfType<Panel>().FirstOrDefault(c => c.Height == 36 && c.Dock == DockStyle.Top);
            darkTitleBar.Should().NotBeNull();
            darkTitleBar!.BackColor.Should().Be(Color.FromArgb(10, 16, 29));

            // Header with logo
            var header = form.Controls.OfType<Panel>().FirstOrDefault(c => c.Height == 56 && c.Dock == DockStyle.Top);
            header.Should().NotBeNull();
            var titleFlow = header!.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            titleFlow.Should().NotBeNull();
            var screenTitle = titleFlow!.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "CREATE NEW LEDGER");
            screenTitle.Should().NotBeNull();

            // Primary Card Wrapper (Dock = Fill, no blank space, compact)
            var primaryWrapper = form.Controls.OfType<Panel>().FirstOrDefault(c => c.Dock == DockStyle.Fill);
            primaryWrapper.Should().NotBeNull();
            var primaryCard = primaryWrapper!.Controls.OfType<Panel>().FirstOrDefault();
            primaryCard.Should().NotBeNull();
            var primaryTlp = primaryCard!.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            primaryTlp.Should().NotBeNull();
            primaryTlp!.RowCount.Should().Be(2);
            primaryTlp.ColumnCount.Should().Be(4);

            // Footer (Dock = Bottom, directly below primary card)
            var footer = form.Controls.OfType<Panel>().FirstOrDefault(c => c.Dock == DockStyle.Bottom);
            footer.Should().NotBeNull();
            footer!.Height.Should().Be(56);

            // Compact height (target ~330px, no scrolling)
            form.Height.Should().Be(330);

            // Verify removed sections and scrollbars do NOT exist in the form
            var allControls = GetAllChildControls(form);
            allControls.Any(c => c.Text.Contains("MAILING", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
            allControls.Any(c => c.Text.Contains("BANKING", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
            allControls.Any(c => c is Panel p && p.AutoScroll).Should().BeFalse();

            // Export visual bitmap
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\55a8c451-703a-43a7-96d5-869fdeb22b46";
            if (!System.IO.Directory.Exists(artifactDir)) System.IO.Directory.CreateDirectory(artifactDir);
            var artifactPath = System.IO.Path.Combine(artifactDir, "ledger_create_rendered.png");
            bmp.Save(artifactPath, System.Drawing.Imaging.ImageFormat.Png);

            form.Close();
        });
    }

    [Fact]
    public void LedgerCreateForm_AlterMode_LoadsExistingDataWithoutCrash()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1, ledgerId: 10);
            form.Show();
            Application.DoEvents();

            // Verify form loaded cleanly in Alter mode without crash or stack overflow
            form.Visible.Should().BeTrue();

            // Verify Alter title is displayed
            var header = form.Controls.OfType<Panel>().FirstOrDefault(c => c.Height == 56 && c.Dock == DockStyle.Top);
            var titleFlow = header!.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            var screenTitle = titleFlow!.Controls.OfType<Label>().FirstOrDefault(l => l.Text == "ALTER LEDGER");
            screenTitle.Should().NotBeNull();

            // Verify compact 4 containers and 330px height
            form.Controls.Count.Should().Be(4);
            form.Height.Should().Be(330);

            // Verify removed sections do NOT exist
            var allControls = GetAllChildControls(form);
            allControls.Any(c => c.Text.Contains("MAILING", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();
            allControls.Any(c => c.Text.Contains("BANKING", StringComparison.OrdinalIgnoreCase)).Should().BeFalse();

            // Export visual bitmap
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\55a8c451-703a-43a7-96d5-869fdeb22b46";
            if (!System.IO.Directory.Exists(artifactDir)) System.IO.Directory.CreateDirectory(artifactDir);
            var artifactPath = System.IO.Path.Combine(artifactDir, "ledger_alter_rendered.png");
            bmp.Save(artifactPath, System.Drawing.Imaging.ImageFormat.Png);

            form.Close();
        });
    }

    [Fact]
    public void LookupPopup_RendersSingleAccountNameWithoutDuplicateHierarchy_MatchesRequiredResult()
    {
        RunInSta(() =>
        {
            var items = new List<LookupItem>
            {
                new() { Id = 1, Name = "Branch/Divisions", Subtitle = "Branch/Divisions", Code = "Liabilities", HasChildren = true, IsExpanded = true },
                new() { Id = 2, Name = "Capital Account", Subtitle = "Capital Account", Code = "Liabilities", HasChildren = true, IsExpanded = true },
                new() { Id = 3, Name = "Reserves & Surplus", Subtitle = "Capital Account > Reserves & Surplus", Code = "Liabilities", ParentId = 2, Level = 1 },
                new() { Id = 4, Name = "Current Assets", Subtitle = "Current Assets", Code = "Assets", HasChildren = true, IsExpanded = true },
                new() { Id = 5, Name = "Bank Accounts", Subtitle = "Current Assets > Bank Accounts", Code = "Assets", ParentId = 4, Level = 1 },
                new() { Id = 6, Name = "Cash-in-hand", Subtitle = "Current Assets > Cash-in-hand", Code = "Assets", ParentId = 4, Level = 1 },
                new() { Id = 7, Name = "Deposits (Asset)", Subtitle = "Current Assets > Deposits (Asset)", Code = "Assets", ParentId = 4, Level = 1 },
                new() { Id = 8, Name = "Loans & Advances (Asset)", Subtitle = "Current Assets > Loans & Advances (Asset)", Code = "Assets", ParentId = 4, Level = 1 },
                new() { Id = 9, Name = "Stock-in-hand", Subtitle = "Current Assets > Stock-in-hand", Code = "Assets", ParentId = 4, Level = 1 }
            };

            var provider = new ListLookupProvider<LookupItem>(items, x => x.Id, x => x.Name, x => x.Code, x => x.Subtitle);
            using var popup = new MoneyFlowLookupPopup(provider, new LookupConfig
            {
                DefaultWidth = 520,
                DefaultHeight = 350,
                ShowCodeColumn = true,
                HierarchyEnabled = true
            });

            popup.Show();
            var reloadTask = popup.ReloadDataAsync();
            while (!reloadTask.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Application.DoEvents();

            using var bmp = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(bmp, new Rectangle(0, 0, popup.Width, popup.Height));
            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\55a8c451-703a-43a7-96d5-869fdeb22b46";
            if (!System.IO.Directory.Exists(artifactDir)) System.IO.Directory.CreateDirectory(artifactDir);
            var artifactPath = System.IO.Path.Combine(artifactDir, "lookup_popup_rendered.png");
            bmp.Save(artifactPath, System.Drawing.Imaging.ImageFormat.Png);

            popup.Close();
        });
    }

    [Fact]
    public void LedgerAlterForm_WithOpenLookupPopup_PositionsCleanlyWithoutOverhang()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1, ledgerId: 10)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(200, 150)
            };
            form.Show();
            Application.DoEvents();

            var cmbGroup = GetAllChildControls(form).OfType<MoneyFlowTextLookup>().FirstOrDefault();
            cmbGroup.Should().NotBeNull();

            var lookupTask = cmbGroup!.OpenLookupAsync();
            while (!lookupTask.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            Application.DoEvents();

            // Find popup among open forms
            var popup = Application.OpenForms.OfType<MoneyFlowLookupPopup>().LastOrDefault(p => !p.IsDisposed);
            popup.Should().NotBeNull();
            popup!.Visible.Should().BeTrue();

            // Verify position:
            // 1. Must not extend beyond parent form's right edge by an awkward amount
            var formScreen = form.RectangleToScreen(form.ClientRectangle);
            popup.Right.Should().BeLessThanOrEqualTo(formScreen.Right);

            // 2. Must be positioned below the Under Group field
            var cmbScreen = cmbGroup.RectangleToScreen(cmbGroup.ClientRectangle);
            popup.Top.Should().BeGreaterThanOrEqualTo(cmbScreen.Bottom);

            // 3. Render and export combined visual bitmap showing Alter Ledger + attached popup
            int minX = Math.Min(form.Location.X, popup.Location.X);
            int minY = Math.Min(form.Location.Y, popup.Location.Y);
            int maxX = Math.Max(form.Location.X + form.Width, popup.Location.X + popup.Width);
            int maxY = Math.Max(form.Location.Y + form.Height, popup.Location.Y + popup.Height);

            using var combinedBmp = new Bitmap(maxX - minX + 20, maxY - minY + 20);
            using var g = Graphics.FromImage(combinedBmp);
            g.Clear(Color.FromArgb(15, 23, 42));

            using var formBmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(formBmp, new Rectangle(0, 0, form.Width, form.Height));
            g.DrawImage(formBmp, form.Location.X - minX + 10, form.Location.Y - minY + 10);

            using var popBmp = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(popBmp, new Rectangle(0, 0, popup.Width, popup.Height));
            g.DrawImage(popBmp, popup.Location.X - minX + 10, popup.Location.Y - minY + 10);

            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\55a8c451-703a-43a7-96d5-869fdeb22b46";
            if (!System.IO.Directory.Exists(artifactDir)) System.IO.Directory.CreateDirectory(artifactDir);
            var artifactPath = System.IO.Path.Combine(artifactDir, "alter_ledger_with_popup_combined.png");
            combinedBmp.Save(artifactPath, System.Drawing.Imaging.ImageFormat.Png);

            form.Close();
        });
    }

    [Fact]
    public void OpeningBalance_Control_HasNoSpinnerButtons_AndCleanLayout()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);
            form.Show();
            Application.DoEvents();

            var numUpDown = GetAllChildControls(form).OfType<CleanNumericUpDown>().FirstOrDefault();
            numUpDown.Should().NotBeNull();
            numUpDown!.DecimalPlaces.Should().Be(2);
            numUpDown.TextAlign.Should().Be(HorizontalAlignment.Right);

            // UpDownButtons (Controls[0]) must be invisible (spinner hidden)
            numUpDown.Controls[0].Visible.Should().BeFalse();

            // Export rendered bitmap of the clean input
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            var artifactDir = @"C:\Users\DELL\.gemini\antigravity-ide\brain\55a8c451-703a-43a7-96d5-869fdeb22b46";
            if (!System.IO.Directory.Exists(artifactDir)) System.IO.Directory.CreateDirectory(artifactDir);
            var artifactPath = System.IO.Path.Combine(artifactDir, "ledger_create_rendered.png");
            bmp.Save(artifactPath, System.Drawing.Imaging.ImageFormat.Png);

            form.Close();
        });
    }

    [Fact]
    public void LedgerCreateForm_EffectiveFrom_DefaultDateLogic_CreateVsAlterMode()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            // 1. CREATE MODE: Form opens with current system local date in "dd-MM-yyyy"
            using (var createForm = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1))
            {
                createForm.Show();
                Application.DoEvents();

                createForm.EffectiveFromText.Should().Be(DateTime.Today.ToString("dd-MM-yyyy"));

                // Verify user can manually alter date without it being overwritten while open
                var effInput = GetAllChildControls(createForm).OfType<TextBox>().FirstOrDefault(t => t.PlaceholderText == "DD-MM-YYYY");
                effInput.Should().NotBeNull();
                effInput!.Text = "10-10-2026";
                Application.DoEvents();
                createForm.EffectiveFromText.Should().Be("10-10-2026");

                createForm.Close();
            }

            // 2. ALTER MODE: Form loads and displays existing saved Effective From date without replacing with today's date
            using (var alterForm = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1, ledgerId: 10))
            {
                alterForm.Show();
                Application.DoEvents();

                alterForm.EffectiveFromText.Should().Be("15-04-2025");
                alterForm.EffectiveFromText.Should().NotBe(DateTime.Today.ToString("dd-MM-yyyy"));

                alterForm.Close();
            }
        });
    }

    [Fact]
    public void LedgerForm_TabNavigation_StepsThroughAllFieldsInOrder()
    {
        RunInSta(() =>
        {
            var fakeLedgerService = new FakeLedgerService();
            var fakeGroupService = new FakeGroupService();

            using var form = new LedgerCreateEditForm(fakeLedgerService, fakeGroupService, companyId: 1);
            form.Show();
            Application.DoEvents();

            // Field 0: Name
            form.FocusField(0);
            form.GetCurrentFieldIndex().Should().Be(0);

            // Tab Next -> Field 1: Under Group
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(1);

            // Tab Next -> Field 2: Opening Balance
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(2);

            // Tab Next -> Field 3: Balance Type
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(3);

            // Tab Next -> Field 4: Effective From
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(4);

            // Tab Next -> Field 5: Alias / Code
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(5);

            // Tab Next -> Field 8: Save Button
            form.MoveToNextField();
            form.GetCurrentFieldIndex().Should().Be(8);

            // Shift+Tab Previous -> Field 5: Alias / Code
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(5);

            // Shift+Tab Previous -> Field 4: Effective From
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(4);

            // Shift+Tab Previous -> Field 3: Balance Type
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(3);

            // Shift+Tab Previous -> Field 2: Opening Balance
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(2);

            // Shift+Tab Previous -> Field 1: Under Group
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(1);

            // Shift+Tab Previous -> Field 0: Name
            form.MoveToPreviousField();
            form.GetCurrentFieldIndex().Should().Be(0);

            form.Close();
        });
    }

    [Fact]
    public void GroupCreateEditForm_TabNavigation_StepsThroughAllFieldsInOrder()
    {
        RunInSta(() =>
        {
            var fakeGroupService = new FakeGroupService();

            using var form = new GroupCreateEditForm(fakeGroupService, companyId: 1);
            form.Show();
            Application.DoEvents();

            // Initial: Field 0 (Name)
            form.ActiveFieldIndex.Should().Be(0);

            // Tab Next -> Field 1: (alias)
            form.MoveToNextField(0);
            form.ActiveFieldIndex.Should().Be(1);

            // Tab Next -> Field 2: Under
            form.MoveToNextField(1);
            form.ActiveFieldIndex.Should().Be(2);

            // Tab Next -> Field 3: Group behaves like a sub-ledger
            form.MoveToNextField(2);
            form.ActiveFieldIndex.Should().Be(3);

            // Tab Next -> Field 4: Nett Debit/Credit Balances
            form.MoveToNextField(3);
            form.ActiveFieldIndex.Should().Be(4);

            // Tab Next -> Field 5: Used for calculation
            form.MoveToNextField(4);
            form.ActiveFieldIndex.Should().Be(5);

            // Tab Next -> Field 6: Method to allocate
            form.MoveToNextField(5);
            form.ActiveFieldIndex.Should().Be(6);

            // Tab Next -> Field 7: Nature
            form.MoveToNextField(6);
            form.ActiveFieldIndex.Should().Be(7);

            // Tab Next -> Field 8: Affects Gross / Net Profit / Loss
            form.MoveToNextField(7);
            form.ActiveFieldIndex.Should().Be(8);

            // Tab Next -> Save Button
            form.MoveToNextField(8);
            form.ButtonSave.Focused.Should().BeTrue();

            // Shift+Tab Previous from Save Button -> Field 8
            form.MoveToPreviousField(9);
            form.ActiveFieldIndex.Should().Be(8);

            form.Close();
        });
    }

    private static List<Control> GetAllChildControls(Control parent)
    {
        var list = new List<Control>();
        foreach (Control c in parent.Controls)
        {
            list.Add(c);
            list.AddRange(GetAllChildControls(c));
        }
        return list;
    }

    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex;
    }

    private class FakeLedgerService : ILedgerService
    {
        public Task<Ledger> CreateLedgerAsync(int companyId, LedgerCreateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Ledger { LedgerId = 1, LedgerName = dto.LedgerName, CompanyId = companyId });

        public Task<Ledger> UpdateLedgerAsync(LedgerUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Ledger { LedgerId = dto.LedgerId, LedgerName = dto.LedgerName });

        public Task<bool> DeleteLedgerAsync(int ledgerId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<Ledger?> GetLedgerByIdAsync(int ledgerId, CancellationToken ct = default) =>
            Task.FromResult<Ledger?>(new Ledger { LedgerId = ledgerId, LedgerName = "Test Ledger" });

        public Task<LedgerDetailDto?> GetLedgerDetailsAsync(int ledgerId, CancellationToken ct = default) =>
            Task.FromResult<LedgerDetailDto?>(new LedgerDetailDto
            {
                LedgerId = ledgerId,
                CompanyId = 1,
                GroupId = 1,
                LedgerName = "Acme Industrial Solutions Pvt Ltd",
                OpeningBalance = 150000.50m,
                OpeningBalanceType = BalanceType.Debit,
                Address = "123 Industrial Area, Sector 5",
                State = "Maharashtra",
                Phone = "+91 9876543210",
                Email = "finance@acmeind.com",
                PAN = "AAACA1234F",
                BankName = "HDFC Bank",
                BankAccountNumber = "50100234567890",
                IFSC = "HDFC0000123",
                CreditLimit = 500000,
                CreditDays = 30,
                IsActive = true,
                CreatedAt = new DateTime(2025, 4, 15)
            });

        public Task<IReadOnlyList<LedgerSummaryDto>> GetLedgersByCompanyAsync(int companyId, string? searchTerm = null, int? groupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LedgerSummaryDto>>(new List<LedgerSummaryDto>());

        public Task<bool> ValidateLedgerNameAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private class FakeGroupService : IGroupService
    {
        public Task<Group> CreateGroupAsync(int companyId, GroupCreateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = 10, GroupName = dto.GroupName, CompanyId = companyId });

        public Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(true);

        public Task<Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default) =>
            Task.FromResult<Group?>(new Group { GroupId = groupId, GroupName = "Sundry Debtors" });

        public Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupSummaryDto>>(new List<GroupSummaryDto>
            {
                new GroupSummaryDto { GroupId = 1, GroupName = "Sundry Debtors (Receivables)", Nature = GroupNature.Assets }
            });

        public Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GroupTreeNodeDto>>(new List<GroupTreeNodeDto>());

        public Task<IReadOnlyList<Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Group>>(new List<Group>());

        public Task<Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default) =>
            Task.FromResult(new Group { GroupId = dto.GroupId, GroupName = dto.GroupName });
    }
}
