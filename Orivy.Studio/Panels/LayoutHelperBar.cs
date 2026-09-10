using Orivy;
using Orivy.Controls;
using Orivy.Studio.History;
using SkiaSharp;
using System;

namespace Orivy.Studio.Panels;

/// <summary>
/// Quick Dock + Anchor editors for the primary selection (a WinForms-designer-style helper that is
/// faster than hunting the property grid). Changes are applied live, relayout the canvas, and are
/// recorded on the document's undo stack.
/// </summary>
public sealed class LayoutHelperBar : Element
{
    private static readonly DockStyle[] DockOrder =
        { DockStyle.None, DockStyle.Top, DockStyle.Bottom, DockStyle.Left, DockStyle.Right, DockStyle.Fill };

    private readonly Func<DesignSurface> _active;
    private readonly ComboBox _dock;
    private readonly CheckBox _anchorLeft, _anchorTop, _anchorRight, _anchorBottom;
    private readonly Element _gridRow;
    private readonly NumericUpDown _gridRowField, _gridColumnField, _gridRowSpanField, _gridColumnSpanField;
    private bool _syncing;

    public LayoutHelperBar(Func<DesignSurface> active)
    {
        _active = active;

        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        var dockRow = new Element
        {
            Dock = DockStyle.Top, Height = 40, Margin = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0),
        };
        dockRow.Controls.Add(new Element
        {
            Text = "Dock", Dock = DockStyle.Left, Width = 52, BackColor = SKColors.Transparent,
            Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        });
        _dock = new ComboBox { Dock = DockStyle.Fill };
        foreach (var d in DockOrder)
            _dock.Items.Add(d.ToString());
        _dock.SelectedIndexChanged += (_, _) => ApplyDock();
        dockRow.Controls.Add(_dock);

