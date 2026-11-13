using System;
using System.Collections.Concurrent;
using System.Windows.Forms;
using MoneyFlow.Desktop.Controls.Dropdown;
using MoneyFlow.Desktop.Controls.Dropdown.Events;
using MoneyFlow.Desktop.Controls.Dropdown.Models;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Services;

/// <summary>
/// Centralized master jump and navigation coordinator for MoneyFlow Tally dropdowns.
/// Decouples UI dropdown controls from direct form references and manages Alt+C / Alt+M routing.
/// </summary>
public class MasterNavigationService
{
    private static readonly ConcurrentDictionary<MasterType, Func<IWin32Window?, DropdownItemModel?>> _createHandlers = new();
    private static readonly ConcurrentDictionary<MasterType, Action<IWin32Window?>> _moreHandlers = new();

    private readonly INavigationService? _navigationService;

    public MasterNavigationService(INavigationService? navigationService = null)
    {
        _navigationService = navigationService;
    }

    /// <summary>
    /// Registers a custom creation callback for a specific master type.
    /// </summary>
    public static void RegisterCreateHandler(MasterType masterType, Func<IWin32Window?, DropdownItemModel?> handler)
    {
        _createHandlers[masterType] = handler;
    }

    /// <summary>
    /// Registers a custom 'Show More' callback for a specific master type.
    /// </summary>
    public static void RegisterMoreHandler(MasterType masterType, Action<IWin32Window?> handler)
    {
        _moreHandlers[masterType] = handler;
    }

    /// <summary>
    /// Attaches automated MasterNavigationService routing to any MoneyFlowTallyDropdown instance.
    /// </summary>
    public void BindDropdown(MoneyFlowTallyDropdown dropdown, IWin32Window? owner = null)
    {
        if (dropdown == null) return;

        dropdown.CreateRequested += (s, e) =>
        {
            if (e.Handled) return;
            var created = OpenCreateMaster(e.MasterType, owner);
            if (created != null)
            {
                e.CreatedItem = created;
                e.Handled = true;
            }
        };

        dropdown.ShowMoreRequested += (s, e) =>
        {
            if (e.Handled) return;
            OpenMasterList(e.MasterType, owner);
            e.Handled = true;
        };
    }

    /// <summary>
    /// Dispatches master creation window based on MasterType.
    /// </summary>
    public DropdownItemModel? OpenCreateMaster(MasterType masterType, IWin32Window? owner = null)
    {
        // 1. Check custom registered handlers
        if (_createHandlers.TryGetValue(masterType, out var customHandler))
        {
            return customHandler(owner);
        }

        // 2. Built-in defaults
        switch (masterType)
        {
            case MasterType.Country:
                return PromptQuickCreate("Country", "Enter new Country name:", "Global", owner);

            case MasterType.State:
                return PromptQuickCreate("State / Province", "Enter new State / Territory name:", "State", owner);

            case MasterType.Ledger:
                if (_navigationService != null)
                {
                    _navigationService.OpenLedgerList(owner);
                }
                break;

            case MasterType.Group:
                if (_navigationService != null)
                {
                    _navigationService.OpenGroupList(owner);
                }
                break;

            case MasterType.FinancialYear:
                if (_navigationService != null)
                {
                    _navigationService.OpenFinancialYearList(owner);
                }
                break;

            default:
                MoneyFlowMessageBox.Show(
                    owner,
                    $"Direct master creation for {masterType} is accessible via ERP Masters navigation.",
                    "Master Creation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;
        }

        return null;
    }

    /// <summary>
    /// Dispatches full master list view based on MasterType.
    /// </summary>
    public void OpenMasterList(MasterType masterType, IWin32Window? owner = null)
    {
        if (_moreHandlers.TryGetValue(masterType, out var customHandler))
        {
            customHandler(owner);
            return;
        }

        switch (masterType)
        {
            case MasterType.Ledger:
                _navigationService?.OpenLedgerList(owner);
                break;

            case MasterType.Group:
                _navigationService?.OpenGroupList(owner);
                break;

            case MasterType.FinancialYear:
                _navigationService?.OpenFinancialYearList(owner);
                break;

            case MasterType.Company:
                _navigationService?.OpenCompanyList(owner);
                break;

            default:
                MoneyFlowMessageBox.Show(
                    owner,
                    $"Full directory for {masterType} is displayed with search filtering in the dropdown.",
                    $"{masterType} Directory",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;
        }
    }

    private static DropdownItemModel? PromptQuickCreate(string title, string prompt, string defaultCode, IWin32Window? owner)
    {
        using var promptForm = new Form
        {
            Text = $"New {title} — Quick Master Creation",
            Size = new System.Drawing.Size(380, 160),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = System.Drawing.Color.FromArgb(248, 250, 252)
        };

        var lbl = new Label { Text = prompt, Location = new System.Drawing.Point(16, 14), AutoSize = true };
        var txt = new TextBox { Location = new System.Drawing.Point(16, 38), Width = 330, Font = new System.Drawing.Font("Segoe UI", 9.5F) };
        var btnOk = new Button { Text = "Save Master", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(160, 80), Size = new System.Drawing.Size(100, 28) };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(268, 80), Size = new System.Drawing.Size(78, 28) };

        promptForm.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
        promptForm.AcceptButton = btnOk;
        promptForm.CancelButton = btnCancel;

        if (promptForm.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
        {
            string clean = txt.Text.Trim();
            return new DropdownItemModel
            {
                Id = clean,
                Name = clean,
                Code = defaultCode,
                Status = "Configured"
            };
        }

        return null;
    }
}
