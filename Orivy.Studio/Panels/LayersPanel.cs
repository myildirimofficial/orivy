using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// Layers (outline) panel: designed controls listed topmost-first with per-row visibility and lock
/// toggles, multi-selection synced with the canvas, and Z-reorder buttons.
/// </summary>
public sealed class LayersPanel : Element
{
    private DesignSurface _surface;
    private readonly GridList _list;
    private readonly List<ElementBase> _rows = new();
    private bool _syncing;

    public LayersPanel(DesignSurface surface)
    {
        _surface = surface;

        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        var buttons = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Thickness(0, 6, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        var toFront = new Button { Text = "▲ Front", Dock = DockStyle.Left, Width = 92, Margin = new Thickness(0, 0, 6, 0) };
        var toBack = new Button { Text = "▼ Back", Dock = DockStyle.Left, Width = 92 };
        buttons.Controls.Add(toBack);
        buttons.Controls.Add(toFront);

        _list = new GridList
        {
            Dock = DockStyle.Fill,
            HeaderVisible = false,
            GroupingEnabled = false,
            ShowGridLines = false,
            MultiSelect = true,
            FullRowSelect = true,
            RowHeight = 28,
            Radius = new Radius(10),
            Border = new Thickness(1),
        };
        _list.Columns.Add(new GridListColumn { Name = "name", Text = "Layer", SizeMode = GridListColumnSizeMode.Fill, Sortable = false });
        _list.Columns.Add(new GridListColumn { Name = "eye", Text = "👁", Width = 40f, MinWidth = 36f, ShowCheckBox = true, Sortable = false, CellTextAlign = ContentAlignment.MiddleCenter });
        _list.Columns.Add(new GridListColumn { Name = "lock", Text = "🔒", Width = 40f, MinWidth = 36f, ShowCheckBox = true, Sortable = false, CellTextAlign = ContentAlignment.MiddleCenter });

        Controls.Add(_list);
        Controls.Add(buttons);

        _list.SelectionChanged += (_, _) =>
        {
            if (_syncing)
                return;

            var picked = _list.SelectedIndices
                .Where(i => i >= 0 && i < _rows.Count)
                .Select(i => _rows[i]);
            _surface.Selection.SetMany(picked);
        };

        _list.CellCheckChanged += (_, e) =>
        {
            if (_syncing || e.ItemIndex < 0 || e.ItemIndex >= _rows.Count)
                return;

            var control = _rows[e.ItemIndex];
            var isChecked = e.CurrentState == CheckState.Checked;
            if (e.ColumnIndex == 1)
            {
                control.Visible = isChecked;
            }
            else if (e.ColumnIndex == 2)
            {
                if (isChecked) _surface.Locked.Add(control);
                else _surface.Locked.Remove(control);
            }

            _surface.Invalidate();
        };

        toFront.Click += (_, _) => { if (_surface.Selection.Primary is { } c) _surface.BringToFront(c); Rebuild(); };
        toBack.Click += (_, _) => { if (_surface.Selection.Primary is { } c) _surface.SendToBack(c); Rebuild(); };

        _surface.StructureChanged += Rebuild;
        _surface.Selection.Changed += SyncSelectionFromSurface;
        Rebuild();
    }

    /// <summary>Rebinds this panel to a different document's surface (multi-document switching).</summary>
    public void Attach(DesignSurface surface)
    {
        if (ReferenceEquals(_surface, surface))
            return;

        _surface.StructureChanged -= Rebuild;
        _surface.Selection.Changed -= SyncSelectionFromSurface;

        _surface = surface;

        _surface.StructureChanged += Rebuild;
        _surface.Selection.Changed += SyncSelectionFromSurface;
        Rebuild();
    }

    public void Rebuild()
    {
        _syncing = true;
        try
        {
            _rows.Clear();
            _list.Items.Clear();

            // Topmost first, like Figma's layer list.
            foreach (var control in _surface.DesignedControls.OrderByDescending(c => c.ZOrder))
            {
                _rows.Add(control);
                var item = new GridListItem($"{control.Name}  ·  {control.GetType().Name}");
                item.Cells.Add(new GridListCell { CheckState = control.Visible ? CheckState.Checked : CheckState.Unchecked });
                item.Cells.Add(new GridListCell { CheckState = _surface.Locked.Contains(control) ? CheckState.Checked : CheckState.Unchecked });
                _list.Items.Add(item);
            }
        }
        finally
        {
            _syncing = false;
        }

        SyncSelectionFromSurface();
    }

    private void SyncSelectionFromSurface()
    {
        _syncing = true;
        try
        {
            for (var i = 0; i < _rows.Count; i++)
                if (_list.Items[i] is { } item)
                    item.Selected = _surface.Selection.Contains(_rows[i]);
        }
        finally
        {
            _syncing = false;
        }
    }
}
