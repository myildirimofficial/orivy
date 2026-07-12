using Orivy;
using Orivy.Controls;
using SkiaSharp;

namespace Orivy.Example;

internal sealed partial class GridListDemoPage
{
    private Element gridListStatus = null!;
    private GridList gridListPrimary = null!;
    private GridList gridListCompact = null!;
    private Button gridListToggleHeaderButton = null!;
    private Button gridListToggleStickyButton = null!;
    private Button gridListToggleGroupingButton = null!;
    private Button gridListToggleGridLinesButton = null!;
    private Button gridListToggleRowResizeButton = null!;

    private ListBox listBoxSingle = null!;
    private ListBox listBoxOwnerDraw = null!;
    private ListBox checkedListBoxDemo = null!;
    private Element listBoxStatus = null!;

    private PropertyGrid propertyGridDemo = null!;
    private Element propertyGridDescription = null!;

    private PropertyGrid listMgmtGrid = null!;
    private TextBox listMgmtInput = null!;
    private Button listMgmtAdd = null!;
    private Button listMgmtRemove = null!;
    private Button listMgmtUp = null!;
    private Button listMgmtDown = null!;
    private Button listMgmtClear = null!;
    private Element listMgmtStatus = null!;

    private static Element BuildListColumn(string caption, ElementBase control, bool last)
    {
        var column = new Element
        {
            Dock = Orivy.DockStyle.Left,
            Width = 320,
            Margin = new(0, 0, last ? 0 : 16, 0),
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0)
        };

