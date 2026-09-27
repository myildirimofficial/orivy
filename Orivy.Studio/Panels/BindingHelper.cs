using Orivy.Controls;
using Orivy.Studio.History;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// Bindings page for the selected control. Each row becomes a <c>Link(...).From(...)</c>
/// or <c>FromData</c> call in <c>InitializeComponent</c>.
/// </summary>
public sealed class BindingHelper : Element
{
    private const string DataContextSource = "(DataContext)";

    private readonly Func<DesignSurface> _active;
    private readonly GridList _list;
    private readonly ComboBox _property;
    private readonly ComboBox _source;
    private readonly TextBox _member;
    private readonly ComboBox _memberList;
    private readonly TextBox _sourceType;
    private readonly ComboBox _mode;
    private readonly List<ControlBinding> _rows = new();
    private bool _syncing;

    public BindingHelper(Func<DesignSurface> active)
    {
        _active = active;
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        var editor = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 168,
            Margin = new Thickness(0, 8, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };

        _property = new ComboBox { Dock = DockStyle.Fill };
        _source = new ComboBox { Dock = DockStyle.Fill };
        _member = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Property on the data object" };
        _memberList = new ComboBox { Dock = DockStyle.Fill, Visible = false };
        _sourceType = new TextBox { Dock = DockStyle.Fill };
        _mode = new ComboBox { Dock = DockStyle.Fill };
        _mode.Items.Add("OneWay");
        _mode.Items.Add("TwoWay");
        _source.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _sourceType.Enabled = IsDataContext();
            FillMembers();
        };

        var remove = new Button { Text = "Remove", Dock = DockStyle.Right, Width = 76, Margin = new Thickness(4, 4, 0, 0) };
        var add = new Button { Text = "Add", Dock = DockStyle.Right, Width = 64, Margin = new Thickness(0, 4, 0, 0) };
        ToolbarButton.ApplyFlatSkin(remove);
        ToolbarButton.ApplyFlatSkin(add);
        var buttons = new Element
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        buttons.Controls.Add(remove);
        buttons.Controls.Add(add);
        // Last docked-top child is laid out at the top, so add from the bottom of the form upward.
        editor.Controls.Add(buttons);
        editor.Controls.Add(Row("Mode", _mode));
        editor.Controls.Add(Row("Context type", _sourceType));
        var memberHost = new Element
        {
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        memberHost.Controls.Add(_member);
        memberHost.Controls.Add(_memberList);
        editor.Controls.Add(Row("Member", memberHost));
        editor.Controls.Add(Row("Source", _source));
        editor.Controls.Add(Row("Property", _property));

        add.Click += (_, _) => AddBinding();
        remove.Click += (_, _) => RemoveSelected();

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
        _list.Columns.Add(new GridListColumn { Name = "property", Text = "Property", Width = 90, Sortable = false });
        _list.Columns.Add(new GridListColumn { Name = "source", Text = "Source", SizeMode = GridListColumnSizeMode.Fill, Sortable = false });
        _list.Columns.Add(new GridListColumn { Name = "mode", Text = "Mode", Width = 72, Sortable = false });
        _list.SelectionChanged += (_, _) => ShowSelected();

        Controls.Add(_list);
        Controls.Add(editor);
    }

