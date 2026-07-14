using Orivy;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// Toolbox: control types discovered dynamically from Orivy.Controls, grouped by category with
/// search and per-entry description tooltips. Double-click (or the Add button in the shell) places
/// the selected entry on the canvas.
/// </summary>
public sealed class ToolboxPanel : Element
{
    private readonly IReadOnlyList<ControlEntry> _all;
    private readonly List<ControlEntry> _visible = new();
    private readonly GridList _list;
    private readonly TextBox _search;

    /// <summary>Raised when the user requests placing an entry (double-click).</summary>
    public event Action<ControlEntry>? PlaceRequested;

    public ToolboxPanel()
    {
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        _all = ControlCatalog.Discover();

        _search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 36,
            Margin = new Thickness(0, 0, 0, 8),
            PlaceholderText = "Search controls…",
        };
        _search.TextChanged += (_, _) => Refill();

        _list = new GridList
        {
            Dock = DockStyle.Fill,
            HeaderVisible = false,
            GroupingEnabled = true,
            ShowGridLines = false,
            MultiSelect = false,
            FullRowSelect = true,
            RowHeight = 30,
            GroupHeaderHeight = 26,
            Radius = new Radius(10),
            Border = new Thickness(1),
        };
        _list.Columns.Add(new GridListColumn { Name = "name", Text = "Control", SizeMode = GridListColumnSizeMode.Fill, Sortable = false });
        _list.CellClick += (_, _) => { };
        _list.MouseDoubleClick += (_, _) =>
        {
            if (SelectedEntry is { } entry)
                PlaceRequested?.Invoke(entry);
        };

        Controls.Add(_list);
        Controls.Add(_search);

        Refill();
    }

    public ControlEntry? SelectedEntry
    {
        get
        {
            var index = _list.SelectedIndex;
            return index >= 0 && index < _visible.Count ? _visible[index] : null;
        }
    }

    private void Refill()
    {
        var filter = (_search.Text ?? string.Empty).Trim();
        _visible.Clear();
        _list.Items.Clear();

        foreach (var entry in _all)
        {
            if (filter.Length > 0 &&
                entry.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                entry.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            _visible.Add(entry);
            var item = new GridListItem(entry.DisplayName)
            {
                GroupKey = entry.Category,
                GroupText = entry.Category,
                ToolTipText = entry.Description,
                Tag = entry,
            };
            _list.Items.Add(item);
        }

        if (_visible.Count > 0)
            _list.SelectedIndex = 0;
    }
}
