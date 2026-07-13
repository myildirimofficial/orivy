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

    /// <summary>Adds a text cell and returns it (WinForms ListViewItem.SubItems.Add ergonomics).</summary>
    public GridListCell Add(string? text)
    {
        var cell = new GridListCell { Text = text ?? string.Empty };
        Add(cell);
        return cell;
    }

    /// <summary>Adds a cell for an arbitrary value; its text is the value's string representation.</summary>
    public GridListCell Add(object? value)
    {
        var cell = new GridListCell { Value = value, Text = value?.ToString() ?? string.Empty };
        Add(cell);
        return cell;
    }

    /// <summary>Adds one text cell per string (WinForms SubItems.AddRange(string[]) ergonomics).</summary>
    public void AddRange(IEnumerable<string?> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        foreach (var text in texts)
            Add(text);
    }

    public void AddRange(params string?[] texts) => AddRange((IEnumerable<string?>)texts);
}