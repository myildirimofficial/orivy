using Orivy;
using Orivy.Controls;
using Orivy.Studio.History;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Orivy.Studio.Panels;

/// <summary>
/// Events page for the selected control. Lists every public event and the handler method
/// written into <c>InitializeComponent</c> (<c>name.Click += name_Click</c>). The handler is
/// typed in the Handler cell; a double-click on an empty row names it <c>control_Event</c>.
/// </summary>
public sealed class EventHelper : Element
{
    private readonly Func<DesignSurface> _active;
    private const int HandlerColumn = 1;

    private readonly TextBox _search;
    private readonly GridList _list;
    private readonly TextBox _cellEditor;
    private readonly List<string> _eventNames = new();
    private int _editingIndex = -1;
    private string? _editingEvent;
    private bool _suppressCellEdit;
    private bool _syncing;

    public EventHelper(Func<DesignSurface> active)
    {
        _active = active;
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        _search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 32,
            Margin = new Thickness(0, 0, 0, 8),
            PlaceholderText = "Filter events",
        };
        _search.TextChanged += (_, _) => Reload();

        _list = new GridList
        {
            Dock = DockStyle.Fill,
            HeaderVisible = true,
            HeaderHeight = 26f,
            GroupingEnabled = false,
            ShowGridLines = false,
            FullRowSelect = true,
            RowHeight = 28,
            Radius = new Radius(10),
            Border = new Thickness(1),
        };
        _list.ConfigureVisualStyles(styles => styles.Base(b => b.Background(ColorScheme.Surface.WithAlpha(178))));
        _list.Columns.Add(new GridListColumn { Name = "event", Text = "Event", SizeMode = GridListColumnSizeMode.Fill, Sortable = false });
        _list.Columns.Add(new GridListColumn { Name = "handler", Text = "Handler", Width = 140, Sortable = false });
        _list.CellClick += (_, e) =>
        {
            if (_suppressCellEdit)
            {
                _suppressCellEdit = false;
                return;
            }

            if (e.ColumnIndex == HandlerColumn)
                BeginCellEdit(e.ItemIndex);
        };
        _list.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
                return;

            var hit = _list.HitTest(e.Location);
            if (hit.ItemIndex < 0 || hit.Region is not (GridListHitTestRegion.Cell or GridListHitTestRegion.CheckBox))
                return;

            _list.SelectedIndex = hit.ItemIndex;
            if (!string.IsNullOrEmpty(CurrentHandler()))
                return;

