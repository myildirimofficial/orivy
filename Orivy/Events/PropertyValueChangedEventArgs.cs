using System;
using System.ComponentModel;

namespace Orivy;

/// <summary>
/// Provides data for the PropertyGrid.PropertyValueChanged event. Mirrors
/// System.Windows.Forms.PropertyValueChangedEventArgs.
/// </summary>
public sealed class PropertyValueChangedEventArgs : EventArgs
{
    public PropertyValueChangedEventArgs(PropertyDescriptor? changedItem, object? oldValue)
        : this(changedItem, oldValue, changedItem, oldValue)
    {
    }

    public PropertyValueChangedEventArgs(
        PropertyDescriptor? changedItem,
        object? oldValue,
        PropertyDescriptor? persistedItem,
        object? persistedOldValue)
    {
        ChangedItem = changedItem;
        OldValue = oldValue;
        PersistedItem = persistedItem ?? changedItem;
        PersistedOldValue = persistedItem != null ? persistedOldValue : oldValue;
    }

    /// <summary>The property descriptor whose value changed. Nested edits report the child descriptor.</summary>
    public PropertyDescriptor? ChangedItem { get; }

    /// <summary>The value of <see cref="ChangedItem"/> before it was changed.</summary>
    public object? OldValue { get; }

    /// <summary>Property on the grid's selected object to undo and persist. For a nested edit this is the root property (for example <c>Margin</c>, not <c>Left</c>).</summary>
    public PropertyDescriptor? PersistedItem { get; }

    /// <summary>Value of <see cref="PersistedItem"/> before the edit.</summary>
    public object? PersistedOldValue { get; }
}
