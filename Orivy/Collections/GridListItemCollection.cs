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

    // ── Keyed access by GridListItem.Name (mirrors WinForms ListView.ListViewItemCollection) ──

    /// <summary>
    /// Gets the first item whose <see cref="GridListItem.Name"/> matches <paramref name="key"/>
    /// (case-insensitive), or null if none. Does not throw for unknown keys.
    /// </summary>
    public GridListItem? this[string key]
    {
        get
        {
            var index = IndexOfKey(key);
            return index >= 0 ? this[index] : null;
        }
    }

    /// <summary>Returns true if an item with the given <see cref="GridListItem.Name"/> exists (case-insensitive).</summary>
    public bool ContainsKey(string key) => IndexOfKey(key) >= 0;

    /// <summary>Returns the index of the first item with the given Name (case-insensitive), or -1.</summary>
    public int IndexOfKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return -1;

        for (var i = 0; i < Count; i++)
            if (string.Equals(this[i].Name, key, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }

    /// <summary>Removes the first item with the given Name (case-insensitive), if present.</summary>
    public void RemoveByKey(string key)
    {
        var index = IndexOfKey(key);
        if (index >= 0)
            RemoveAt(index);
    }

    /// <summary>
    /// Moves the item at <paramref name="fromIndex"/> to <paramref name="toIndex"/>. Note: this does
    /// not adjust the owning grid's selection — prefer <see cref="GridList.MoveItem(int, int)"/>
    /// when the move is user-driven.
    /// </summary>
    public void Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Count || toIndex < 0 || toIndex >= Count || fromIndex == toIndex)
            return;

        var item = this[fromIndex];
        RemoveAt(fromIndex);
        Insert(toIndex, item);
    }

    /// <summary>Adds an item with the given key (Name) and a single text cell, and returns it.</summary>
    public GridListItem Add(string key, string text)
    {
        var item = new GridListItem(text) { Name = key };
        Add(item);
        return item;
    }
}