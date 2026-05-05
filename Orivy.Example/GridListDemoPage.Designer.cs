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
