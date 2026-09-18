using System;
using System.Collections.Generic;
using System.Drawing;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Represents an action button displayed in the dynamic footer operations rail.
/// </summary>
public record FooterActionItem(
    string KeyText,
    string ActionText,
    Color BadgeColor,
    Action Action,
    bool IsActive = false
);

/// <summary>
/// Optional interface implemented by child forms that provide specialized footer actions.
/// If not implemented, the ERP application shell provides default contextual shortcuts based on module category.
/// </summary>
public interface IFooterActionProvider
{
    IReadOnlyList<FooterActionItem> GetFooterActions();
}
