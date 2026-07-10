namespace Orivy;

/// <summary>
/// Specifies the selection behavior of a <see cref="Orivy.Controls.ListBox"/>.
/// Mirrors System.Windows.Forms.SelectionMode.
/// </summary>
public enum SelectionMode
{
    /// <summary>No items can be selected.</summary>
    None,

    /// <summary>Only one item can be selected at a time.</summary>
    One,

    /// <summary>Multiple items can be selected; a click toggles an item's selection.</summary>
    MultiSimple,

    /// <summary>
    /// Multiple items can be selected; Shift and Ctrl extend/toggle the selection while a plain
    /// click selects a single item (Windows Explorer style).
    /// </summary>
    MultiExtended
}
