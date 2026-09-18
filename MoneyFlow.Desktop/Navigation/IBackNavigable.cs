namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Implemented by dynamic workspace forms that manage internal view navigation
/// (such as nested create/alter views inside a master list) before the shell exits to the Gateway/Dashboard.
/// </summary>
public interface IBackNavigable
{
    /// <summary>
    /// Attempts to navigate back within the form's local view hierarchy.
    /// </summary>
    /// <returns>True if internal back navigation was handled; false if the shell should navigate back to Gateway.</returns>
    bool HandleBackNavigation();
}
