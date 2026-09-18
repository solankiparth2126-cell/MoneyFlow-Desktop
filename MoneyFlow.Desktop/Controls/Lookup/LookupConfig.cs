namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Configuration and behavior options for a universal lookup instance.
/// </summary>
public class LookupConfig
{
    public string Title { get; set; } = "LIST OF ITEMS";
    public string? Placeholder { get; set; } = "Select...";
    public bool AllowCreate { get; set; }
    public string CreateButtonText { get; set; } = "New";
    public bool AllowShowMore { get; set; }
    public bool HierarchyEnabled { get; set; }
    public bool AllowClear { get; set; }
    public string ClearItemText { get; set; } = "◆ Not Applicable";
    public int DefaultWidth { get; set; } = 540;
    public int DefaultHeight { get; set; } = 420;
    public string DescriptionColumnHeader { get; set; } = "Description";
    public string CodeColumnHeader { get; set; } = "Code";
    public bool ShowCodeColumn { get; set; } = true;
}
