using Orivy.Animation;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Orivy.Controls;

/// <summary>
/// A property sheet that reflects a target object's public properties into an editable, expandable
/// two-column grid (Property / Value), grouped by <c>[Category]</c>. Supports inline editors for
/// text and numbers, drop-down pickers for booleans and enums, and expandable rows for nested
/// objects and collections/arrays. Built on <see cref="GridList"/>. API mirrors the common surface
/// of System.Windows.Forms.PropertyGrid.
/// </summary>
public class PropertyGrid : GridList
{
    private const int NameColumn = 0;
    private const int ValueColumn = 1;

    private object? _selectedObject;
    private PropertySort _propertySort = PropertySort.Categorized;

    private readonly List<PropNode> _rootNodes = new();

    private TextBox? _textEditor;
    private NumericUpDown? _numericEditor;
    private DatePicker? _dateEditor;
    private bool _dateDropDownOpen;
    private ColorPicker? _colorEditor;
    private WindowBase? _colorHost;
    private ContextMenuStrip? _dropDown;

    // Expand/collapse chevron icons + reveal animation (styled after the group headers).
    private SKImage? _chevronRight;
    private SKImage? _chevronDown;
    private int _chevronDpi;
    private SKColor _chevronColor;
    private readonly AnimationManager _revealAnim;
    private readonly List<GridListItem> _revealItems = new();
    private PropNode? _revealNode;
    private bool _revealExpanding;

    private PropNode? _editingNode;
    private GridListItem? _editingItem;
    private SKRect _editorBounds;
    private bool _suppressEditorEvents;

    /// <summary>Occurs after a property value has been changed through an editor.</summary>
    public event EventHandler<PropertyValueChangedEventArgs>? PropertyValueChanged;

    /// <summary>Occurs when <see cref="SelectedObject"/> changes.</summary>
    public event EventHandler? SelectedObjectChanged;

    /// <summary>Occurs when the highlighted property row changes (use for a description/help pane).</summary>
    public event EventHandler? SelectedPropertyChanged;

    public PropertyGrid()
    {
        Columns.Add(new GridListColumn
        {
            Name = "property",
            Text = "Property",
            Width = 190f,
            MinWidth = 110f,
            SizeMode = GridListColumnSizeMode.Fixed,
            Sortable = false
        });
        Columns.Add(new GridListColumn
        {
            Name = "value",
            Text = "Value",
            MinWidth = 120f,
            SizeMode = GridListColumnSizeMode.Fill,
            FillWeight = 1.4f,
            Sortable = false
        });

        HeaderVisible = true;
        StickyHeader = true;
        GroupingEnabled = true;
        MultiSelect = false;
        FullRowSelect = true;
        ShowGridLines = true;
        AllowColumnResize = true;
        AllowRowResize = false;

        _revealAnim = new AnimationManager(true)
        {
            Increment = 0.10,
            AnimationType = AnimationType.CubicEaseOut,
            InterruptAnimation = true
        };
        _revealAnim.OnAnimationProgress += _ => ApplyRevealProgress();
        _revealAnim.OnAnimationFinished += _ => FinishReveal();

        CellClick += OnCellClicked;
        SelectionChanged += (_, _) => SelectedPropertyChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Expand chevron + reveal animation

    private SKImage GetChevron(bool expanded)
    {
        var color = ColorScheme.ForeColor.WithAlpha(160);
        var dpi = DeviceDpi;
        if (_chevronRight == null || _chevronDown == null || _chevronDpi != dpi || _chevronColor != color)
        {
            _chevronRight?.Dispose();
            _chevronDown?.Dispose();
            _chevronRight = CreateChevronImage(expanded: false, color);
            _chevronDown = CreateChevronImage(expanded: true, color);
            _chevronDpi = dpi;
            _chevronColor = color;
        }

        return expanded ? _chevronDown! : _chevronRight!;
    }

    private static SKImage CreateChevronImage(bool expanded, SKColor color)
    {
        const int size = 16;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.7f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = color
        };

        using var path = new SKPath();
        if (expanded)
        {
            path.MoveTo(size * 0.28f, size * 0.40f);
            path.LineTo(size * 0.50f, size * 0.64f);
            path.LineTo(size * 0.72f, size * 0.40f);
        }
        else
        {
            path.MoveTo(size * 0.40f, size * 0.28f);
            path.LineTo(size * 0.64f, size * 0.50f);
            path.LineTo(size * 0.40f, size * 0.72f);
        }

        canvas.DrawPath(path, paint);
        return surface.Snapshot();
    }