        var anchorRow = new Element
        {
            Dock = DockStyle.Top, Height = 36,
            BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0),
        };
        anchorRow.Controls.Add(new Element
        {
            Text = "Anchor", Dock = DockStyle.Left, Width = 52, BackColor = SKColors.Transparent,
            Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        });
        _anchorLeft = AnchorToggle("L", anchorRow);
        _anchorTop = AnchorToggle("T", anchorRow);
        _anchorRight = AnchorToggle("R", anchorRow);
        _anchorBottom = AnchorToggle("B", anchorRow);

        // A Grid child's Row/Column/RowSpan/ColumnSpan live in the PARENT Grid's own placement
        // dictionary (Grid.SetPlacement), not as a property on the child itself — so unlike Dock/
        // Anchor above, there's nothing here reflection could ever find on the selected control to
        // show in the ordinary property grid. Only shown when the selection's parent is a Grid.
        _gridRow = new Element
        {
            Dock = DockStyle.Top, Height = 36, Margin = new Thickness(0, 0, 0, 8),
            BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0),
        };
        _gridRowField = GridPlacementField("Row", _gridRow);
        _gridColumnField = GridPlacementField("Col", _gridRow);
        _gridRowSpanField = GridPlacementField("RSpan", _gridRow);
        _gridColumnSpanField = GridPlacementField("CSpan", _gridRow);

        Controls.Add(_gridRow);
        Controls.Add(anchorRow);
        Controls.Add(dockRow);

        Refresh();
    }

    private NumericUpDown GridPlacementField(string label, Element row)
    {
        var field = new NumericUpDown { Dock = DockStyle.Right, Width = 44, Minimum = 0, Maximum = 999, Margin = new Thickness(4, 0, 0, 0) };
        field.ValueChanged += (_, _) => ApplyGridPlacement();
        row.Controls.Add(field);
        row.Controls.Add(new Element
        {
            Text = label, Dock = DockStyle.Right, Width = 36, BackColor = SKColors.Transparent,
            Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleRight,
            Margin = new Thickness(8, 0, 4, 0),
        });
        return field;
    }

    private CheckBox AnchorToggle(string text, Element row)
    {
        var toggle = new CheckBox { Text = text, Dock = DockStyle.Left, Width = 52, Margin = new Thickness(0, 0, 4, 0) };
        toggle.CheckedChanged += (_, _) => ApplyAnchor();
        row.Controls.Add(toggle);
        return toggle;
    }

    /// <summary>Reloads the editors from the current primary selection.</summary>
    public void Refresh()
    {
        _syncing = true;
        try
        {
            var target = _active().Selection.Primary;
            Enabled = target != null;

            var dock = target?.Dock ?? DockStyle.None;
            _dock.SelectedIndex = Array.IndexOf(DockOrder, dock);

            var anchor = target?.Anchor ?? (AnchorStyles.Top | AnchorStyles.Left);
            _anchorLeft.Checked = (anchor & AnchorStyles.Left) != 0;
            _anchorTop.Checked = (anchor & AnchorStyles.Top) != 0;
            _anchorRight.Checked = (anchor & AnchorStyles.Right) != 0;
            _anchorBottom.Checked = (anchor & AnchorStyles.Bottom) != 0;

            // Anchor is meaningless while docked.
            var anchorEnabled = target != null && dock == DockStyle.None;
            _anchorLeft.Enabled = _anchorTop.Enabled = _anchorRight.Enabled = _anchorBottom.Enabled = anchorEnabled;

            if (target?.Parent is Grid grid)
            {
                _gridRow.Visible = true;
                var placement = grid.GetPlacement(target);
                _gridRowField.Value = placement.Row;
                _gridColumnField.Value = placement.Column;
                _gridRowSpanField.Value = placement.RowSpan;
                _gridColumnSpanField.Value = placement.ColumnSpan;
            }
            else
            {
                _gridRow.Visible = false;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ApplyGridPlacement()
    {
        if (_syncing)
            return;
        var surface = _active();
        var target = surface.Selection.Primary;
        if (target?.Parent is not Grid grid)
            return;

        var old = grid.GetPlacement(target);
        var next = new GridPlacement(
            (int)_gridRowField.Value, (int)_gridColumnField.Value,
            (int)_gridRowSpanField.Value, (int)_gridColumnSpanField.Value);
        if (next == old)
            return;

        surface.Commands.Execute(new DelegateCommand(
            $"Grid placement of {target.Name}",
            () => { grid.SetPlacement(target, next.Row, next.Column, next.RowSpan, next.ColumnSpan); surface.RelayoutRoot(); },
            () => { grid.SetPlacement(target, old.Row, old.Column, old.RowSpan, old.ColumnSpan); surface.RelayoutRoot(); }));
    }

    private void ApplyDock()
    {
        if (_syncing)
            return;
        var surface = _active();
        var target = surface.Selection.Primary;
        if (target == null || _dock.SelectedIndex < 0)
            return;

        var newDock = DockOrder[_dock.SelectedIndex];
        var oldDock = target.Dock;
        if (newDock == oldDock)
            return;

        surface.Commands.Execute(new DelegateCommand(
            $"Dock = {newDock}",
            () => { target.Dock = newDock; surface.RelayoutRoot(); },
            () => { target.Dock = oldDock; surface.RelayoutRoot(); }));
        Refresh();
    }

    private void ApplyAnchor()
    {
        if (_syncing)
            return;
        var surface = _active();
        var target = surface.Selection.Primary;
        if (target == null)
            return;

        AnchorStyles next = 0;
        if (_anchorLeft.Checked) next |= AnchorStyles.Left;
        if (_anchorTop.Checked) next |= AnchorStyles.Top;
        if (_anchorRight.Checked) next |= AnchorStyles.Right;
        if (_anchorBottom.Checked) next |= AnchorStyles.Bottom;
        if (next == 0)
            next = AnchorStyles.Top | AnchorStyles.Left;

        var old = target.Anchor;
        if (next == old)
            return;

        surface.Commands.Execute(new DelegateCommand(
            $"Anchor = {next}",
            () => { target.Anchor = next; surface.RelayoutRoot(); },
            () => { target.Anchor = old; surface.RelayoutRoot(); }));
    }
}
