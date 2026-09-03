using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Orivy.Studio.Toolbox;

/// <summary>One placeable control type discovered from Orivy.Controls.</summary>
public sealed record ControlEntry(Type Type, string DisplayName, string Category, string Description)
{
    public ElementBase CreateInstance()
    {
        var control = (ElementBase)Activator.CreateInstance(Type)!;
        ControlCatalog.ApplyDesignDefaults(control);
        return control;
    }
}

/// <summary>
/// Discovers every instantiable control in the Orivy assembly via reflection — the toolbox is never
/// a static list. A control added to Orivy.Controls appears in the Studio automatically; the
/// category/description maps below only refine presentation and fall back to sensible defaults.
/// </summary>
public static class ControlCatalog
{
    /// <summary>Infrastructure/overlay types that make no sense as design-time toolbox entries.</summary>
    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
    {
        nameof(ScrollBar),          // created internally by scrollable hosts
        nameof(ContextMenuStrip),   // floating overlay, not a placeable child
        "NotificationTray",
        "MessageBox",
    };

    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        [nameof(Button)] = "Buttons",
        ["ToggleButton"] = "Buttons",
        [nameof(SwitchButton)] = "Buttons",
        ["ButtonGroup"] = "Buttons",
        [nameof(TextBox)] = "Inputs",
        [nameof(NumericUpDown)] = "Inputs",
        [nameof(ComboBox)] = "Inputs",
        [nameof(CheckBox)] = "Inputs",
        ["RadioButton"] = "Inputs",
        [nameof(DatePicker)] = "Inputs",
        [nameof(TimePicker)] = "Inputs",
        [nameof(TrackBar)] = "Inputs",
        [nameof(ColorPicker)] = "Inputs",
        [nameof(ListBox)] = "Data",
        ["CheckedListBox"] = "Data",
        [nameof(GridList)] = "Data",
        [nameof(PropertyGrid)] = "Data",
        [nameof(TreeView)] = "Data",
        [nameof(Element)] = "Layout",
        [nameof(Card)] = "Layout",
        ["Container"] = "Layout",
        ["SplitContainer"] = "Layout",
        ["Grid"] = "Layout",
        ["FlowLayout"] = "Layout",
        [nameof(TabView)] = "Layout",
        ["Accordion"] = "Layout",
        ["Collapse"] = "Layout",
        [nameof(Separator)] = "Layout",
        [nameof(Badge)] = "Display",
        ["Breadcrumb"] = "Display",
        [nameof(ProgressBar)] = "Display",
        ["MenuStrip"] = "Display",
    };

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        [nameof(Button)] = "Clickable push button with visual states.",
        [nameof(TextBox)] = "Single or multi-line text input.",
        [nameof(CheckBox)] = "Boolean check box with label.",
        [nameof(ComboBox)] = "Drop-down selection list.",
        [nameof(ListBox)] = "Scrollable list with single/multi selection.",
        [nameof(GridList)] = "Columns + rows list view (ListView equivalent).",
        [nameof(PropertyGrid)] = "Reflective object property editor.",
        [nameof(Element)] = "Plain panel / label surface.",
        [nameof(Card)] = "Titled content card container.",
        [nameof(TreeView)] = "Hierarchical node tree.",
        [nameof(DatePicker)] = "Date field with calendar drop-down.",
        [nameof(ProgressBar)] = "Progress indicator.",
    };

    /// <summary>Per-type default sizes so freshly dropped controls look reasonable.</summary>
    private static readonly Dictionary<string, SKSize> DefaultSizes = new(StringComparer.Ordinal)
    {
        [nameof(Button)] = new(120, 40),
        [nameof(TextBox)] = new(200, 30),
        [nameof(Element)] = new(200, 120),
        [nameof(Card)] = new(240, 150),
        [nameof(ListBox)] = new(190, 170),
        ["CheckedListBox"] = new(190, 170),
        [nameof(GridList)] = new(280, 170),
        [nameof(PropertyGrid)] = new(280, 220),
        [nameof(TreeView)] = new(200, 180),
        [nameof(ProgressBar)] = new(200, 14),
        [nameof(TrackBar)] = new(200, 32),
        [nameof(Separator)] = new(200, 12),
        [nameof(TabView)] = new(300, 200),
        [nameof(ColorPicker)] = new(300, 340),
        ["SplitContainer"] = new(300, 200),
    };

    public static IReadOnlyList<ControlEntry> Discover()
    {
        var assembly = typeof(ElementBase).Assembly;
        var entries = new List<ControlEntry>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsPublic || type.IsAbstract || !type.IsSubclassOf(typeof(ElementBase)))
                continue;
            if (type.IsGenericTypeDefinition)
                continue; // open generics (e.g. ButtonGroup<T>) can't be Activator-instantiated
            if (typeof(WindowBase).IsAssignableFrom(type))
                continue; // windows are documents, not children
            if (Excluded.Contains(type.Name))
                continue;
            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            // Strip the CLR generic arity suffix (e.g. "ButtonGroup`1" → "ButtonGroup").
            var displayName = type.Name;
            var backtick = displayName.IndexOf('`');
            if (backtick >= 0)
                displayName = displayName[..backtick];

            var category = Categories.TryGetValue(displayName, out var cat) ? cat : "General";
            var description = Descriptions.TryGetValue(displayName, out var desc) ? desc : $"Orivy {displayName} control.";
            entries.Add(new ControlEntry(type, displayName, category, description));
        }

        return entries
            .OrderBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    internal static void ApplyDesignDefaults(ElementBase control)
    {
        if (DefaultSizes.TryGetValue(control.GetType().Name, out var size))
            control.Size = size;
        else if (control.Width <= 1 || control.Height <= 1)
            control.Size = new SKSize(160, 40);

        // Seed a little content so data controls aren't empty white boxes on the canvas.
        switch (control)
        {
            case GridList grid when grid.Columns.Count == 0:
                grid.Columns.Add(new GridListColumn { Name = "col1", Text = "Name", Width = 110f });
                grid.Columns.Add(new GridListColumn { Name = "col2", Text = "Value", SizeMode = GridListColumnSizeMode.Fill });
                grid.Items.Add(new[] { "Alpha", "1" });
                grid.Items.Add(new[] { "Beta", "2" });
                break;
            case ListBox list when list.Items.Count == 0:
                list.Items.AddRange("Item 1", "Item 2", "Item 3");
                break;
        }
    }
}
