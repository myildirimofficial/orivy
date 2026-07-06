using Orivy.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Orivy.Collections;

public sealed class GridListColumnCollection : Collection<GridListColumn>
{
    private readonly GridList _owner;

    internal GridListColumnCollection(GridList owner)
    {
        _owner = owner;
    }

    protected override void InsertItem(int index, GridListColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachOwner(_owner);
        base.InsertItem(index, item);
        _owner.OnColumnsChanged(layoutAffected: true);
    }

    protected override void SetItem(int index, GridListColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.AttachOwner(_owner);
        base.SetItem(index, item);
        _owner.OnColumnsChanged(layoutAffected: true);
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        _owner.OnColumnsChanged(layoutAffected: true);
    }

    protected override void ClearItems()
    {
        base.ClearItems();
        _owner.OnColumnsChanged(layoutAffected: true);
    }

    public void AddRange(IEnumerable<GridListColumn> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
            Add(item);
    }
}
