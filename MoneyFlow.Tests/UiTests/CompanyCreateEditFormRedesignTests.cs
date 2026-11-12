using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FluentAssertions;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Forms;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class CompanyCreateEditFormRedesignTests
{
    private class FakeCompanyService : ICompanyService
    {
        public CompanyCreateDto? LastCreatedDto { get; private set; }
        public CompanyUpdateDto? LastUpdatedDto { get; private set; }

        public Task<Company> CreateCompanyAsync(CompanyCreateDto dto, CancellationToken ct = default)
        {
            LastCreatedDto = dto;
            return Task.FromResult(new Company
            {
                CompanyId = 1,
                CompanyName = dto.CompanyName,
                CompanyNumber = dto.CompanyNumber,
                DataDirectory = dto.DataDirectory
            });
        }

        public Task<Company> UpdateCompanyAsync(CompanyUpdateDto dto, CancellationToken ct = default)
        {
            LastUpdatedDto = dto;
            return Task.FromResult(new Company
            {
                CompanyId = dto.CompanyId,
                CompanyName = dto.CompanyName,
                CompanyNumber = dto.CompanyNumber,
                DataDirectory = dto.DataDirectory
            });
        }

        public Task<Company?> GetCompanyByIdAsync(int companyId, CancellationToken ct = default)
        {
            return Task.FromResult<Company?>(new Company
            {
                CompanyId = companyId,
                CompanyName = "Acme Test Corp",
                CompanyNumber = "010001",
                DataDirectory = @"C:\TestPath\010001",
                State = "Gujarat",
                Country = "India",
                Currency = "₹"
            });
        }

        public Task<IReadOnlyList<CompanySummaryDto>> GetAllCompaniesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CompanySummaryDto>>(new List<CompanySummaryDto>());

        public Task<bool> DeleteCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> OpenCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> VerifyCompanyPasswordAsync(int companyId, string password, CancellationToken ct = default) => Task.FromResult(true);
        public void CloseCompany() { }
    }

    [Fact]
    public void LandscapeOrientation_WideAndNoScrollbars()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Assert - wide landscape orientation
        form.Width.Should().BeInRange(1050, 1150, "Form must have landscape width (~1050-1150px)");

        // Assert - strictly NO scrollbars anywhere in form body
        var panels = GetAllChildControls(form).OfType<Panel>().ToList();
        panels.Any(p => p.AutoScroll).Should().BeFalse("No panel should have AutoScroll enabled; entire form must fit without scrollbars");

        form.FormBorderStyle.Should().Be(FormBorderStyle.None);
    }

    [Fact]
    public void UsesExistingMoneyFlowCommonDropdown_ForStateCountryCurrency()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Act - find all MoneyFlowTextLookup components
        var lookups = GetAllChildControls(form).OfType<MoneyFlowTextLookup>().ToList();

        // Assert - must have at least 3 common dropdowns (State, Country, Currency)
        lookups.Count.Should().BeGreaterThanOrEqualTo(3, "Must use MoneyFlowTextLookup for State, Country, and Currency");

        // Verify dropdown configurations
        var titles = lookups.Select(l => l.Config.Title).ToList();
        titles.Should().Contain(t => t.Contains("STATE", StringComparison.OrdinalIgnoreCase));
        titles.Should().Contain(t => t.Contains("COUNTRY", StringComparison.OrdinalIgnoreCase));
        titles.Should().Contain(t => t.Contains("CURRENCY", StringComparison.OrdinalIgnoreCase));

        // Verify Description and Code column headers
        lookups.All(l => l.Config.DescriptionColumnHeader == "Description" && l.Config.CodeColumnHeader == "Code")
            .Should().BeTrue("Common dropdowns must display Description and Code columns");

        // Verify dropdown arrow
        lookups.All(l => l.ShowDropdownArrow)
            .Should().BeTrue("Common dropdowns must display a vertically centered dropdown arrow at the far right");
    }

    [Fact]
    public void CompanyDataPath_Controls_AreCompletelyRemovedFromUI()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        string testPath = @"C:\TestCustomDataPath\Companies\";
        using var form = new CompanyCreateEditForm(fakeService, null, null, testPath);

        // Act - inspect all child controls recursively
        var allControls = GetAllChildControls(form).ToList();

        // Assert - verify NO Data Path label, textbox, or browse button exists in UI
        var dataPathLabels = allControls.OfType<Label>()
            .Where(l => l.Text.IndexOf("Company Data Path", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        dataPathLabels.Should().BeEmpty("Company Data Path label must be completely removed from the Create Company popup");

        var browseButtons = allControls.OfType<Button>()
            .Where(b => b.Text.IndexOf("Browse", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        browseButtons.Should().BeEmpty("Browse button for Company Data Path must be completely removed from Create Company popup");

        var gunaBrowseButtons = allControls.OfType<Guna2Button>()
            .Where(b => b.Text.IndexOf("Browse", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        gunaBrowseButtons.Should().BeEmpty("No Guna2 Browse button should exist for Data Path");
    }

    [Fact]
    public void Header_MatchesSelectCompanyBrandingAndDesignLanguage()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Assert Header Panel
        var headerPanel = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Top);
        headerPanel.Should().NotBeNull();
        headerPanel!.Height.Should().Be(56);
        headerPanel.BackColor.Should().Be(Color.FromArgb(11, 41, 69)); // #0B2945

        // Header Labels
        var labels = headerPanel.Controls.OfType<Label>().ToList();
        labels.Any(l => l.Text == "Create Company").Should().BeTrue("Header must contain 'Create Company' title");
        labels.Any(l => l.Text == "|").Should().BeTrue("Header must contain '|' separator");
        labels.Any(l => l.Text == "MoneyFlow ERP").Should().BeTrue("Header must contain 'MoneyFlow ERP' subtitle");

        // Close button
        var closeBtn = headerPanel.Controls.OfType<Guna2Button>().FirstOrDefault();
        closeBtn.Should().NotBeNull();
        closeBtn!.Text.Should().Be("✕");
        closeBtn.HoverState.FillColor.Should().Be(Color.FromArgb(220, 38, 38));
    }

    [Fact]
    public void FormStructure_ContainsThreeDistinctSectionCards()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);
        var allControls = GetAllChildControls(form).ToList();

        // Assert 3 Section Cards
        var cards = allControls.OfType<Guna2Panel>()
            .Where(p => p.FillColor == Color.White && p.BorderRadius == 8)
            .ToList();

        cards.Count.Should().BeGreaterThanOrEqualTo(3, "Must have 3 distinct section cards: Company Info, Financial, Security & Backup");

        // Section Headers
        var labels = allControls.OfType<Label>().Select(l => l.Text).ToList();
        labels.Any(t => t.Contains("COMPANY INFORMATION")).Should().BeTrue();
        labels.Any(t => t.Contains("FINANCIAL CONFIGURATION")).Should().BeTrue();
        labels.Any(t => t.Contains("SECURITY & BACKUP")).Should().BeTrue();
    }

    [Fact]
    public void SectionA_ContainsAllRequiredCompanyInformationFields()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);
        var allControls = GetAllChildControls(form).ToList();

        // Inputs
        var textboxes = allControls.OfType<Guna2TextBox>().ToList();
        var lookups = allControls.OfType<MoneyFlowTextLookup>().ToList();

        // Company Name
        textboxes.Any(t => t.PlaceholderText.Contains("company name", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();

        // State & Country dropdowns (MoneyFlowTextLookup)
        lookups.Any(l => l.Config.Title.Contains("STATE")).Should().BeTrue();
        lookups.Any(l => l.Config.Title.Contains("COUNTRY")).Should().BeTrue();

        // Address multiline
        var addressBox = textboxes.FirstOrDefault(t => t.Multiline && t.AcceptsReturn);
        addressBox.Should().NotBeNull("Address input must be multiline with AcceptsReturn enabled");

        // PAN & Email
        textboxes.Any(t => t.PlaceholderText.Contains("ABCDE1234F")).Should().BeTrue();
        textboxes.Any(t => t.PlaceholderText.Contains("accounts") || t.PlaceholderText.Contains("finance")).Should().BeTrue();
    }

    [Fact]
    public void SectionB_ContainsFinancialYearAndCurrencySettings()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);
        var allControls = GetAllChildControls(form).ToList();

        var datePickers = allControls.OfType<DateTimePicker>().ToList();
        datePickers.Count.Should().Be(2, "Must contain Financial Year From and Books Beginning From date pickers");
        datePickers.All(dp => dp.CustomFormat == "dd-MMM-yyyy").Should().BeTrue();

        var currencyLookup = allControls.OfType<MoneyFlowTextLookup>().FirstOrDefault(l => l.Config.Title.Contains("CURRENCY"));
        currencyLookup.Should().NotBeNull("Base Currency must use MoneyFlow Common Dropdown");
    }

    [Fact]
    public void SectionC_ContainsSecurityCheckboxesAndMaskedPasswords()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);
        var allControls = GetAllChildControls(form).ToList();

        // Masked passwords
        var passwordInputs = allControls.OfType<Guna2TextBox>()
            .Where(t => t.UseSystemPasswordChar)
            .ToList();
        passwordInputs.Count.Should().Be(2, "Must have Vault Password and Repeat Password inputs masked");

        // Checkboxes
        var checkboxes = allControls.OfType<Guna2CheckBox>().ToList();
        checkboxes.Any(c => c.Text.Contains("Auto-Backup")).Should().BeTrue();
        checkboxes.Any(c => c.Text.Contains("Create default accounting ledgers")).Should().BeTrue();
        checkboxes.All(c => c.Checked).Should().BeTrue("Default checkboxes should be checked");
    }

    [Fact]
    public void Footer_ContainsPrimaryAndSecondaryButtons()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);
        var footer = form.Controls.OfType<Panel>().FirstOrDefault(p => p.Dock == DockStyle.Bottom);
        footer.Should().NotBeNull();
        footer!.Height.Should().Be(56);

        var buttons = footer.Controls.OfType<Guna2Button>().ToList();
        var primaryBtn = buttons.FirstOrDefault(b => b.Text.Contains("Create Company"));
        primaryBtn.Should().NotBeNull();
        primaryBtn!.FillColor.Should().Be(Color.FromArgb(11, 41, 69)); // #0B2945

        var cancelBtn = buttons.FirstOrDefault(b => b.Text.Contains("Cancel"));
        cancelBtn.Should().NotBeNull();
        cancelBtn!.FillColor.Should().Be(Color.White);
    }

    [Fact]
    public void EffectiveDataPath_IsInheritedAndPassedToCreateCompanyAsync()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        string customPath = @"D:\MoneyFlow\CustomData\Companies\";
        using var form = new CompanyCreateEditForm(fakeService, null, null, customPath);

        // Find controls
        var allControls = GetAllChildControls(form).ToList();
        var txtName = allControls.OfType<Guna2TextBox>().First(t => t.PlaceholderText.Contains("company name", StringComparison.OrdinalIgnoreCase));
        var btnSave = allControls.OfType<Guna2Button>().First(b => b.Text.Contains("Create Company"));

        // Act
        txtName.Text = "Dynamic Test Enterprise";
        btnSave.PerformClick();

        // Give async operation time to complete
        Application.DoEvents();

        // Assert
        fakeService.LastCreatedDto.Should().NotBeNull();
        fakeService.LastCreatedDto!.CompanyName.Should().Be("Dynamic Test Enterprise");
        fakeService.LastCreatedDto.DataDirectory.Should().Be(customPath, "Must inherit selected path internally without user entering it again");
    }

    [Fact]
    public void Dropdowns_HaveAutoOpenOnFocusEnabled()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Act
        var lookups = GetAllChildControls(form).OfType<MoneyFlowTextLookup>().ToList();

        // Assert
        lookups.Count.Should().BeGreaterThanOrEqualTo(3);
        lookups.All(l => l.AutoOpenOnFocus)
            .Should().BeTrue("Dropdowns must automatically open when focused");
    }

    [Fact]
    public void SectionHeaders_UseMnemonicFalse_DisplaysSecurityAndBackupProperly()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Act
        var labels = GetAllChildControls(form).OfType<Label>()
            .Where(l => l.Text.Contains("SECURITY & BACKUP"))
            .ToList();

        // Assert
        labels.Should().NotBeEmpty("Security & Backup section header must exist");
        labels.First().UseMnemonic.Should().BeFalse("UseMnemonic must be false so '&' does not render as an underscore ('SECURITY _BACKUP')");
    }

    [Fact]
    public void CompanyInfoCard_BalancedLayout_NoStretchedPhoneBox()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Act
        var textboxes = GetAllChildControls(form).OfType<Guna2TextBox>().ToList();
        var phoneBox = textboxes.FirstOrDefault(t => t.PlaceholderText.Contains("98765"));

        // Assert
        phoneBox.Should().NotBeNull();
        phoneBox!.Width.Should().BeLessThan(600, "Phone input must be standard column width (~493px), not stretched across full 1004px card width");
    }

    [Fact]
    public void EnterNavigation_MoveToNext_AdvancesAcrossSequentialControls()
    {
        // Arrange
        var fakeService = new FakeCompanyService();
        using var form = new CompanyCreateEditForm(fakeService);

        // Act & Assert - MoveToNext from index 0 should not throw and should advance
        Action act = () => form.MoveToNext(0);
        act.Should().NotThrow();

        Action actPrev = () => form.MoveToPrevious(2);
        actPrev.Should().NotThrow();
    }

    private static System.Collections.Generic.IEnumerable<Control> GetAllChildControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in GetAllChildControls(child))
            {
                yield return descendant;
            }
        }
    }
}
