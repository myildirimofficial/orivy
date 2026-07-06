using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Orivy.Collections;

public sealed class GridListCellCollection : Collection<GridListCell>
{
    private readonly GridListItem _owner;

    internal GridListCellCollection(GridListItem owner)
    {
        _owner = owner;
    }

    protected override void InsertItem(int index, GridListCell item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachParent(_owner);
        base.InsertItem(index, item);
        _owner.NotifyCellChanged(layoutAffected: false);
    }

    protected override void SetItem(int index, GridListCell item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachParent(_owner);
        base.SetItem(index, item);
        _owner.NotifyCellChanged(layoutAffected: false);
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        _owner.NotifyCellChanged(layoutAffected: false);
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        _owner.NotifyCellChanged(layoutAffected: false);
    }

    public void AddRange(IEnumerable<GridListCell> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            Add(item);
    }
}