            CancelCellEdit();
            _suppressCellEdit = true;
            CommitHandler(SuggestedHandler());
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode is not (Keys.F2 or Keys.Enter) || _editingIndex >= 0)
                return;

            BeginCellEdit(_list.SelectedIndex);
            e.Handled = true;
        };
        _list.MouseWheel += (_, _) => CommitCellEdit(reload: true);

        _cellEditor = new TextBox
        {
            Visible = false,
            Dock = DockStyle.None,
            Radius = new Radius(4),
            Border = new Thickness(1),
            Padding = new Thickness(8, 0, 8, 0),
        };
        _cellEditor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                CommitCellEdit(reload: true);
                _list.Focus();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                CancelCellEdit();
                _list.Focus();
            }
        };
        _cellEditor.LostFocus += (_, _) => CommitCellEdit(reload: true);
        _cellEditor.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !string.IsNullOrEmpty(CurrentHandler()))
                return;

            var suggested = SuggestedHandler();
            CancelCellEdit();
            CommitHandler(suggested);
        };
        _list.Controls.Add(_cellEditor);

        Controls.Add(_list);
        Controls.Add(_search);
    }

    public void Reload()
    {
        if (_syncing)
            return;

        if (_editingIndex >= 0)
            CommitCellEdit(reload: false);

        _syncing = true;
        try
        {
            var selected = SelectedEventName();
            _eventNames.Clear();
            _list.Items.Clear();

            var surface = _active();
            var target = surface.Selection.Primary;
            Enabled = target != null;
            if (target == null)
                return;

            var filter = _search.Text?.Trim() ?? string.Empty;
            var wired = new HashSet<string>(
                surface.EventWires
                    .Where(wire => wire.ControlName == target.Name && !string.IsNullOrEmpty(wire.Handler))
                    .Select(wire => wire.EventName),
                StringComparer.Ordinal);
            var events = target.GetType()
                .GetEvents(BindingFlags.Instance | BindingFlags.Public)
                .Select(item => item.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(name => wired.Contains(name))
                .ThenBy(name => name, StringComparer.Ordinal);
            var row = 0;
            var select = -1;
            foreach (var name in events)
            {
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var handler = surface.EventWires
                    .FirstOrDefault(wire => wire.ControlName == target.Name && wire.EventName == name)
                    ?.Handler ?? string.Empty;
                _eventNames.Add(name);
                _list.Items.Add(new GridListItem(name) { Cells = { new GridListCell { Text = handler } } });
                if (name == selected)
                    select = row;
                row++;
            }

            if (select >= 0)
                _list.SelectedIndex = select;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BeginCellEdit(int index)
    {
        if (_syncing || index < 0 || index >= _eventNames.Count)
            return;

        CommitCellEdit(reload: true);
        if (index >= _eventNames.Count)
            return;

        var bounds = _list.GetCellBounds(index, HandlerColumn);
        if (bounds.IsEmpty)
            return;

        _list.SelectedIndex = index;
        _editingIndex = index;
        _editingEvent = _eventNames[index];
        _cellEditor.Text = CurrentHandler();
        _cellEditor.Location = bounds.Location;
        _cellEditor.Size = bounds.Size;
        _cellEditor.Visible = true;
        _cellEditor.BringToFront();
        _cellEditor.Focus();
        _cellEditor.SelectAll();
    }

    private void CommitCellEdit(bool reload)
    {
        if (_editingIndex < 0)
            return;

        var eventName = _editingEvent;
        var handler = _cellEditor.Text;
        CancelCellEdit();
        CommitHandler(eventName, handler, reload);
    }

    private void CancelCellEdit()
    {
        _editingIndex = -1;
        _editingEvent = null;
        _cellEditor.Visible = false;
    }

    private void CommitHandler(string? handler) =>
        CommitHandler(SelectedEventName(), handler, reload: true);

    private void CommitHandler(string? eventName, string? handler, bool reload)
    {
        var surface = _active();
        var target = surface.Selection.Primary;
        if (target == null || string.IsNullOrEmpty(target.Name) || eventName == null)
            return;

        handler = handler?.Trim() ?? string.Empty;
        var existing = surface.EventWires.FirstOrDefault(wire => wire.ControlName == target.Name && wire.EventName == eventName);
        var previous = existing?.Handler ?? string.Empty;
        if (previous == handler)
            return;

        var controlName = target.Name;
        surface.Commands.Execute(new DelegateCommand(
            string.IsNullOrEmpty(handler) ? $"Clear {controlName}.{eventName}" : $"{controlName}.{eventName} += {handler}",
            () => Set(surface, controlName, eventName, handler),
            () => Set(surface, controlName, eventName, previous)));
        if (reload)
            Reload();
    }

    private static void Set(DesignSurface surface, string controlName, string eventName, string handler)
    {
        var existing = surface.EventWires.FirstOrDefault(wire => wire.ControlName == controlName && wire.EventName == eventName);
        if (string.IsNullOrEmpty(handler))
        {
            if (existing != null)
                surface.EventWires.Remove(existing);
            return;
        }

        if (existing == null)
            surface.EventWires.Add(new EventWire(controlName, eventName, handler));
        else
            existing.Handler = handler;
    }

    private string CurrentHandler()
    {
        var name = SelectedEventName();
        var target = _active().Selection.Primary;
        if (name == null || target == null)
            return string.Empty;
        return _active().EventWires.FirstOrDefault(wire => wire.ControlName == target.Name && wire.EventName == name)?.Handler ?? string.Empty;
    }

    private string SuggestedHandler()
    {
        var target = _active().Selection.Primary;
        var eventName = SelectedEventName();
        if (target == null || eventName == null)
            return string.Empty;
        return target.Name + "_" + eventName;
    }

    private string? SelectedEventName()
    {
        var index = _list.SelectedIndex;
        return index >= 0 && index < _eventNames.Count ? _eventNames[index] : null;
    }
}
