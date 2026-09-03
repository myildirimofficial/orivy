using Orivy;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
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
    private readonly GridListColumn _eyeColumn;
    private readonly GridListColumn _lockColumn;
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
            Height = 36,
            Padding = new Thickness(0, 6, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        var toFront = new ToolbarButton("bring-front", "Bring to front", 28f);
        var toBack = new ToolbarButton("send-back", "Send to back", 28f);
        buttons.Controls.Add(toBack);
        buttons.Controls.Add(toFront);

        _list = new GridList
        {
            Dock = DockStyle.Fill,
            // The eye/lock columns are icon-only with no room for a text label anywhere except the
            // header — without it, showing/hiding a control has no on-screen explanation of which
            // checkbox does which.
            HeaderVisible = true,
            HeaderHeight = 26f,
            GroupingEnabled = false,
            ShowGridLines = false,
            MultiSelect = true,
            FullRowSelect = true,
            RowHeight = 28,
            Radius = new Radius(10),
            Border = new Thickness(1),
        };
        _list.ConfigureVisualStyles(styles => styles.Base(b => b.Background(ColorScheme.Surface.WithAlpha(178))));
        _list.Columns.Add(new GridListColumn { Name = "name", Text = "Layer", SizeMode = GridListColumnSizeMode.Fill, Sortable = false });
        _eyeColumn = new GridListColumn { Name = "eye", Width = 40f, MinWidth = 36f, ShowCheckBox = true, Sortable = false, CellTextAlign = ContentAlignment.MiddleCenter, TextAlign = ContentAlignment.MiddleCenter };
        _lockColumn = new GridListColumn { Name = "lock", Width = 40f, MinWidth = 36f, ShowCheckBox = true, Sortable = false, CellTextAlign = ContentAlignment.MiddleCenter, TextAlign = ContentAlignment.MiddleCenter };
        _list.Columns.Add(_eyeColumn);
        _list.Columns.Add(_lockColumn);

        Controls.Add(_list);
        Controls.Add(buttons);

        RefreshColumnIcons();
        ColorScheme.ThemeChanged += (_, _) => RefreshColumnIcons();

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

            // Topmost first, like Figma's layer list. Recurses into a group's own children too — this
            // used to only ever list the top-level DesignedControls, so a group's contents had no way
            // to be seen, selected or unlocked from here at all short of clicking them directly on the
            // (possibly crowded) canvas.
            foreach (var control in _surface.DesignedControls.OrderByDescending(c => c.ZOrder))
                AddRow(control, depth: 0);
        }
        finally
        {
            _syncing = false;
        }

        SyncSelectionFromSurface();
    }

    /// <summary>Adds one row for <paramref name="control"/>, then recurses into its own children —
    /// indented one level further each time — so nesting reads as a simple visual hierarchy even
    /// though this is a flat list, not an actual collapsible tree.</summary>
    private void AddRow(ElementBase control, int depth)
    {
        _rows.Add(control);

        // A leading tree-branch glyph (rather than plain spaces, which some text renderers collapse)
        // makes a nested row visually distinct from a top-level one at a glance.
        var indent = depth > 0 ? new string(' ', (depth - 1) * 3) + "↳ " : string.Empty;
        var item = new GridListItem($"{indent}{control.Name}  ·  {control.GetType().Name}");
        item.Cells.Add(new GridListCell { CheckState = control.Visible ? CheckState.Checked : CheckState.Unchecked });
        item.Cells.Add(new GridListCell { CheckState = _surface.Locked.Contains(control) ? CheckState.Checked : CheckState.Unchecked });
        _list.Items.Add(item);

        var children = control.Controls.OfType<ElementBase>().Where(c => c is not ScrollBar).OrderByDescending(c => c.ZOrder);
        foreach (var child in children)
            AddRow(child, depth + 1);
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

    /// <summary>Renders the eye/lock column headers as the app's own themed vector glyphs instead of
    /// raw emoji text — matches every other icon in the shell and follows dark/light toggles.</summary>
    private void RefreshColumnIcons()
    {
        // GridList draws a HeaderIcon into a fixed 16-logical-unit rect (GridList.IconSize) — baking
        // at a different logical size (14) here meant the source bitmap's own "already crisp at 14"
        // resolution got a second, mismatched stretch to fill the actual 16-unit slot, softening it.
        var color = ColorScheme.ForeColor.WithAlpha(190);
        var oldEye = _eyeColumn.HeaderIcon;
        var oldLock = _lockColumn.HeaderIcon;
        _eyeColumn.HeaderIcon = ToolbarIcons.CreateImage("eye", 16f * ScaleFactor * 2f, color);
        _lockColumn.HeaderIcon = ToolbarIcons.CreateImage("lock", 16f * ScaleFactor * 2f, color);
        oldEye?.Dispose();
        oldLock?.Dispose();
        Invalidate();
    }
}
