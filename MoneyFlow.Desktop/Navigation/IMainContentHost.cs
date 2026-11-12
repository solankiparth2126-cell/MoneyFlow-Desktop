using System.Windows.Forms;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Host interface for embedding screens, forms, and menus directly inside MainForm's workspace panel.
/// </summary>
public interface IMainContentHost
{
    /// <summary>
    /// Embeds and displays a view/form inside the main workspace container panel.
    /// </summary>
    void ShowView(Form viewForm);

    /// <summary>
    /// Closes the currently active embedded view and returns to the previous view or Gateway.
    /// </summary>
    void CloseCurrentView();

    /// <summary>
    /// Whether an embedded child view is currently active over the Gateway of Accounting.
    /// </summary>
    bool HasActiveChildView { get; }

    /// <summary>
    /// Clears all stacked child views and returns immediately to the Gateway of Accounting.
    /// </summary>
    void ReturnToGateway();
}
