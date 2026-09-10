using Orivy.Controls;
using System;
using System.Collections.ObjectModel;

namespace Orivy.Collections;

public sealed class RowDefinitionCollection : Collection<RowDefinition>
{
    private readonly Grid _owner;

    internal RowDefinitionCollection(Grid owner)
    {
        _owner = owner;
    }

    protected override void InsertItem(int index, RowDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Owner = _owner;
        base.InsertItem(index, item);
        _owner.OnDefinitionsChanged();
    }

    protected override void SetItem(int index, RowDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Owner = _owner;
        base.SetItem(index, item);
        _owner.OnDefinitionsChanged();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.OnDefinitionsChanged();
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
            item.Owner = null;
        base.ClearItems();
        _owner.OnDefinitionsChanged();
    }
}