    public void Reload()
    {
        _syncing = true;
        try
        {
            var surface = _active();
            var target = surface.Selection.Primary;
            Enabled = target != null;
            _rows.Clear();
            _list.Items.Clear();
            FillCombos(surface, target);

            if (target == null || string.IsNullOrEmpty(target.Name))
                return;

            foreach (var binding in surface.ControlBindings)
            {
                if (!string.Equals(binding.ControlName, target.Name, StringComparison.Ordinal))
                    continue;

                _rows.Add(binding);
                var source = string.IsNullOrEmpty(binding.SourceControl)
                    ? DataContextSource + "." + binding.SourceProperty
                    : binding.SourceControl + "." + binding.SourceProperty;
                _list.Items.Add(new GridListItem(binding.TargetProperty)
                {
                    Cells =
                    {
                        new GridListCell { Text = source },
                        new GridListCell { Text = binding.TwoWay ? "TwoWay" : "OneWay" },
                    }
                });
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void AddBinding()
    {
        if (_syncing)
            return;

        var surface = _active();
        var target = surface.Selection.Primary;
        if (target == null || string.IsNullOrEmpty(target.Name))
            return;
        var member = MemberName();
        if (_property.SelectedIndex < 0 || _source.SelectedIndex < 0 || string.IsNullOrWhiteSpace(member))
            return;

        var property = _property.Items[_property.SelectedIndex]?.ToString() ?? string.Empty;
        var sourceItem = _source.Items[_source.SelectedIndex]?.ToString() ?? string.Empty;
        var dataContext = sourceItem == DataContextSource;
        var binding = new ControlBinding(
            target.Name,
            property,
            dataContext ? null : sourceItem,
            dataContext ? _sourceType.Text?.Trim() : null,
            member,
            _mode.SelectedIndex == 1);

        var previous = surface.ControlBindings
            .Where(item => item.ControlName == target.Name && item.TargetProperty == property)
            .ToList();

        surface.Commands.Execute(new DelegateCommand(
            $"Bind {target.Name}.{property}",
            () =>
            {
                surface.ControlBindings.RemoveAll(item => item.ControlName == binding.ControlName && item.TargetProperty == binding.TargetProperty);
                surface.ControlBindings.Add(binding);
            },
            () =>
            {
                surface.ControlBindings.RemoveAll(item => item.ControlName == binding.ControlName && item.TargetProperty == binding.TargetProperty);
                surface.ControlBindings.AddRange(previous);
            }));
        Reload();
    }

    private void RemoveSelected()
    {
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _rows.Count)
            return;

        var surface = _active();
        var binding = _rows[index];
        surface.Commands.Execute(new DelegateCommand(
            $"Remove {binding.ControlName}.{binding.TargetProperty}",
            () => surface.ControlBindings.Remove(binding),
            () => surface.ControlBindings.Add(binding)));
        Reload();
    }

    private void ShowSelected()
    {
        if (_syncing)
            return;
        var index = _list.SelectedIndex;
        if (index < 0 || index >= _rows.Count)
            return;

        var binding = _rows[index];
        Select(_property, binding.TargetProperty);
        Select(_source, string.IsNullOrEmpty(binding.SourceControl) ? DataContextSource : binding.SourceControl!);
        _sourceType.Text = binding.SourceType ?? string.Empty;
        _mode.SelectedIndex = binding.TwoWay ? 1 : 0;
        _sourceType.Enabled = IsDataContext();
        FillMembers();
        if (IsDataContext())
            _member.Text = binding.SourceProperty;
        else
            Select(_memberList, binding.SourceProperty);
    }

    private void FillCombos(DesignSurface surface, ElementBase? target)
    {
        _property.Items.Clear();
        _source.Items.Clear();
        _source.Items.Add(DataContextSource);
        if (target != null)
        {
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(target))
            {
                if (descriptor.IsReadOnly || !descriptor.IsBrowsable || descriptor.SerializationVisibility == DesignerSerializationVisibility.Hidden)
                    continue;
                _property.Items.Add(descriptor.Name);
            }

            foreach (var control in surface.AllDesignedControls)
            {
                if (!ReferenceEquals(control, target) && !string.IsNullOrEmpty(control.Name))
                    _source.Items.Add(control.Name);
            }
        }

        if (_property.Items.Count > 0 && _property.SelectedIndex < 0)
            _property.SelectedIndex = 0;
        if (_source.SelectedIndex < 0)
            _source.SelectedIndex = 0;
        if (_mode.SelectedIndex < 0)
            _mode.SelectedIndex = 0;
        _sourceType.Enabled = IsDataContext();
        FillMembers();
    }

    private void FillMembers()
    {
        var dataContext = IsDataContext();
        _member.Visible = dataContext;
        _memberList.Visible = !dataContext;
        if (dataContext)
            return;

        var keep = _memberList.SelectedIndex >= 0
            ? _memberList.Items[_memberList.SelectedIndex]?.ToString()
            : null;
        _memberList.Items.Clear();
        var sourceName = _source.SelectedIndex >= 0 ? _source.Items[_source.SelectedIndex]?.ToString() : null;
        var source = _active().AllDesignedControls.FirstOrDefault(control => control.Name == sourceName);
        if (source != null)
        {
            foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(source))
            {
                if (!descriptor.IsBrowsable || descriptor.SerializationVisibility == DesignerSerializationVisibility.Hidden)
                    continue;
                _memberList.Items.Add(descriptor.Name);
            }
        }

        if (!string.IsNullOrEmpty(keep))
            Select(_memberList, keep);
        if (_memberList.SelectedIndex < 0 && _memberList.Items.Count > 0)
            _memberList.SelectedIndex = 0;
    }

    private string MemberName()
    {
        if (IsDataContext())
            return _member.Text?.Trim() ?? string.Empty;
        if (_memberList.SelectedIndex < 0)
            return string.Empty;
        return _memberList.Items[_memberList.SelectedIndex]?.ToString() ?? string.Empty;
    }

    private bool IsDataContext() =>
        _source.SelectedIndex >= 0
        && string.Equals(_source.Items[_source.SelectedIndex]?.ToString(), DataContextSource, StringComparison.Ordinal);

    private static void Select(ComboBox combo, string text)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), text, StringComparison.Ordinal))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static Element Row(string label, ElementBase field)
    {
        var row = new Element
        {
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Thickness(0, 0, 0, 4),
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        field.Margin = new Thickness(0);
        row.Controls.Add(field);
        row.Controls.Add(new Element
        {
            Text = label,
            Dock = DockStyle.Left,
            Width = 88,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleLeft,
        });
        return row;
    }
}