        var label = new Element
        {
            Text = caption,
            Dock = Orivy.DockStyle.Top,
            Height = 26,
            Margin = new(2, 0, 0, 8),
            BackColor = SKColors.Transparent,
            ForeColor = ColorScheme.ForeColor,
            Border = new(0),
            Radius = new(0),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 11f)
        };

        control.Dock = Orivy.DockStyle.Fill;
        column.Controls.Add(control);
        column.Controls.Add(label);
        return column;
    }

    private void InitializeComponent()
    {
        Text = "Grid List";
        Name = "panel6";
        Padding = new(20);
        Dock = Orivy.DockStyle.Fill;
        Radius = new(0);
        Border = new(0);
        AutoScroll = true;
        AutoScrollMargin = new(0, 24);


        var gridListHeader = new Element
        {
            Name = "gridListHeader",
            Text = "Grid List Surface\nAnimated groups, sticky header, column resize, optional row resize and denser typography are all visible without leaving this page.",
            Dock = Orivy.DockStyle.Top,
            Height = 124,
            Padding = new(24),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(92),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 15f)
        };

        var gridListToolbar = new Element
        {
            Name = "gridListToolbar",
            Dock = Orivy.DockStyle.Top,
            Height = 72,
            Padding = new(10),
            Margin = new(0, 0, 0, 16),
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(92),
            BackColor = ColorScheme.Surface
        };

        gridListToggleHeaderButton = new Button
        {
            Name = "gridListToggleHeaderButton",
            Text = "Header: On",
            Dock = Orivy.DockStyle.Left,
            Width = 128,
            Margin = new(0, 0, 10, 0),
        };

        gridListToggleStickyButton = new Button
        {
            Name = "gridListToggleStickyButton",
            Text = "Sticky: On",
            Dock = Orivy.DockStyle.Left,
            Width = 128,
            Margin = new(0, 0, 10, 0),
        };

        gridListToggleGroupingButton = new Button
        {
            Name = "gridListToggleGroupingButton",
            Text = "Grouping: On",
            Dock = Orivy.DockStyle.Left,
            Width = 144,
            Margin = new(0, 0, 10, 0),
        };

        gridListToggleGridLinesButton = new Button
        {
            Name = "gridListToggleGridLinesButton",
            Text = "Grid Lines: On",
            Dock = Orivy.DockStyle.Left,
            Width = 152,
            Margin = new(0, 0, 0, 0),
        };

        gridListToggleRowResizeButton = new Button
        {
            Name = "gridListToggleRowResizeButton",
            Text = "Row Resize: Off",
            Dock = Orivy.DockStyle.Left,
            Width = 164,
            Margin = new(0, 0, 10, 0),
        };

        gridListToolbar.Controls.Add(gridListToggleGridLinesButton);
        gridListToolbar.Controls.Add(gridListToggleRowResizeButton);
        gridListToolbar.Controls.Add(gridListToggleGroupingButton);
        gridListToolbar.Controls.Add(gridListToggleStickyButton);
        gridListToolbar.Controls.Add(gridListToggleHeaderButton);

        var gridListWorkspace = new Element
        {
            Name = "gridListWorkspace",
            Dock = Orivy.DockStyle.Top,
            Height = 860,
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0),
            Margin = new(0, 0, 0, 8)
        };

        var gridListInspectorRail = new Element
        {
            Name = "gridListInspectorRail",
            Dock = Orivy.DockStyle.Right,
            Width = 328,
            Padding = new(0),
            Margin = new(16, 0, 0, 0),
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0)
        };

        gridListStatus = new Element
        {
            Name = "gridListStatus",
            Text = "Status\nReady",
            Dock = Orivy.DockStyle.Top,
            Height = 110,
            Padding = new(18),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(20),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(96),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI Semibold") ?? SKTypeface.Default, 11f)
        };

        var gridListPrimaryShell = new Element
        {
            Name = "gridListPrimaryShell",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(16),
            Margin = new(0),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var gridListPrimaryIntro = new Element
        {
            Name = "gridListPrimaryIntro",
            Text = "Operations Board\nScroll inside the grid to verify sticky header. Resize columns from the header edge, then enable row resize from the toolbar to stretch the body rhythm.",
            Dock = Orivy.DockStyle.Top,
            Height = 84,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(78),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        gridListPrimary = new GridList
        {
            Name = "gridListPrimary",
            Dock = Orivy.DockStyle.Fill,
            Margin = new(0),
            Radius = new(14),
            Border = new(1),
            HeaderVisible = true,
            StickyHeader = true,
            GroupingEnabled = true,
            MultiSelect = true,
            FullRowSelect = true,
            CheckBoxes = false,
            AllowColumnResize = true,
            AllowRowResize = true,
            ShowGridLines = true,
            HeaderHeight = 42,
            RowHeight = 38,
            GroupHeaderHeight = 32,
            CellPadding = 11,
        };

        gridListPrimaryShell.Controls.Add(gridListPrimary);
        gridListPrimaryShell.Controls.Add(gridListPrimaryIntro);

        var gridListCompactShell = new Element
        {
            Name = "gridListCompactShell",
            Dock = Orivy.DockStyle.Top,
            Height = 286,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(22),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var gridListCompactHeader = new Element
        {
            Name = "gridListCompactHeader",
            Text = "Compact Feed\nHeaderless mode for icon-first rows and faster scanning.",
            Dock = Orivy.DockStyle.Top,
            Height = 72,
            Padding = new(16),
            Margin = new(0, 0, 0, 12),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(16),
            Border = new(1),
            BorderColor = ColorScheme.Success.WithAlpha(86),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        gridListCompact = new GridList
        {
            Name = "gridListCompact",
            Dock = Orivy.DockStyle.Fill,
            Radius = new(14),
            Border = new(1),
            HeaderVisible = false,
            StickyHeader = false,
            GroupingEnabled = false,
            MultiSelect = true,
            FullRowSelect = true,
            ShowGridLines = false,
            AllowRowResize = true,
            RowHeight = 36,
            CellPadding = 11,
        };

        gridListCompactShell.Controls.Add(gridListCompact);
        gridListCompactShell.Controls.Add(gridListCompactHeader);

        var gridListFooter = new Element
        {
            Name = "gridListFooter",
            Text = "Guide\n1. Scroll inside the primary grid to verify sticky header.\n2. Collapse a group and watch the rows animate.\n3. Enable row resize only when you want variable density.",
            Dock = Orivy.DockStyle.Fill,
            Padding = new(18),
            Margin = new(0),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(22),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        gridListInspectorRail.Controls.Add(gridListFooter);
        gridListInspectorRail.Controls.Add(gridListCompactShell);
        gridListInspectorRail.Controls.Add(gridListStatus);

        gridListWorkspace.Controls.Add(gridListPrimaryShell);
        gridListWorkspace.Controls.Add(gridListInspectorRail);

        // ── List Box showcase (single-select, owner-drawn, checked) ──
        var listBoxShell = new Element
        {
            Name = "listBoxShell",
            Dock = Orivy.DockStyle.Top,
            Height = 404,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var listBoxHeader = new Element
        {
            Name = "listBoxHeader",
            Text = "List Box\nSingle-select, owner-drawn rows, and a checked list. Click, Ctrl/Shift-click, scroll and use arrow keys.",
            Dock = Orivy.DockStyle.Top,
            Height = 72,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(78),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        listBoxStatus = new Element
        {
            Name = "listBoxStatus",
            Text = "Ready",
            Dock = Orivy.DockStyle.Bottom,
            Height = 42,
            Padding = new(14, 0, 14, 0),
            Margin = new(0, 12, 0, 0),
            BackColor = ColorScheme.SurfaceContainerLow,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var listBoxRow = new Element
        {
            Name = "listBoxRow",
            Dock = Orivy.DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0)
        };

        listBoxSingle = new ListBox { Name = "listBoxSingle", SelectionMode = SelectionMode.One };
        listBoxOwnerDraw = new ListBox { Name = "listBoxOwnerDraw", DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46 };
        // Checked list is now just a ListBox mode (CheckBoxes) rather than a separate class.
        checkedListBoxDemo = new ListBox { Name = "checkedListBoxDemo", CheckBoxes = true, CheckOnClick = true };

        listBoxRow.Controls.Add(BuildListColumn("Checked list", checkedListBoxDemo, last: true));
        listBoxRow.Controls.Add(BuildListColumn("Owner-drawn", listBoxOwnerDraw, last: false));
        listBoxRow.Controls.Add(BuildListColumn("Single select", listBoxSingle, last: false));

        listBoxShell.Controls.Add(listBoxRow);
        listBoxShell.Controls.Add(listBoxStatus);
        listBoxShell.Controls.Add(listBoxHeader);

        // ── Property Grid showcase ──
        var propertyGridShell = new Element
        {
            Name = "propertyGridShell",
            Dock = Orivy.DockStyle.Top,
            Height = 404,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var propertyGridHeader = new Element
        {
            Name = "propertyGridHeader",
            Text = "Property Grid\nReflects a live object's properties, grouped by category. Click a value (or press Enter/F2) to edit — text, numbers, booleans and enums all get inline editors.",
            Dock = Orivy.DockStyle.Top,
            Height = 82,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(78),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        propertyGridDescription = new Element
        {
            Name = "propertyGridDescription",
            Text = "Select a property to see its description.",
            Dock = Orivy.DockStyle.Bottom,
            Height = 52,
            Padding = new(14, 0, 14, 0),
            Margin = new(0, 12, 0, 0),
            BackColor = ColorScheme.SurfaceContainerLow,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        propertyGridDemo = new PropertyGrid
        {
            Name = "propertyGridDemo",
            Dock = Orivy.DockStyle.Fill,
            Radius = new(14),
            Border = new(1),
            PropertySort = PropertySort.Categorized
        };

        propertyGridShell.Controls.Add(propertyGridDemo);
        propertyGridShell.Controls.Add(propertyGridDescription);
        propertyGridShell.Controls.Add(propertyGridHeader);

        // ── List management showcase (add / remove / reorder / clear) ──
        var listMgmtShell = new Element
        {
            Name = "listMgmtShell",
            Dock = Orivy.DockStyle.Top,
            Height = 400,
            Padding = new(18),
            Margin = new(0, 0, 0, 16),
            BackColor = ColorScheme.Surface,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(24),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(88)
        };

        var listMgmtHeader = new Element
        {
            Name = "listMgmtHeader",
            Text = "PropertyGrid List Management\nManage the Servers collection inside the PropertyGrid: select an element row ([0], [1]…) then use the buttons — or right-click a row for Add / Remove / Move.",
            Dock = Orivy.DockStyle.Top,
            Height = 64,
            Padding = new(16),
            Margin = new(0, 0, 0, 14),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(18),
            Border = new(1),
            BorderColor = ColorScheme.Primary.WithAlpha(78),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        listMgmtStatus = new Element
        {
            Name = "listMgmtStatus",
            Text = "0 items.",
            Dock = Orivy.DockStyle.Bottom,
            Height = 40,
            Padding = new(14, 0, 14, 0),
            Margin = new(0, 12, 0, 0),
            BackColor = ColorScheme.SurfaceContainerLow,
            ForeColor = ColorScheme.ForeColor,
            Radius = new(14),
            Border = new(1),
            BorderColor = ColorScheme.Outline.WithAlpha(80),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 10.5f)
        };

        var listMgmtBody = new Element
        {
            Name = "listMgmtBody",
            Dock = Orivy.DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0)
        };

        listMgmtGrid = new PropertyGrid
        {
            Name = "listMgmtGrid",
            Dock = Orivy.DockStyle.Left,
            Width = 340,
            Margin = new(0, 0, 16, 0),
            Radius = new(14),
            Border = new(1),
            PropertySort = PropertySort.Categorized
        };

        var listMgmtActions = new Element
        {
            Name = "listMgmtActions",
            Dock = Orivy.DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new(0),
            Radius = new(0),
            Padding = new(0)
        };

        listMgmtInput = new TextBox
        {
            Name = "listMgmtInput",
            Dock = Orivy.DockStyle.Top,
            Height = 40,
            Margin = new(0, 0, 0, 12),
            PlaceholderText = "New item…"
        };

        listMgmtAdd = new Button { Name = "listMgmtAdd", Text = "Add", Dock = Orivy.DockStyle.Top, Height = 36, Margin = new(0, 0, 0, 8) };
        listMgmtRemove = new Button { Name = "listMgmtRemove", Text = "Remove", Dock = Orivy.DockStyle.Top, Height = 36, Margin = new(0, 0, 0, 8) };
        listMgmtUp = new Button { Name = "listMgmtUp", Text = "Move Up", Dock = Orivy.DockStyle.Top, Height = 36, Margin = new(0, 0, 0, 8) };
        listMgmtDown = new Button { Name = "listMgmtDown", Text = "Move Down", Dock = Orivy.DockStyle.Top, Height = 36, Margin = new(0, 0, 0, 8) };
        listMgmtClear = new Button { Name = "listMgmtClear", Text = "Clear", Dock = Orivy.DockStyle.Top, Height = 36, Margin = new(0, 0, 0, 0) };

        // Dock=Top stacks in reverse add order, so add bottom-most first.
        listMgmtActions.Controls.Add(listMgmtClear);
        listMgmtActions.Controls.Add(listMgmtDown);
        listMgmtActions.Controls.Add(listMgmtUp);
        listMgmtActions.Controls.Add(listMgmtRemove);
        listMgmtActions.Controls.Add(listMgmtAdd);
        listMgmtActions.Controls.Add(listMgmtInput);

        listMgmtBody.Controls.Add(listMgmtActions);
        listMgmtBody.Controls.Add(listMgmtGrid);

        listMgmtShell.Controls.Add(listMgmtBody);
        listMgmtShell.Controls.Add(listMgmtStatus);
        listMgmtShell.Controls.Add(listMgmtHeader);

        Controls.Add(listMgmtShell);
        Controls.Add(propertyGridShell);
        Controls.Add(listBoxShell);
        Controls.Add(gridListWorkspace);
        Controls.Add(gridListToolbar);
        Controls.Add(gridListHeader);

        gridListToggleHeaderButton.Click += GridListToggleHeaderButton_Click;
        gridListToggleStickyButton.Click += GridListToggleStickyButton_Click;
        gridListToggleGroupingButton.Click += GridListToggleGroupingButton_Click;
        gridListToggleGridLinesButton.Click += GridListToggleGridLinesButton_Click;
        gridListToggleRowResizeButton.Click += GridListToggleRowResizeButton_Click;

    }
}
