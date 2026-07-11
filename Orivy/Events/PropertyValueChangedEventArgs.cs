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
    {
        ChangedItem = changedItem;
        OldValue = oldValue;
    }

    /// <summary>The property descriptor whose value changed.</summary>
    public PropertyDescriptor? ChangedItem { get; }

    /// <summary>The value of the property before it was changed.</summary>
    public object? OldValue { get; }
}
