using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Orivy.Collections;

public sealed class GridListItemCollection : Collection<GridListItem>
{
    private readonly GridList _owner;

    internal GridListItemCollection(GridList owner)
    {
        _owner = owner;
    }

    protected override void InsertItem(int index, GridListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachOwner(_owner);
        base.InsertItem(index, item);
        _owner.OnItemsChanged(layoutAffected: true);
    }

    protected override void SetItem(int index, GridListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachOwner(_owner);
        base.SetItem(index, item);
        _owner.OnItemsChanged(layoutAffected: true);
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        _owner.OnItemsChanged(layoutAffected: true);
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        _owner.ClearSelection();
        _owner.OnItemsChanged(layoutAffected: true);
    }

    public void AddRange(IEnumerable<GridListItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            Add(item);
    }
}