    private void ToggleNodeAnimated(PropNode node)
    {
        // Settle any in-flight animation before starting a new one.
        FinishReveal();

        if (!node.Expanded)
        {
            node.Expanded = true;
            EnsureChildren(node);
            RebuildRows();

            CollectSubtreeRows(node);
            if (_revealItems.Count == 0)
                return;

            _revealExpanding = true;
            _revealNode = node;
            foreach (var row in _revealItems)
                row.Height = 2f;
            _revealAnim.StartNewAnimation(AnimationDirection.In);
        }
        else
        {
            CollectSubtreeRows(node);
            if (_revealItems.Count == 0)
            {
                node.Expanded = false;
                RebuildRows();
                return;
            }

            // Flip the chevron immediately so it doesn't lag the closing animation.
            SetNodeChevron(node, expanded: false);
            _revealExpanding = false;
            _revealNode = node;
            _revealAnim.StartNewAnimation(AnimationDirection.In);
        }
    }

    private void SetNodeChevron(PropNode node, bool expanded)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (ReferenceEquals(Items[i].Tag, node) && Items[i].Cells.Count > NameColumn)
            {
                Items[i].Cells[NameColumn].Icon = GetChevron(expanded);
                Invalidate();
                return;
            }
        }
    }

    private void CollectSubtreeRows(PropNode node)
    {
        _revealItems.Clear();

        var nodeRow = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (ReferenceEquals(Items[i].Tag, node))
            {
                nodeRow = i;
                break;
            }
        }

        if (nodeRow < 0)
            return;

        for (var i = nodeRow + 1; i < Items.Count; i++)
        {
            if (Items[i].Tag is not PropNode child || child.Depth <= node.Depth)
                break;
            _revealItems.Add(Items[i]);
        }
    }

    private void ApplyRevealProgress()
    {
        if (_revealNode == null)
            return;

        var progress = (float)_revealAnim.GetProgress();
        var target = RowHeight;
        var height = _revealExpanding
            ? 2f + (target - 2f) * progress
            : Math.Max(2f, target * (1f - progress));

        for (var i = 0; i < _revealItems.Count; i++)
            _revealItems[i].Height = height;
    }

    private void FinishReveal()
    {
        if (_revealNode == null)
            return;

        var node = _revealNode;
        _revealNode = null;

        if (_revealExpanding)
        {
            // Back to the default row height (0 = "use RowHeight").
            for (var i = 0; i < _revealItems.Count; i++)
                _revealItems[i].Height = 0f;
        }
        else
        {
            node.Expanded = false;
            RebuildRows();
        }

        _revealItems.Clear();
        Invalidate();
    }

    #endregion

    #region Public API

    /// <summary>Gets or sets the object whose properties are displayed and edited.</summary>
    [Browsable(false)]
    public object? SelectedObject
    {
        get => _selectedObject;
        set
        {
            if (ReferenceEquals(_selectedObject, value))
                return;

            CommitEditor();
            _selectedObject = value;
            BuildTree();
            RebuildRows();
            SelectedObjectChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets whether properties are categorized, alphabetized, or unsorted.</summary>
    [DefaultValue(PropertySort.Categorized)]
    public PropertySort PropertySort
    {
        get => _propertySort;
        set
        {
            if (_propertySort == value)
                return;

            _propertySort = value;
            BuildTree();
            RebuildRows();
        }
    }

    /// <summary>The property descriptor of the currently highlighted row, or null.</summary>
    [Browsable(false)]
    public PropertyDescriptor? SelectedProperty =>
        (SelectedItem?.Tag as PropNode)?.Descriptor;

    /// <summary>The description of the highlighted property, or empty.</summary>
    [Browsable(false)]
    public string SelectedPropertyDescription =>
        (SelectedItem?.Tag as PropNode)?.Descriptor?.Description ?? string.Empty;

    /// <summary>
    /// Re-reads all property values from the selected object and repaints. Collection rows are
    /// re-synchronized (added/removed elements appear/disappear) while expansion state is kept.
    /// </summary>
    public new void Refresh()
    {
        CommitEditor();
        InvalidateCollectionChildren(_rootNodes);
        RebuildRows();
    }

    private static void InvalidateCollectionChildren(List<PropNode> nodes)
    {
        foreach (var node in nodes)
        {
            InvalidateCollectionChildren(node.Children);

            // Force expanded collections to re-enumerate on the next row build.
            if (node.ChildrenBuilt && node.GetValue() is IEnumerable and not string)
                node.ChildrenBuilt = false;
        }
    }

    /// <summary>
    /// When the highlighted row is a collection element (<c>[i]</c>), returns its owning list and
    /// index so external UI (add/remove/move buttons) can operate on it. Combine with
    /// <see cref="Refresh"/> after mutating the list.
    /// </summary>
    public bool TryGetSelectedCollectionElement(out IList? list, out int index)
    {
        if (SelectedItem?.Tag is PropNode { ParentList: { } parent, ElementIndex: >= 0 } node)
        {
            list = parent;
            index = node.ElementIndex;
            return true;
        }

        list = null;
        index = -1;
        return false;
    }

    /// <summary>Expands every expandable row (nested objects and collections). Mirrors WinForms ExpandAllGridItems.</summary>
    public void ExpandAllGridItems()
    {
        SetAllExpanded(_rootNodes, true);
        RebuildRows();
    }

    /// <summary>Collapses every expandable row. Mirrors WinForms CollapseAllGridItems.</summary>
    public void CollapseAllGridItems()
    {
        SetAllExpanded(_rootNodes, false);
        RebuildRows();
    }

    private void SetAllExpanded(List<PropNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.Expandable)
                continue;

            node.Expanded = expanded;
            EnsureChildren(node);
            SetAllExpanded(node.Children, expanded);
        }
    }

    #endregion

    #region Tree model

    private sealed class PropNode
    {
        public PropertyDescriptor? Descriptor;   // null for collection-element rows
        public object? Component;                 // owner of Descriptor (or the collection for elements)
        public string Label = string.Empty;
        public string Category = "Misc";
        public int Depth;
        public bool Expandable;
        public bool Expanded;
        public bool ChildrenBuilt;
        public readonly List<PropNode> Children = new();
        public Func<object?>? Getter;
        public Action<object?>? Setter;           // null => read-only
        public Type ValueType = typeof(object);

        // Collection support. For an element row: ParentList/ElementIndex identify it. For the
        // collection row itself: CollectionValue is the live IList (when add/remove is possible).
        public IList? ParentList;
        public int ElementIndex = -1;
        public IList? CollectionValue;

        public object? GetValue()
        {
            try { return Getter?.Invoke(); }
            catch { return null; }
        }
    }

    private void BuildTree()
    {
        _rootNodes.Clear();
        if (_selectedObject == null)
            return;

        foreach (var pd in GetProperties(_selectedObject))
            _rootNodes.Add(CreatePropertyNode(pd, _selectedObject, depth: 0));
    }

    private IEnumerable<PropertyDescriptor> GetProperties(object component)
    {
        var props = TypeDescriptor.GetProperties(component).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable);

        return _propertySort switch
        {
            PropertySort.Alphabetical => props.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            PropertySort.Categorized => props
                .OrderBy(p => CategoryOf(p), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => props
        };
    }

    private PropNode CreatePropertyNode(PropertyDescriptor pd, object component, int depth)
    {
        var node = new PropNode
        {
            Descriptor = pd,
            Component = component,
            Label = pd.DisplayName,
            Category = depth == 0 ? CategoryOf(pd) : "Misc",
            Depth = depth,
            ValueType = pd.PropertyType,
            Getter = () => pd.GetValue(component),
            Setter = pd.IsReadOnly ? null : v => pd.SetValue(component, v)
        };

        node.Expandable = IsExpandable(node.ValueType, node.GetValue());
        return node;
    }

    private static bool IsExpandable(Type type, object? value)
    {
        if (value == null)
            return false;

        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (IsSimple(t))
            return false;

        if (value is IEnumerable)
            return true;

        // Complex object with at least one browsable property.
        return TypeDescriptor.GetProperties(value).Cast<PropertyDescriptor>().Any(p => p.IsBrowsable);
    }

    private void EnsureChildren(PropNode node)
    {
        if (node.ChildrenBuilt)
            return;

        node.ChildrenBuilt = true;
        node.Children.Clear();

        var value = node.GetValue();
        if (value == null)
            return;

        if (value is IEnumerable enumerable and not string)
        {
            // Arrays and List<T> implement IList, so their elements can be written back by index.
            var list = value as IList;
            node.CollectionValue = list;
            var elementType = GetCollectionElementType(value.GetType());
            var index = 0;
            foreach (var element in enumerable)
            {
                var elementIndex = index;
                var captured = element;
                var runtimeType = captured?.GetType() ?? elementType;
                var canEditElement = list is { IsReadOnly: false } && IsSimple(runtimeType);

                var child = new PropNode
                {
                    Descriptor = null,
                    Component = value,
                    Label = $"[{index}]",
                    Category = "Misc",
                    Depth = node.Depth + 1,
                    ValueType = runtimeType,
                    ParentList = list,
                    ElementIndex = elementIndex,
                    Getter = () => list != null && elementIndex < list.Count ? list[elementIndex] : captured,
                    Setter = canEditElement ? v => list![elementIndex] = v : null
                };
                child.Expandable = IsExpandable(runtimeType, captured);
                node.Children.Add(child);
                index++;
            }

            return;
        }

        // Nested object: reflect its properties. Honor a custom TypeConverter
        // (e.g. ExpandableObjectConverter) that projects a different property set.
        var converter = TypeDescriptor.GetConverter(value);
        var props = converter.GetPropertiesSupported()
            ? converter.GetProperties(value)
            : TypeDescriptor.GetProperties(value);

        foreach (var pd in (props ?? new PropertyDescriptorCollection(Array.Empty<PropertyDescriptor>()))
                     .Cast<PropertyDescriptor>().Where(p => p.IsBrowsable)
                     .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            node.Children.Add(CreatePropertyNode(pd, value, node.Depth + 1));
        }
    }

    private static Type GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType() ?? typeof(object);

        foreach (var i in collectionType.GetInterfaces())
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
                return i.GetGenericArguments()[0];

        if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
            return collectionType.GetGenericArguments()[0];

        return typeof(object);
    }

    private static bool IsSimple(Type t)
        => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
           || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Guid) || t == typeof(SKColor);

    private static string CategoryOf(PropertyDescriptor pd)
        => string.IsNullOrWhiteSpace(pd.Category) ? "Misc" : pd.Category;

    #endregion

    #region Row generation

    private void RebuildRows()
    {
        CloseEditor();
        Items.Clear();

        GroupingEnabled = _propertySort == PropertySort.Categorized;

        if (_selectedObject == null)
        {
            Invalidate();
            return;
        }

        foreach (var node in _rootNodes)
            AppendRow(node, node.Category);

        Invalidate();
    }

    private void AppendRow(PropNode node, string rootCategory)
    {
        var item = new GridListItem { Tag = node, Name = node.Descriptor?.Name ?? node.Label };

        if (_propertySort == PropertySort.Categorized)
        {
            item.GroupKey = rootCategory;
            item.GroupText = rootCategory;
        }

        item.Cells.Add(new GridListCell
        {
            Text = FormatLabel(node),
            Icon = node.Expandable ? GetChevron(node.Expanded) : null
        });
        item.Cells.Add(new GridListCell { Text = FormatValue(node) });
        Items.Add(item);

        if (node.Expandable && node.Expanded)
        {
            EnsureChildren(node);
            foreach (var child in node.Children)
                AppendRow(child, rootCategory);
        }
    }

    private static string FormatLabel(PropNode node)
    {
        // Depth is conveyed by indentation; the expand indicator is a drawn chevron icon on the
        // name cell (see GetChevron), matching the group-header styling.
        return new string(' ', node.Depth * 3) + node.Label;
    }

    private string FormatValue(PropNode node)
    {
        object? value;
        try { value = node.GetValue(); }
        catch { return string.Empty; }

        if (value == null)
            return node.Expandable ? string.Empty : "(none)";

        if (value is string s)
            return s;

        if (value is IEnumerable and not string)
        {
            EnsureChildren(node);
            var typeName = value.GetType().Name;
            var arity = typeName.IndexOf('`');
            if (arity > 0)
                typeName = typeName[..arity];
            return $"{typeName} ({node.Children.Count})";
        }

        var t = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
        if (!IsSimple(t) && node.Expandable)
            return value.GetType().Name;

        if (node.Descriptor?.Converter is { } conv && conv.CanConvertTo(typeof(string)))
        {
            try { return conv.ConvertToString(value) ?? value.ToString() ?? string.Empty; }
            catch { /* fall through */ }
        }

        return value.ToString() ?? string.Empty;
    }

    private void RefreshValues()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Tag is PropNode node && Items[i].Cells.Count > ValueColumn)
                Items[i].Cells[ValueColumn].Text = FormatValue(node);
        }
    }

    #endregion

    #region Interaction

    private void OnCellClicked(object? sender, GridListCellEventArgs e)
    {
        if (e.Item.Tag is not PropNode node)
        {
            CommitEditor();
            return;
        }

        // Expandable rows: toggle on either column.
        if (node.Expandable)
        {
            CommitEditor();
            ToggleNodeAnimated(node);
            return;
        }

        if (e.ColumnIndex != ValueColumn || node.Setter == null || !CanEdit(node.ValueType))
        {
            CommitEditor();
            return;
        }

        BeginEdit(e.ItemIndex, e.Item, node);
    }

    private static bool CanEdit(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(bool) || t.IsEnum || IsNumeric(t) || t == typeof(string)
            || t == typeof(DateTime) || t == typeof(SKColor) || t == typeof(System.Drawing.Color))
            return true;

        var converter = TypeDescriptor.GetConverter(t);
        return converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string));
    }

    private static bool IsNumeric(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong)
            || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    private void BeginEdit(int itemIndex, GridListItem item, PropNode node)
    {
        CommitEditor();

        var bounds = GetCellBounds(itemIndex, ValueColumn);
        if (bounds.IsEmpty)
            return;

        _editingNode = node;
        _editingItem = item;
        _editorBounds = bounds;

        var t = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;

        if (t == typeof(bool) || t.IsEnum)
            ShowChoiceDropDown(node, bounds, t);
        else if (IsNumeric(t))
            ShowNumericEditor(node, bounds, t);
        else if (t == typeof(DateTime))
            ShowDateEditor(node, bounds);
        else if (t == typeof(SKColor) || t == typeof(System.Drawing.Color))
            ShowColorEditor(node, bounds);
        else
            ShowTextEditor(node, bounds);
    }

    private void ShowDateEditor(PropNode node, SKRect bounds)
    {
        var editor = EnsureDateEditor();
        _suppressEditorEvents = true;
        try { editor.Value = node.GetValue() is DateTime dt ? dt : DateTime.Now; }
        catch { editor.Value = DateTime.Now; }
        _suppressEditorEvents = false;

        PlaceEditor(editor, bounds);
        editor.Focus();

        // Open the calendar immediately — the user clicked the value to edit it, so don't require
        // a second click on the picker field.
        editor.ShowDropDown();
    }

    private void ShowColorEditor(PropNode node, SKRect bounds)
    {
        var host = GetParentWindow();
        if (host == null)
        {
            ShowTextEditor(node, bounds);
            return;
        }

        var picker = EnsureColorEditor(host);
        _suppressEditorEvents = true;
        picker.SelectedColor = ToSKColor(node.GetValue());
        _suppressEditorEvents = false;

        // Anchor the popup just below the value cell, clamped to the host window.
        var screen = PointToScreen(new SKPoint(bounds.Left, bounds.Bottom + 2f));
        var client = host.PointToClient(screen);
        var x = Math.Clamp(client.X, 4f, Math.Max(4f, host.Width - picker.Width - 4f));
        var y = Math.Clamp(client.Y, 4f, Math.Max(4f, host.Height - picker.Height - 4f));
        picker.Location = new SKPoint(x, y);
        picker.Visible = true;
        picker.BringToFront();
        picker.Focus();
    }

    private static SKColor ToSKColor(object? value)
    {
        if (value is SKColor c) return c;
        if (value is System.Drawing.Color d) return new SKColor(d.R, d.G, d.B, d.A);
        return SKColors.White;
    }

    private void ShowTextEditor(PropNode node, SKRect bounds)
    {
        var editor = EnsureTextEditor();
        _suppressEditorEvents = true;
        editor.Text = FormatValue(node);
        _suppressEditorEvents = false;

        PlaceEditor(editor, bounds);
        editor.Focus();
        editor.SelectAll();
    }

    private void ShowNumericEditor(PropNode node, SKRect bounds, Type numericType)
    {
        var editor = EnsureNumericEditor();
        _suppressEditorEvents = true;

        var isFloating = numericType == typeof(float) || numericType == typeof(double) || numericType == typeof(decimal);
        editor.DecimalPlaces = isFloating ? 4 : 0;
        editor.Minimum = decimal.MinValue / 2m;
        editor.Maximum = decimal.MaxValue / 2m;

        try
        {
            var current = node.GetValue();
            editor.Value = current == null ? 0m : Convert.ToDecimal(current, CultureInfo.CurrentCulture);
        }
        catch { editor.Value = 0m; }

        _suppressEditorEvents = false;

        PlaceEditor(editor, bounds);
        editor.Focus();
    }

    private void ShowChoiceDropDown(PropNode node, SKRect bounds, Type type)
    {
        var options = type == typeof(bool)
            ? new[] { bool.TrueString, bool.FalseString }
            : Enum.GetNames(type);

        var menu = new ContextMenuStrip();
        foreach (var option in options)
        {
            var captured = option;
            menu.AddItem(new MenuItem(option, (_, _) => ApplyChoice(node, captured)));
        }

        // ContextMenuStrip.Show expects SCREEN coordinates (it converts back to client internally).
        var anchor = PointToScreen(new SKPoint(bounds.Left, bounds.Bottom));
        menu.Closed += (_, _) => { _editingNode = null; _editingItem = null; };
        menu.Show(this, anchor);
        _dropDown = menu;
    }

    private void ApplyChoice(PropNode node, string text)
    {
        if (node.Setter == null)
            return;

        try
        {
            var t = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
            object? converted = t.IsEnum ? Enum.Parse(t, text, ignoreCase: true)
                : t == typeof(bool) ? bool.Parse(text)
                : text;

            var oldValue = node.GetValue();
            if (!Equals(converted, oldValue))
            {
                node.Setter(converted);
                RaiseValueChanged(node, oldValue);
            }
        }
        catch { /* ignore invalid choice */ }
    }

    private void PlaceEditor(ElementBase editor, SKRect bounds)
    {
        editor.Location = new SKPoint(bounds.Left, bounds.Top);
        editor.Size = new SKSize(Math.Max(24f, bounds.Width), Math.Max(18f, bounds.Height));
        editor.Visible = true;
        editor.BringToFront();
        Invalidate();
    }

    private TextBox EnsureTextEditor()
    {
        if (_textEditor != null)
            return _textEditor;

        // No vertical padding so the (MiddleLeft) text stays vertically centered in the short cell
        // rather than sitting low.
        _textEditor = new TextBox { Visible = false, Radius = new Radius(4), Border = new Thickness(1), Padding = new Thickness(8, 0, 8, 0) };
        _textEditor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; CommitEditor(); Focus(); }
            else if (e.KeyCode == Keys.Escape) { e.Handled = true; CloseEditor(); Focus(); }
        };
        _textEditor.LostFocus += (_, _) => { if (!_suppressEditorEvents) CommitEditor(); };
        Controls.Add(_textEditor);
        return _textEditor;
    }

    private NumericUpDown EnsureNumericEditor()
    {
        if (_numericEditor != null)
            return _numericEditor;

        _numericEditor = new NumericUpDown { Visible = false };
        _numericEditor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; CommitEditor(); Focus(); }
            else if (e.KeyCode == Keys.Escape) { e.Handled = true; CloseEditor(); Focus(); }
        };
        _numericEditor.LostFocus += (_, _) => { if (!_suppressEditorEvents) CommitEditor(); };
        Controls.Add(_numericEditor);
        return _numericEditor;
    }

    private DatePicker EnsureDateEditor()
    {
        if (_dateEditor != null)
            return _dateEditor;

        _dateEditor = new DatePicker { Visible = false };
        _dateEditor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; CommitEditor(); Focus(); }
            else if (e.KeyCode == Keys.Escape) { e.Handled = true; CloseEditor(); Focus(); }
        };
        // While the calendar drop-down is open the DatePicker loses focus to the popup; don't commit
        // (which would hide the editor). Commit when the user picks a date and the drop-down closes.
        _dateEditor.DropDownOpened += (_, _) => _dateDropDownOpen = true;
        _dateEditor.DropDownClosed += (_, _) => { _dateDropDownOpen = false; if (!_suppressEditorEvents) CommitEditor(); };
        _dateEditor.LostFocus += (_, _) => { if (!_suppressEditorEvents && !_dateDropDownOpen) CommitEditor(); };
        Controls.Add(_dateEditor);
        return _dateEditor;
    }

    private ColorPicker EnsureColorEditor(WindowBase host)
    {
        if (_colorEditor != null && ReferenceEquals(_colorHost, host))
            return _colorEditor;

        _colorEditor?.Dispose();
        _colorHost = host;
        _colorEditor = new ColorPicker
        {
            Visible = false,
            ZOrder = int.MaxValue,
            Size = new SKSize(300, 340)
        };
        _colorEditor.SelectedColorCommitted += (_, _) => { if (!_suppressEditorEvents) CommitEditor(); };
        _colorEditor.LostFocus += (_, _) => { if (!_suppressEditorEvents) CommitEditor(); };
        host.Controls.Add(_colorEditor);
        return _colorEditor;
    }

    private void CommitEditor()
    {
        var node = _editingNode;
        if (node?.Setter == null)
        {
            CloseEditor();
            return;
        }

        try
        {
            var t = Nullable.GetUnderlyingType(node.ValueType) ?? node.ValueType;
            object? converted = null;
            var haveValue = false;

            if (_numericEditor is { Visible: true })
            {
                converted = Convert.ChangeType(_numericEditor.Value, t, CultureInfo.CurrentCulture);
                haveValue = true;
            }
            else if (_dateEditor is { Visible: true })
            {
                converted = _dateEditor.Value;
                haveValue = true;
            }
            else if (_colorEditor is { Visible: true })
            {
                var picked = _colorEditor.SelectedColor;
                converted = t == typeof(System.Drawing.Color)
                    ? System.Drawing.Color.FromArgb(picked.Alpha, picked.Red, picked.Green, picked.Blue)
                    : picked;
                haveValue = true;
            }
            else if (_textEditor is { Visible: true })
            {
                var text = _textEditor.Text ?? string.Empty;
                var converter = node.Descriptor?.Converter ?? TypeDescriptor.GetConverter(t);
                converted = converter.ConvertFromString(null, CultureInfo.CurrentCulture, text);
                haveValue = true;
            }

            if (haveValue)
            {
                var oldValue = node.GetValue();
                if (!Equals(converted, oldValue))
                {
                    node.Setter(converted);
                    RaiseValueChanged(node, oldValue);
                }
            }
        }
        catch
        {
            // Invalid input (e.g. text in a converter that can't parse it) — keep the previous value.
        }

        CloseEditor();
    }

    private void RaiseValueChanged(PropNode node, object? oldValue)
    {
        if (_editingItem is { } item && item.Cells.Count > ValueColumn)
            item.Cells[ValueColumn].Text = FormatValue(node);
        else
            RefreshValues();

        PropertyValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(node.Descriptor, oldValue));
        Invalidate();
    }

    private void CloseEditor()
    {
        _suppressEditorEvents = true;

        if (_textEditor != null) _textEditor.Visible = false;
        if (_numericEditor != null) _numericEditor.Visible = false;
        if (_dateEditor != null) _dateEditor.Visible = false;
        if (_colorEditor != null) _colorEditor.Visible = false;

        _editingNode = null;
        _editingItem = null;
        _editorBounds = SKRect.Empty;
        _suppressEditorEvents = false;

        Invalidate();
    }

    private bool HasInlineEditorOpen =>
        _textEditor?.Visible == true || _numericEditor?.Visible == true || _dateEditor?.Visible == true;

    public override void OnMouseDown(MouseEventArgs e)
    {
        // Commit an inline editor when clicking away from it (the color popup and drop-downs manage
        // their own lifecycle via LostFocus / selection).
        if (_editingNode != null && HasInlineEditorOpen && !_editorBounds.Contains(e.Location))
            CommitEditor();

        // Right-click a collection or one of its elements to add/remove items.
        if (e.Button == MouseButtons.Right)
        {
            var hit = HitTest(e.Location);
            if (hit.Item?.Tag is PropNode node && TryShowCollectionMenu(node, e.Location))
                return;
        }

        base.OnMouseDown(e);
    }

    private bool TryShowCollectionMenu(PropNode node, SKPoint location)
    {
        var menu = new ContextMenuStrip();
        var added = false;

        // "Add" on a mutable collection row.
        if (node.CollectionValue is { IsReadOnly: false, IsFixedSize: false } list)
        {
            menu.AddItem(new MenuItem("Add item", (_, _) => AddCollectionItem(node, list)));
            added = true;
        }

        // Element rows: remove (resizable lists) and reorder (any writable list — a swap also
        // works for fixed-size arrays).
        if (node.ParentList is { IsReadOnly: false } parent && node.ElementIndex >= 0)
        {
            if (!parent.IsFixedSize)
            {
                menu.AddItem(new MenuItem("Remove item", (_, _) => RemoveCollectionItem(node, parent)));
                added = true;
            }

            if (node.ElementIndex > 0)
            {
                menu.AddItem(new MenuItem("Move up", (_, _) => MoveCollectionItem(node, parent, -1)));
                added = true;
            }

            if (node.ElementIndex < parent.Count - 1)
            {
                menu.AddItem(new MenuItem("Move down", (_, _) => MoveCollectionItem(node, parent, +1)));
                added = true;
            }
        }

        if (!added)
        {
            menu.Dispose();
            return false;
        }

        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(this, PointToScreen(location));
        return true;
    }

    private void AddCollectionItem(PropNode collectionNode, IList list)
    {
        try
        {
            var elementType = GetCollectionElementType(list.GetType());
            list.Add(CreateDefault(elementType));
            RebuildAfterCollectionChange(collectionNode);
        }
        catch { /* fixed / typed collections may reject the default element */ }
    }

    private void MoveCollectionItem(PropNode elementNode, IList list, int delta)
    {
        try
        {
            var i = elementNode.ElementIndex;
            var j = i + delta;
            if (i < 0 || j < 0 || i >= list.Count || j >= list.Count)
                return;

            (list[j], list[i]) = (list[i], list[j]);
            RebuildAfterCollectionChange(FindCollectionNode(list) ?? elementNode);
        }
        catch { /* ignore */ }
    }

    private void RemoveCollectionItem(PropNode elementNode, IList list)
    {
        try
        {
            if (elementNode.ElementIndex >= 0 && elementNode.ElementIndex < list.Count)
            {
                list.RemoveAt(elementNode.ElementIndex);
                // Rebuild the owning collection node (find it among root/children).
                RebuildAfterCollectionChange(FindCollectionNode(list) ?? elementNode);
            }
        }
        catch { /* ignore */ }
    }

    private void RebuildAfterCollectionChange(PropNode collectionNode)
    {
        collectionNode.ChildrenBuilt = false;
        collectionNode.Expanded = true;
        EnsureChildren(collectionNode);
        RebuildRows();
    }

    private PropNode? FindCollectionNode(IList list)
    {
        PropNode? Search(IEnumerable<PropNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (ReferenceEquals(n.CollectionValue, list))
                    return n;
                var found = Search(n.Children);
                if (found != null)
                    return found;
            }
            return null;
        }
        return Search(_rootNodes);
    }

    private static object? CreateDefault(Type type)
    {
        if (type == typeof(string))
            return string.Empty;
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        if (type == typeof(object))
            return string.Empty;

        try { return Activator.CreateInstance(type); }
        catch { return null; }
    }

    public override void OnMouseWheel(MouseEventArgs e)
    {
        if (_editingNode != null && HasInlineEditorOpen)
            CommitEditor();

        base.OnMouseWheel(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _editingNode != null)
            return;

        if (e.KeyCode is Keys.Enter or Keys.F2)
        {
            var index = SelectedIndex;
            if (index >= 0 && index < Items.Count && Items[index].Tag is PropNode node)
            {
                if (node.Expandable)
                {
                    ToggleNodeAnimated(node);
                    e.Handled = true;
                }
                else if (node.Setter != null && CanEdit(node.ValueType))
                {
                    BeginEdit(index, Items[index], node);
                    e.Handled = true;
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textEditor?.Dispose();
            _numericEditor?.Dispose();
            _dateEditor?.Dispose();
            _colorEditor?.Dispose();
            _dropDown?.Dispose();
            _revealAnim.Dispose();
            _chevronRight?.Dispose();
            _chevronDown?.Dispose();
        }

        base.Dispose(disposing);
    }

    #endregion
}
