using System;

namespace Orivy;

/// <summary>
/// Provides data for the CheckedListBox.ItemCheck event. Mirrors
/// System.Windows.Forms.ItemCheckEventArgs.
/// </summary>
public sealed class ItemCheckEventArgs : EventArgs
{
    public ItemCheckEventArgs(int index, CheckState newValue, CheckState currentValue)
    {
        Index = index;
        NewValue = newValue;
        CurrentValue = currentValue;
    }

    /// <summary>The zero-based index of the item whose check state is changing.</summary>
    public int Index { get; }

    /// <summary>The new check state. Set this to override or cancel (assign CurrentValue) the change.</summary>
    public CheckState NewValue { get; set; }

    /// <summary>The current check state before the change.</summary>
    public CheckState CurrentValue { get; }
}
