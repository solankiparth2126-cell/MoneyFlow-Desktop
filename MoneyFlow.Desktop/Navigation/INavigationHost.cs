using System;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Defines the contract for an ERP application shell hosting dynamic module content.
/// </summary>
public interface INavigationHost
{
    /// <summary>
    /// Dynamically loads a module form inside the central dynamic workspace panel.
    /// </summary>
    void ShowInWorkspace(Func<Form> formFactory, string moduleKey, string moduleTitle);

    /// <summary>
    /// Returns the central dynamic workspace to the Gateway of Accounting.
    /// </summary>
    void ReturnToGateway();

    /// <summary>
    /// Navigates back to the previous module or Gateway.
    /// </summary>
    bool NavigateBack();

    /// <summary>
    /// Key of the currently loaded module (e.g., "Gateway", "Payment", "Receipt", "DayBook").
    /// </summary>
    string CurrentModuleKey { get; }

    /// <summary>
    /// Indicates whether the application shell is currently displaying the Gateway of Accounting.
    /// </summary>
    bool IsOnGateway { get; }

    /// <summary>
    /// Contextual shortcut actions currently presented in the dynamic footer operations rail.
    /// </summary>
    IReadOnlyList<FooterActionItem> CurrentFooterActions { get; }
}
