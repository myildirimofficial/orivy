using Orivy;
using Orivy.Controls;
using Orivy.Controls.RichText;
using Orivy.Studio.Canvas;
using Orivy.Studio.Documents;
using Orivy.Studio.History;
using Orivy.Studio.Panels;
using Orivy.Studio.Persistence;
using Orivy.Studio.Toolbox;
using Orivy.Windowing.Desktop.Windows;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Orivy.Studio shell — Figma-style multi-document visual designer.
/// Toolbar (undo/redo · file · export/preview · zoom · toggles · theme) on top, reflection-driven
/// Toolbox on the left, a TabView of design documents in the middle, and Layers + Layout + a live
/// PropertyGrid inspector on the right. The toolbox supports both double-click and drag-and-drop.
/// </summary>
public sealed class StudioWindow : Window
{
    private readonly TabView _documents = new() { Dock = DockStyle.Fill };
    private readonly TabView _sidebar = new() { Dock = DockStyle.Fill };
    private readonly Container _toolboxPage = new() { Dock = DockStyle.Fill, Border = new Thickness(0), Radius = new Radius(0) };
    private readonly Container _explorerPage = new() { Dock = DockStyle.Fill, Border = new Thickness(0), Radius = new Radius(0) };
    private readonly ToolboxPanel _toolbox = new() { Dock = DockStyle.Fill };
    private readonly ProjectExplorerList _explorer = new() { Dock = DockStyle.Fill };
    private readonly LayersPanel _layers;
    private readonly PropertyGrid _inspector = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized };
    private readonly LayoutHelperBar _layoutBar;
    private readonly DragLayer _dragLayer = new();
    private readonly StartScreen _startScreen = new();

    private readonly ToolbarButton _undoButton = new("undo", "Undo (Ctrl+Z)");
    private readonly ToolbarButton _redoButton = new("redo", "Redo (Ctrl+Y)");
    private readonly ToolbarButton _previewButton = new("play", "Preview");
    private readonly Element _zoomLabel;
    private readonly Element _statusHost;

    // Grid row/column helpers — only meaningful (and only shown) while the primary selection is a
    // Grid; the same add-row/add-column actions the on-canvas strips (see DesignSurface.DesignOverlay)
    // already offer, just reachable from the toolbar without having to find the Grid's own edges on
    // the canvas first.
    private readonly Element _gridToolsGroup = new()
    {
        Dock = DockStyle.Left, Visible = false, Width = 168,
        BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0),
        Margin = new Thickness(0, 0, 8, 0),
    };

    /// <summary>Ready-made row/column layouts a user can drop onto a Grid in one click instead of
    /// building common shapes (an even split, a header/content/footer stack, a fixed sidebar) by hand
    /// one Add-Row/Add-Column click at a time. Replaces whichever axis (or both) the preset touches;
    /// leaves the other axis alone.</summary>
    private static readonly (string Label, Action<Grid> Apply)[] GridPresets =
    {
        ("2 Columns", g => SetColumns(g, GridLength.FromStar(), GridLength.FromStar())),
        ("3 Columns", g => SetColumns(g, GridLength.FromStar(), GridLength.FromStar(), GridLength.FromStar())),
        ("Sidebar + Content", g => SetColumns(g, GridLength.FromPixels(200), GridLength.FromStar())),
        ("2 Rows", g => SetRows(g, GridLength.FromStar(), GridLength.FromStar())),
        ("3 Rows", g => SetRows(g, GridLength.FromStar(), GridLength.FromStar(), GridLength.FromStar())),
        ("Header / Content / Footer", g => SetRows(g, GridLength.Auto, GridLength.FromStar(), GridLength.Auto)),
    };

    private static void SetColumns(Grid grid, params GridLength[] widths)
    {
        grid.ColumnDefinitions.Clear();
        foreach (var w in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
    }

    private static void SetRows(Grid grid, params GridLength[] heights)
    {
        grid.RowDefinitions.Clear();
        foreach (var h in heights)
            grid.RowDefinitions.Add(new RowDefinition { Height = h });
    }

    private DesignSurface _active = null!;
    private int _documentCounter;
    private bool _suppressInspectorCommit;


    /// <summary>
    /// The Visual-Studio-style "preview" tab: browsing files in the Explorer reuses this single tab
    /// instead of stacking a new permanent one per file clicked, so navigating around a folder
    /// doesn't leave a trail of tabs behind. It stops being reusable — is "promoted" to a normal,
    /// permanent tab — the moment the user actually edits it (see <see cref="WireDirtyPromotion"/>),
    /// or is left alone entirely by explicit actions (File ▸ Open, New, Recent) which always
    /// open a real tab of their own, matching how Visual Studio's own preview tab behaves. Untyped as
    /// <see cref="IStudioDocument"/> rather than <see cref="DesignDocument"/> specifically — a design
    /// and a plain text file both use the same single preview slot, just never at the same time (see
    /// <see cref="OpenPath"/>).
    /// </summary>
    private IStudioDocument? _previewDocument;

    public StudioWindow()
    {
        Text = "Orivy Studio";
        ClientSize = new SKSize(1440, 900);
        MinimumSize = new SKSize(1024, 640);
        StartPosition = FormStartPosition.CenterScreen;
        ShowIcon = true;
        this.DrawTitleBorder = false;

        // Tabbed is the theme meant to cooperate with a TitleBar-mode TabView (see BuildLayout);
        // it falls back to a flat surface automatically on older Windows.
        WindowThemeType = WindowThemeType.Tabbed;

        _zoomLabel = new Element
        {
            Text = "100%", Dock = DockStyle.Left, Width = 44,
            Border = new Thickness(0), Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Tint(_zoomLabel, foreground: () => ColorScheme.ForeColor.WithAlpha(210));

        _statusHost = new Element
        {
            Dock = DockStyle.Bottom, Height = 34, Padding = new Thickness(16, 0, 16, 0),
            Border = new Thickness(0, 1, 0, 0),
            Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        };
        Tint(_statusHost,
            background: () => ColorScheme.SurfaceContainerHigh,
            foreground: () => ColorScheme.ForeColor.WithAlpha(190),
            border: () => ColorScheme.Outline.WithAlpha(50));

        // First document must exist before panels bind to it.
        var firstDoc = NewDocument();
        _active = firstDoc.Surface;
        _layers = new LayersPanel(_active) { Dock = DockStyle.Top, Height = 230, Margin = new Thickness(0, 0, 0, 10) };
        _layoutBar = new LayoutHelperBar(() => _active) { Dock = DockStyle.Top, Height = 92, Margin = new Thickness(0, 0, 0, 10) };

        BuildLayout();
        TabView = _documents; // hosts the tab strip in the native title bar (TabViewMode.TitleBar)
        AttachSurface(_active);
        WireShell();

        _documents.SelectedTab = firstDoc;
        SwitchActive(firstDoc.Surface);

        if (Environment.GetEnvironmentVariable("ORIVY_STUDIO_SEED") == "1")
            SeedDemoLayout();

        if (Environment.GetEnvironmentVariable("ORIVY_STUDIO_TEST_FOLDER") is { Length: > 0 } testFolder)
            _explorer.RootFolder = testFolder;
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var toolbar = Panel(DockStyle.Top, height: 48);
        toolbar.Padding = new Thickness(12, 8, 12, 8);
        toolbar.Margin = new Thickness(0);
        toolbar.Border = new Thickness(0, 0, 0, 1);
        Tint(toolbar, background: () => SKColors.Transparent, border: () => ColorScheme.Outline.WithAlpha(50));

        var newDocButton = new ToolbarButton("new-doc", "New document");
        var newButton = new ToolbarButton("new", "New (clear canvas)");
        var openButton = new ToolbarButton("open", "Open folder…");
        var saveButton = new ToolbarButton("save", "Save (Ctrl+S)");
        var exportButton = new ToolbarButton("export", "Export designer code");
        var zoomOut = new ToolbarButton("zoom-out", "Zoom out", 28f);
        var zoomIn = new ToolbarButton("zoom-in", "Zoom in", 28f);
        var zoomFit = new ToolbarButton("zoom-fit", "Fit to view", 28f);
        var gridToggle = new ToolbarButton("grid", "Show grid", 28f) { CheckOnClick = true, Checked = true };
        var snapToggle = new ToolbarButton("snap", "Snap to grid", 28f) { CheckOnClick = true, Checked = true };
        var guidesToggle = new ToolbarButton("guides", "Smart guides", 28f) { CheckOnClick = true, Checked = true };
        var themeToggle = new ToolbarButton("moon", "Toggle dark mode", 28f) { CheckOnClick = true, Checked = ColorScheme.IsDarkMode };
        var randomizeToggle = new ToolbarButton("shuffle", "Randomize backgrounds", 28f) { CheckOnClick = true };

        // Same flat, transparent-until-hovered skin as the icon-only toolbar buttons around them
        // (ToolbarButton.ApplyFlatSkin) — Button's own default is a bold filled pill meant for the
        // canvas, not this toolbar, and looked completely out of place sitting next to gridToggle/
        // snapToggle/etc.
        var addGridRowButton = new Button { Text = "+ Row", Dock = DockStyle.Left, Width = 52, Height = 28, Margin = new Thickness(0, 0, 4, 0), ToolTipText = "Add row to selected Grid" };
        var addGridColumnButton = new Button { Text = "+ Col", Dock = DockStyle.Left, Width = 52, Height = 28, Margin = new Thickness(0, 0, 4, 0), ToolTipText = "Add column to selected Grid" };
        var gridPresetsButton = new Button { Text = "Presets ▾", Dock = DockStyle.Left, Width = 56, Height = 28, ToolTipText = "Apply a ready-made row/column layout" };
        ToolbarButton.ApplyFlatSkin(addGridRowButton);
        ToolbarButton.ApplyFlatSkin(addGridColumnButton);
        ToolbarButton.ApplyFlatSkin(gridPresetsButton);
        _gridToolsGroup.Controls.Add(gridPresetsButton);
        _gridToolsGroup.Controls.Add(addGridColumnButton);
        _gridToolsGroup.Controls.Add(addGridRowButton);

        var sortPropertiesButton = new Button
        {
            Text = "Kategori",
            Dock = DockStyle.Right,
            Width = 70,
            Height = 24,
            Margin = new Thickness(0, 2, 0, 2),
            AutoSize = false,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            CheckOnClick = true,
            OpenDropDownOnClick = false,
            ToolTipText = "Alfabetik sırala",
        };
        ToolbarButton.ApplyFlatSkin(sortPropertiesButton);
        sortPropertiesButton.CheckedChanged += (_, _) =>
        {
            var alphabetical = sortPropertiesButton.Checked;
            sortPropertiesButton.Text = alphabetical ? "A–Z" : "Kategori";
            sortPropertiesButton.SetToolTip(alphabetical ? "Kategorilere göre sırala" : "Alfabetik sırala");
            _inspector.PropertySort = alphabetical ? PropertySort.Alphabetical : PropertySort.Categorized;
        };
        sortPropertiesButton.Checked = _inspector.PropertySort == PropertySort.Alphabetical;

        // Grid/snap spacing was a fixed 8px with no way to change it — a plain NumericUpDown next to
        // the toggles it controls, rather than a whole settings dialog for one number.
        var gridSizeField = new NumericUpDown
        {
            Dock = DockStyle.Left,
            Size = new SKSize(56, 28),
            Margin = new Thickness(0, 0, 4, 0),
            Minimum = 2,
            Maximum = 64,
            Value = 4,
            ToolTipText = "Grid / snap size (px)",
        };

        // A compact segmented "pill" for the zoom cluster instead of four loose buttons. Positioned
        // with explicit Location/Size (Dock=None) rather than stacked Dock=Left — the reverse-order
        // docking quirk the rest of the toolbar relies on turned out unreliable for this tightly
        // packed a 4-in-a-row case: the last child to claim space could end up with zero width and
        // silently fail to render.
        const float zoomChildHeight = 28f;
        var zoomGroup = new Element
        {
            Dock = DockStyle.Left, Width = 3 + 28 + 44 + 28 + 28 + 3,
            Radius = new Radius(9),
            Border = new Thickness(0), 
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 8, 0),
        };
        Tint(zoomGroup, background: () => ColorScheme.SurfaceContainerLow.WithAlpha(120));

        PlaceInZoomGroup(zoomOut, 3f, 28f);
        PlaceInZoomGroup(_zoomLabel, 31f, 44f);
        PlaceInZoomGroup(zoomIn, 75f, 28f);
        PlaceInZoomGroup(zoomFit, 103f, 28f);

        void PlaceInZoomGroup(ElementBase control, float x, float width)
        {
            control.Dock = DockStyle.None;
            control.Margin = new Thickness(0);
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.Location = new SKPoint(x, 3f);
            control.Size = new SKSize(width, zoomChildHeight);
            zoomGroup.Controls.Add(control);
        }

        // Toolbar is built right-to-left (last-added docks leftmost); grouped into clusters with
        // thin dividers so related actions read as one unit: theme | view toggles | zoom | preview
        // | file ops | history.
        foreach (var c in new ElementBase[]
        {
            themeToggle,
            Divider(),
            randomizeToggle, guidesToggle, snapToggle, gridSizeField, gridToggle,
            Divider(),
            _gridToolsGroup,
            zoomGroup,
            Divider(),
            _previewButton,
            Divider(),
            exportButton, saveButton, openButton, newButton, newDocButton,
            Divider(),
            _redoButton, _undoButton,
        })
        {
            toolbar.Controls.Add(c);
        }

        // Documents live as tabs in the native title bar (like a browser or VS Code) instead of an
        // embedded strip competing with the toolbar for vertical space. Requires WindowThemeType.Tabbed
        // and Window.TabView (both set in the constructor) to cooperate with the native chrome.
        _documents.TabMode = TabViewMode.TitleBar;
        _documents.TabDesignMode = TabViewDesignMode.Rectangle;
        _documents.TransitionEffect = TabViewTransitionEffect.None;
        _documents.TabOverflowMode = TabOverflowMode.Scroll;
        _documents.NewTabButton = true;
        _documents.TabCloseButton = true;
        _documents.NewTabButtonClick += (_, _) =>
        {
            var d = NewDocument();
            _documents.SelectedTab = d;
            SwitchActive(d.Surface);
        };
        _documents.TabCloseButtonClick += (_, index) =>
        {
            // Once any tab has been drag-reordered, TabView tracks display order in a separate
            // _pageOrder list rather than physically reordering Controls (see GetPageAt) — indexing
            // Controls directly here meant a close-button click closed whatever page originally sat
            // at that Controls slot, not the page currently showing at that tab position.
            if (_documents.GetPageAt(index) is ElementBase tab)
                TryCloseDocument(tab);
        };

        // Resizable three-column layout via nested SplitContainers: [ left | center | right ].
        var outerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterDistance = 248f, SplitterWidth = 8f, PanelMinSize = 180f,
        };
        var innerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            SplitterDistance = 720f, SplitterWidth = 8f, PanelMinSize = 300f,
        };

        StylePanel(outerSplit.Panel1, () => ColorScheme.SurfaceContainerLow);
        StylePanel(innerSplit.Panel2);

        // Solution-Explorer-style sidebar: the Toolbox and the project file browser share one
        // embedded TabView (icon-only, top-aligned) instead of the Toolbox getting a static Header
        // and the Explorer inventing its own switcher — one tab-strip implementation, reused.
        _sidebar.TabMode = TabViewMode.Embedded;
        _sidebar.TabDesignMode = TabViewDesignMode.MacOS;
        _sidebar.TabAlignment = TabViewAlignment.Start;
        _sidebar.TabLayoutMode = TabViewLayoutMode.Top;
        _sidebar.TabStripHeight = 38f;
        _sidebar.DrawTabIcons = true;
        _sidebar.EnableTransitions = true;
        _sidebar.TransitionEffect = TabViewTransitionEffect.Fade;
        _sidebar.Border = new Thickness(0);
        _sidebar.Radius = new Radius(0);
        _sidebar.BackColor = SKColors.Transparent;
        _toolboxPage.Controls.Add(_toolbox);
        _explorerPage.Controls.Add(_explorer);
        _sidebar.Controls.Add(_explorerPage);
        _sidebar.Controls.Add(_toolboxPage);
        RefreshSidebarIcons();
        ColorScheme.ThemeChanged += (_, _) => RefreshSidebarIcons();

        outerSplit.Panel1.Controls.Add(_sidebar);

        innerSplit.Panel1.Controls.Add(_documents);

        innerSplit.Panel2.Controls.Add(_inspector);
        innerSplit.Panel2.Controls.Add(Header("sliders", "Properties", sortPropertiesButton));
        innerSplit.Panel2.Controls.Add(_layoutBar);
        innerSplit.Panel2.Controls.Add(Header("layout", "Layout"));
        innerSplit.Panel2.Controls.Add(_layers);
        innerSplit.Panel2.Controls.Add(Header("layers", "Layers"));

        outerSplit.Panel2.Controls.Add(innerSplit);

        // Keep the right inspector a fixed width while the canvas absorbs horizontal growth.
        innerSplit.SizeChanged += (_, _) =>
        {
            var target = innerSplit.Width - 360f - innerSplit.SplitterWidth;
            if (target > 300f)
                innerSplit.SplitterDistance = target;
        };

        ExtendMenu = BuildExtendMenu();
        ExtendBox = true; // ExtendMenu alone only wires the menu — the title-bar button itself opts in separately.

        Controls.Add(outerSplit);
        Controls.Add(toolbar);
        Controls.Add(_statusHost);
        Controls.Add(_dragLayer); // topmost; hidden until a drag begins
        if (Environment.GetEnvironmentVariable("ORIVY_STUDIO_SKIP_START") != "1")
            Controls.Add(_startScreen); // above everything, including the drag layer, until dismissed

        newDocButton.Click += (_, _) => { var d = NewDocument(); _documents.SelectedTab = d; SwitchActive(d.Surface); };
        newButton.Click += (_, _) => _active.ClearAll();
        openButton.Click += (_, _) => OpenFolder();
        saveButton.Click += (_, _) => SaveActiveDocument();
        exportButton.Click += (_, _) => ExportCode();
        _undoButton.Click += (_, _) => _active.Commands.Undo();
        _redoButton.Click += (_, _) => _active.Commands.Redo();
        _previewButton.Click += (_, _) => TogglePreview();
        zoomOut.Click += (_, _) => _active.Zoom /= 1.25f;
        zoomIn.Click += (_, _) => _active.Zoom *= 1.25f;
        zoomFit.Click += (_, _) => _active.FitToView();
        gridToggle.CheckedChanged += (_, _) => _active.ShowGrid = gridToggle.Checked;
        snapToggle.CheckedChanged += (_, _) => { _active.SnapToGrid = snapToggle.Checked; _active.Invalidate(); };
        gridSizeField.ValueChanged += (_, _) => _active.GridStep = (float)gridSizeField.Value;
        guidesToggle.CheckedChanged += (_, _) => _active.SmartGuides = guidesToggle.Checked;
        themeToggle.CheckedChanged += (_, _) => ColorScheme.IsDarkMode = themeToggle.Checked;
        randomizeToggle.CheckedChanged += (_, _) => _active.ShowRandomBackgrounds = randomizeToggle.Checked;
        addGridRowButton.Click += (_, _) =>
        {
            if (_active.Selection.Primary is not Grid grid)
                return;
            var def = new RowDefinition();
            _active.Commands.Execute(new DelegateCommand("Add row", () => grid.RowDefinitions.Add(def), () => grid.RowDefinitions.Remove(def)));
        };
        addGridColumnButton.Click += (_, _) =>
        {
            if (_active.Selection.Primary is not Grid grid)
                return;
            var def = new ColumnDefinition();
            _active.Commands.Execute(new DelegateCommand("Add column", () => grid.ColumnDefinitions.Add(def), () => grid.ColumnDefinitions.Remove(def)));
        };
        gridPresetsButton.Click += (_, _) =>
        {
            if (_active.Selection.Primary is not Grid grid)
                return;

            var menu = new ContextMenuStrip();
            foreach (var (label, apply) in GridPresets)
                menu.AddItem(new MenuItem(label, (_, _) => ApplyGridPreset(grid, apply, label)));
            menu.Closed += (_, _) => menu.Dispose();
            menu.Show(gridPresetsButton, gridPresetsButton.PointToScreen(new SKPoint(0, gridPresetsButton.Height)));
        };
    }

    /// <summary>Swaps in a preset row/column layout as one undo step — snapshots the Grid's current
    /// track sizes first (not the RowDefinition/ColumnDefinition objects themselves, which the preset
    /// clears) so undo rebuilds equivalent tracks rather than needing to keep the old objects alive.</summary>
    private void ApplyGridPreset(Grid grid, Action<Grid> apply, string label)
    {
        var oldRowHeights = grid.RowDefinitions.Select(d => d.Height).ToArray();
        var oldColumnWidths = grid.ColumnDefinitions.Select(d => d.Width).ToArray();

        _active.Commands.Execute(new DelegateCommand(
            $"Apply preset: {label}",
            () => apply(grid),
            () =>
            {
                SetRows(grid, oldRowHeights);
                SetColumns(grid, oldColumnWidths);
            }));
    }

    /// <summary>
    /// Menu-driven access to everything the icon toolbar and canvas context menu already offer —
    /// same actions, same <see cref="Keys"/> shortcuts (rendered automatically via ShortcutKeys,
    /// not hand-typed into the label like the old context menu items were). Hung off the window's
    /// native title-bar "extend" button (<see cref="Window.ExtendMenu"/>) instead of a docked
    /// MenuStrip — matches the same title-bar-first chrome the TitleBar tab strip already uses.
    /// </summary>
    private ContextMenuStrip BuildExtendMenu()
    {
        var menu = new ContextMenuStrip { ShowShortcutKeys = true };

        var file = menu.AddMenuItem("File");
        file.AddMenuItem("New Document", (_, _) => { var d = NewDocument(); _documents.SelectedTab = d; SwitchActive(d.Surface); }, Keys.Control | Keys.N);
        file.AddMenuItem("New (Clear Canvas)", (_, _) => _active.ClearAll());
        file.AddMenuItem("Open Folder…", (_, _) => OpenFolder(), Keys.Control | Keys.O);
        file.AddMenuItem("Save", (_, _) => SaveActiveDocument(), Keys.Control | Keys.S);
        file.AddSeparator();
        file.AddMenuItem("Export Designer Code…", (_, _) => ExportCode(), Keys.Control | Keys.E);
        file.AddMenuItem("Import Designer Code…", (_, _) => ImportCode(), Keys.Control | Keys.I);
        file.AddSeparator();
        file.AddMenuItem("Close Tab", (_, _) => { if (_documents.SelectedTab is ElementBase t) TryCloseDocument(t); }, Keys.Control | Keys.W);

        var edit = menu.AddMenuItem("Edit");
        edit.AddMenuItem("Undo", (_, _) => _active.Commands.Undo(), Keys.Control | Keys.Z);
        edit.AddMenuItem("Redo", (_, _) => _active.Commands.Redo(), Keys.Control | Keys.Y);
        edit.AddSeparator();
        edit.AddMenuItem("Duplicate", (_, _) => _active.DuplicateSelection(), Keys.Control | Keys.D);
        edit.AddMenuItem("Delete", (_, _) => _active.DeleteSelection(), Keys.Delete);
        edit.AddMenuItem("Select All", (_, _) => _active.Selection.SetMany(_active.DesignedControls), Keys.Control | Keys.A);
        edit.AddSeparator();
        edit.AddMenuItem("Bring to Front", (_, _) => { if (_active.Selection.Primary is { } c) _active.BringToFront(c); });
        edit.AddMenuItem("Send to Back", (_, _) => { if (_active.Selection.Primary is { } c) _active.SendToBack(c); });

        var view = menu.AddMenuItem("View");
        view.AddMenuItem("Preview", (_, _) => TogglePreview(), Keys.Control | Keys.P);
        view.AddSeparator();
        view.AddMenuItem("Zoom In", (_, _) => _active.Zoom *= 1.25f, Keys.Control | Keys.OemPlus);
        view.AddMenuItem("Zoom Out", (_, _) => _active.Zoom /= 1.25f, Keys.Control | Keys.OemMinus);
        view.AddMenuItem("Fit to View", (_, _) => _active.FitToView(), Keys.Control | Keys.D0);
        view.AddSeparator();
        view.AddMenuItem("Toggle Dark Mode", (_, _) => ColorScheme.IsDarkMode = !ColorScheme.IsDarkMode);

        return menu;
    }

    private void WireShell()
    {
        _toolbox.PlaceRequested += entry => _active.AddControl(entry);
        _toolbox.DragStarted += (entry, screen) => _dragLayer.Begin(entry, screen);

        _dragLayer.Dropped += (entry, screen) =>
        {
            _active.ClearDropPreview();
            var client = _active.PointToClient(screen);
            if (client.X >= 0 && client.Y >= 0 && client.X <= _active.Width && client.Y <= _active.Height)
                _active.DropAt(entry, client);
        };

        _dragLayer.Dragging += (entry, screen) =>
        {
            var client = _active.PointToClient(screen);
            if (client.X >= 0 && client.Y >= 0 && client.X <= _active.Width && client.Y <= _active.Height)
                _active.PreviewDrop(entry, client);
            else
                _active.ClearDropPreview();
        };

        _documents.SelectedIndexChanged += (_, _) =>
        {
            if (_documents.SelectedTab is DesignDocument dd)
                SwitchActive(dd.Surface);
            if (_documents.SelectedTab is IStudioDocument doc)
                _explorer.SetActiveFile(doc.FilePath);
        };

        _explorer.FileOpenRequested += path => OpenPath(path, asPreview: true);

        _startScreen.NewRequested += () => Controls.Remove(_startScreen);
        _startScreen.OpenFolderRequested += () => { Controls.Remove(_startScreen); OpenFolder(); };
        _startScreen.RecentSelected += path =>
        {
            Controls.Remove(_startScreen);
            _explorer.RootFolder = path;
            RecentProjects.Add(path);
        };

        _inspector.PropertyValueChanged += (_, e) =>
        {
            if (!_suppressInspectorCommit && e.ChangedItem != null && _inspector.SelectedObject != null)
                _active.CommitPropertyEdit(e.ChangedItem, _inspector.SelectedObject, e.OldValue);
            _active.RelayoutRoot();
            _layers.Rebuild();
            RefreshSelectionDependentUi();
            UpdateStatus();
        };

        KeyDown += (_, e) =>
        {
            // Ctrl+S/Z/Y/W would otherwise reach the default blank document sitting behind the Start
            // Screen — undoing/saving/closing a document the user hasn't actually chosen to work on
            // yet — while the screen forcing that choice is still up.
            if (e.Handled || !e.Control || _startScreen.Parent != null)
                return;
            switch (e.KeyCode)
            {
                case Keys.Z: _active.Commands.Undo(); e.Handled = true; break;
                case Keys.Y: _active.Commands.Redo(); e.Handled = true; break;
                case Keys.S: SaveActiveDocument(); e.Handled = true; break;
                case Keys.W:
                    if (_documents.SelectedTab is ElementBase activeTab) TryCloseDocument(activeTab);
                    e.Handled = true;
                    break;
            }
        };
    }

    // ── Documents ──────────────────────────────────────────────────────────

    private DesignDocument NewDocument()
    {
        _documentCounter++;
        var doc = new DesignDocument($"Window{_documentCounter}");
        _documents.Controls.Add(doc);
        WireDirtyPromotion(doc);
        return doc;
    }

    /// <summary>The moment any document (design or text) is actually edited, it can no longer be the
    /// reusable preview tab — it becomes a normal, permanent one, matching Visual Studio.</summary>
    private void WireDirtyPromotion(IStudioDocument doc)
    {
        doc.DirtyChanged += () =>
        {
            if (doc.IsDirty && ReferenceEquals(_previewDocument, doc))
                _previewDocument = null;
        };
    }

    /// <summary>Activates a tab: selects it, and — depending on its concrete kind — switches the
    /// design-specific side panels to its surface and/or syncs the Explorer's highlighted file.</summary>
    private void ActivateTab(ElementBase tab)
    {
        _documents.SelectedTab = tab;
        if (tab is DesignDocument dd)
            SwitchActive(dd.Surface);
        if (tab is IStudioDocument doc)
            _explorer.SetActiveFile(doc.FilePath);
    }

    /// <summary>Closes a tab (native title-bar close button, Ctrl+W, or the File menu), after
    /// confirming with the user first if it has unsaved changes. Closing the last remaining tab resets
    /// it to a blank design instead of leaving the shell with none — the side panels all assume an
    /// active <see cref="DesignSurface"/> exists.</summary>
    private void TryCloseDocument(ElementBase tab)
    {
        if (tab is not IStudioDocument doc)
            return;

        if (doc.IsDirty)
        {
            var result = MessageBox.Show(this,
                $"\"{doc.DocumentName}\" has unsaved changes. Save before closing?",
                "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
                return;
            if (result == DialogResult.Yes && !SaveDocument(doc))
                return; // user backed out of the save-as prompt — don't close after all
        }

        if (ReferenceEquals(_previewDocument, doc))
            _previewDocument = null;

        if (tab is DesignDocument lastDesign && _documents.Controls.Count <= 1)
        {
            lastDesign.Surface.ClearAll();
            lastDesign.FilePath = null;
            lastDesign.DocumentName = "Window1";
            lastDesign.MarkClean();
            return;
        }

        var wasActive = ReferenceEquals(_documents.SelectedTab, tab);
        // The tab strip's display order can differ from Controls' raw child order once any tab has
        // been drag-reordered (see TabView's _pageOrder) — SelectedIndex is itself already the
        // *display* position of this tab (it's the active one), so it's the right index to re-anchor
        // on after removal, unlike a Controls.IndexOf lookup which would drift from the visual order.
        var visualIndex = _documents.SelectedIndex;
        _documents.Controls.Remove(tab);

        if (_documents.Controls.Count == 0)
        {
            // Never end up with zero tabs — create a fresh blank design instead.
            ActivateTab(NewDocument());
        }
        else if (wasActive)
        {
            var nextIndex = Math.Min(visualIndex, _documents.Controls.Count - 1);
            if (_documents.GetPageAt(nextIndex) is ElementBase next)
                ActivateTab(next);
        }

        // Only reached once the closing tab is no longer referenced by _active/_documents — switching
        // to the next tab above (if it was active) already detached this surface's event handlers.
        tab.Dispose();
    }

    private void SwitchActive(DesignSurface surface)
    {
        if (ReferenceEquals(_active, surface))
        {
            RefreshAllPanels();
            return;
        }

        DetachSurface(_active);
        _active = surface;
        AttachSurface(_active);

        _layers.Attach(_active);
        RefreshSelectionDependentUi();
        RefreshAllPanels();
    }

    private void AttachSurface(DesignSurface s)
    {
        s.Selection.Changed += OnSelectionChanged;
        s.StructureChanged += OnStructureChanged;
        s.SelectionBoundsChanged += OnBoundsChanged;
        s.ZoomChanged += OnZoomChanged;
        s.Commands.Changed += OnCommandsChanged;
    }

    private void DetachSurface(DesignSurface s)
    {
        s.Selection.Changed -= OnSelectionChanged;
        s.StructureChanged -= OnStructureChanged;
        s.SelectionBoundsChanged -= OnBoundsChanged;
        s.ZoomChanged -= OnZoomChanged;
        s.Commands.Changed -= OnCommandsChanged;
    }

    private bool _inspectorRefreshPending;

    private void OnSelectionChanged()
    {
        QueueInspectorRefresh();
        RefreshSelectionDependentUi();
        UpdateStatus();
    }

    private void OnStructureChanged()
    {
        QueueInspectorRefresh();
        UpdateStatus();
    }

    /// <summary>
    /// Rebuilds the property grid only after the mouse button is up. Doing it inside
    /// <c>OnMouseDown</c> — or on the posted message that still runs before mouse-up — walks every
    /// property getter while capture and focus are changing. The grid then keeps a drag or a
    /// capture pointed at rows that were just destroyed, and the right-hand panel ignores clicks.
    /// </summary>
    private void QueueInspectorRefresh()
    {
        _inspectorRefreshPending = true;
        BeginInvoke(TryFlushInspectorRefresh);
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        TryFlushInspectorRefresh();
    }

    private void TryFlushInspectorRefresh()
    {
        if (!_inspectorRefreshPending || IsDisposed)
            return;

        // Still inside the click that changed the selection. OnMouseUp flushes once capture is gone.
        if ((GetAsyncKeyState(1) & 0x8000) != 0)
            return;

        _inspectorRefreshPending = false;
        ReleaseInspectorCapture();
        SetInspectorObject(_active.ActiveObject);
    }

    /// <summary>
    /// Drops capture held by the inspector or one of its editors. Rebuilding the grid while that
    /// capture is live leaves later clicks routed to a row that no longer exists.
    /// </summary>
    private void ReleaseInspectorCapture()
    {
        var captured = _mouseCapturedElement;
        if (captured == null || !IsInsideInspector(captured))
            return;

        ReleaseMouseCapture(captured);
    }

    private bool IsInsideInspector(ElementBase element)
    {
        var current = element;
        while (current != null)
        {
            if (ReferenceEquals(current, _inspector))
                return true;
            current = current.Parent as ElementBase;
        }

        return false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>Resyncs every panel that mirrors the primary selection — the Layout helper bar plus
    /// the Grid row/column toolbar buttons, which only make sense (and are only shown) while a Grid
    /// is selected.</summary>
    private void RefreshSelectionDependentUi()
    {
        _layoutBar.Refresh();
        _gridToolsGroup.Visible = _active.Selection.Primary is Grid;
    }
    private void OnZoomChanged() => _zoomLabel.Text = $"{_active.Zoom * 100f:0}%";
    private void OnCommandsChanged() { UpdateHistoryButtons(); _layers.Rebuild(); }

    private void OnBoundsChanged()
    {
        // Fires on every mouse-move of a live drag/resize — a full Refresh() would re-walk the whole
        // property tree (and re-enumerate any expanded collections) dozens of times per second, so
        // just re-stamp the visible cell text instead. The structural Refresh() still runs once the
        // gesture commits, via OnStructureChange-adjacent paths.
        _suppressInspectorCommit = true;
        try { _inspector.RefreshVisibleValues(); }
        finally { _suppressInspectorCommit = false; }
        RefreshSelectionDependentUi();
        UpdateStatus();
    }

    private void RefreshAllPanels()
    {
        SetInspectorObject(_active.ActiveObject);
        UpdateHistoryButtons();
        OnZoomChanged();
        UpdateStatus();
    }

    private void SetInspectorObject(object target)
    {
        _suppressInspectorCommit = true;
        try { _inspector.SelectedObject = target; }
        finally { _suppressInspectorCommit = false; }
    }

    private void UpdateHistoryButtons()
    {
        _undoButton.Enabled = _active.Commands.CanUndo;
        _redoButton.Enabled = _active.Commands.CanRedo;
        _undoButton.ToolTipText = _active.Commands.UndoLabel ?? string.Empty;
        _redoButton.ToolTipText = _active.Commands.RedoLabel ?? string.Empty;
    }

    private void UpdateStatus()
    {
        var count = _active.DesignedControls.Count;
        // The form's own current size otherwise has no persistent display anywhere — the status bar
        // only ever shows the *selected* control's size, so seeing the form's meant selecting the
        // root itself (or opening Properties) first. Leading with it here means it stays visible
        // alongside whatever else the status bar is currently reporting, selection or not.
        var formSize = $"Form {_active.DesignRoot.Width:0}×{_active.DesignRoot.Height:0}";
        if (_active.PreviewMode)
            _statusHost.Text = "PREVIEW — controls are live. Click ⏹ Design to return.";
        else if (_active.Selection.Count > 1)
            _statusHost.Text = $"{formSize}   ·   {_active.Selection.Count} selected · right-click for align/distribute · {count} control(s)";
        else if (_active.Selection.Primary is { } s)
            _statusHost.Text = $"{formSize}   ·   {s.Name} ({s.GetType().Name})   X={s.Location.X:0} Y={s.Location.Y:0}  W={s.Width:0} H={s.Height:0}   · {count} control(s)";
        else
            _statusHost.Text = $"{formSize}   ·   {count} control(s) · drag or double-click toolbox to add · Ctrl+wheel zoom · wheel/middle-drag pan";
    }

    private void TogglePreview()
    {
        _active.PreviewMode = !_active.PreviewMode;
        _previewButton.Icon = _active.PreviewMode ? "stop" : "play";
        _previewButton.SetToolTip(_active.PreviewMode ? "Stop preview" : "Preview");
        UpdateStatus();
    }


    // ── File / export ────────────────────────────────────────────────────────

    private void SaveActiveDocument()
    {
        if (_documents.SelectedTab is IStudioDocument doc)
            SaveDocument(doc);
    }

    /// <summary>Saves a document, prompting for a path first if it's never been saved. Returns false
    /// if the user backed out of that prompt (so a close-with-unsaved-changes flow knows not to
    /// proceed with closing).</summary>
    private bool SaveDocument(IStudioDocument doc)
    {
        if (doc.FilePath == null)
        {
            var isDesign = doc is DesignDocument;
            var dialog = new SaveFileDialog
            {
                Title = "Save",
                FileName = isDesign ? $"{doc.DocumentName}.Designer.cs" : doc.DocumentName,
                Filter = isDesign
                    ? "C# source (*.cs)|*.cs|All files (*.*)|*.*"
                    : "All files (*.*)|*.*",
            };
            var path = dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(path))
                return false;
            doc.FilePath = path;
        }

        doc.Save();
        _statusHost.Text = $"Saved → {doc.FilePath}";

        AdoptRootFolderIfNone(doc.FilePath);
        _explorer.SetActiveFile(doc.FilePath);
        return true;
    }

    private void OpenFolder()
    {
        var dialog = new FolderSelectionDialog { Title = "Open Folder" };
        var folder = dialog.ShowDialog(this);
        if (string.IsNullOrEmpty(folder))
            return;

        _explorer.RootFolder = folder;
        RecentProjects.Add(folder);
    }

    /// <summary>The saved/opened file's folder becomes the browsed root, unless one is already open —
    /// a zero-friction "workspace follows your files" model instead of requiring an explicit "Open
    /// Folder" first. Only registers the folder as recent when it was actually just adopted this way
    /// — a file opened inside an already-browsed folder shouldn't bump that folder's recency for
    /// every single file click.</summary>
    private void AdoptRootFolderIfNone(string filePath)
    {
        if (_explorer.RootFolder != null)
            return;

        _explorer.RootFolder = System.IO.Path.GetDirectoryName(filePath);
        if (_explorer.RootFolder != null)
            RecentProjects.Add(_explorer.RootFolder);
    }

    /// <summary>
    /// Opens <paramref name="path"/> as a document tab — reused by the File ▸ Open dialog, the
    /// recent-projects list, and clicking a file in the Explorer sidebar. There is no proprietary
    /// project format: a <c>.cs</c> file whose content is recognizable Designer code (see
    /// <see cref="CodeImporter"/>) opens in the visual designer exactly like <see cref="ImportCode"/>
    /// would; anything else — a hand-written class, a README, any other text file — opens as plain
    /// text. Whatever the user picked from the browsed folder is exactly what gets read and written;
    /// nothing here invents a hidden save format on top of it.
    /// </summary>
    /// <param name="asPreview">
    /// True for Explorer navigation: reuses the single preview tab (<see cref="_previewDocument"/>)
    /// instead of opening a new permanent one, matching Visual Studio's Solution Explorer — clicking
    /// through files to look at them doesn't leave a trail of tabs behind. False (File ▸ Open, a
    /// recent-projects entry, or any other explicit "open this" action) always opens a real,
    /// permanent tab, exactly like Visual Studio's own File ▸ Open does.
    /// </param>
    private void OpenPath(string path, bool asPreview = false)
    {
        // Already open in some tab (preview or permanent)? Focus that tab instead of opening a
        // second copy of the same file.
        foreach (var control in _documents.Controls)
        {
            if (control is IStudioDocument existing && string.Equals(existing.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                ActivateTab((ElementBase)existing);
                return;
            }
        }

        string content;
        try
        {
            content = System.IO.File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var isDesignFile = path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && LooksLikeDesignerCode(content);
        if (isDesignFile)
            OpenDesignFile(path, content, asPreview);
        else
            OpenTextFile(path, content, asPreview);
    }

    /// <summary>Same recognizability test <see cref="CodeImporter.Import"/> itself requires
    /// (an <c>InitializeComponent</c> method) — cheap enough to run on every <c>.cs</c> file opened
    /// without paying for a full parse first.</summary>
    private static bool LooksLikeDesignerCode(string content) => content.Contains("InitializeComponent");

    private void OpenDesignFile(string path, string content, bool asPreview)
    {
        // The preview tab can only ever hold one kind of document — if it's currently a text
        // file and we need a design tab (or vice versa, in OpenTextFile below), drop it and
        // start a fresh preview rather than trying to convert it in place.
        if (asPreview && _previewDocument is { } stale and not DesignDocument)
        {
            _documents.Controls.Remove((ElementBase)stale);
            stale.Dispose();
            _previewDocument = null;
        }

        var reusingPreview = asPreview && _previewDocument is DesignDocument;
        var target = reusingPreview ? (DesignDocument)_previewDocument! : NewDocument();

        try
        {
            var skipped = CodeImporter.Import(target.Surface, content, path);
            target.FilePath = path;
            target.OriginalSourceText = content;
            target.DocumentName = CodeImporter.TryGetClassName(content) ?? System.IO.Path.GetFileNameWithoutExtension(path);
            target.MarkClean();
            ActivateTab(target);
            AdoptRootFolderIfNone(path);
            _statusHost.Text = skipped.Count == 0
                ? $"Opened {path}"
                : $"Opened; {skipped.Count} unknown type(s) skipped: {string.Join(", ", skipped)}";

            if (asPreview)
                _previewDocument = target;
        }
        catch (Exception ex)
        {
            // Don't leave an empty orphaned tab behind when a freshly-created target failed to load —
            // a reused preview tab is left in place instead, since it may already hold prior content.
            if (!reusingPreview)
            {
                _documents.Controls.Remove(target);
                target.Dispose();
            }
            MessageBox.Show(ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenTextFile(string path, string content, bool asPreview)
    {
        if (asPreview && _previewDocument is { } stale and not TextFileDocument)
        {
            _documents.Controls.Remove((ElementBase)stale);
            stale.Dispose();
            _previewDocument = null;
        }

        TextFileDocument target;
        if (asPreview && _previewDocument is TextFileDocument reusable)
        {
            target = reusable;
            target.Rename(System.IO.Path.GetFileName(path));
        }
        else
        {
            target = new TextFileDocument(System.IO.Path.GetFileName(path));
            _documents.Controls.Add(target);
            WireDirtyPromotion(target);
        }

        target.Content = content;
        target.FilePath = path;
        target.MarkClean();
        ActivateTab(target);
        AdoptRootFolderIfNone(path);
        _statusHost.Text = $"Opened {path}";

        if (asPreview)
            _previewDocument = target;
    }

    private void RefreshSidebarIcons()
    {
        var color = ColorScheme.ForeColor.WithAlpha(215);
        var oldToolboxImage = _toolboxPage.Image;
        var oldExplorerImage = _explorerPage.Image;
        // TabView draws embedded-mode tab icons at 24 (logical) px — rasterize at that size (×2 for a
        // crisp render at any pixel-snapping) instead of an arbitrary smaller size that then has to
        // be stretched up and blurs.
        _toolboxPage.Image = ToolbarIcons.CreateImage("toolbox", 24f * ScaleFactor * 2f, color);
        _explorerPage.Image = ToolbarIcons.CreateImage("explorer", 24f * ScaleFactor * 2f, color);
        oldToolboxImage?.Dispose();
        oldExplorerImage?.Dispose();
    }

    private void ExportCode()
    {
        var code = (_documents.SelectedTab as DesignDocument)?.GetPersistedSource()
            ?? CodeGenerator.Generate(_active, "MyWindow");

        var preview = new Window
        {
            Text = "Generated Designer Code",
            ClientSize = new SKSize(760, 580),
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false, MaximizeBox = false,
        };

        var bar = new Element { Dock = DockStyle.Bottom, Height = 52, Padding = new Thickness(12, 10, 12, 10), BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0) };
        var close = new Button { Text = "Close", Dock = DockStyle.Right, Width = 96, DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save .cs…", Dock = DockStyle.Right, Width = 116, Margin = new Thickness(0, 0, 8, 0) };
        bar.Controls.Add(close);
        bar.Controls.Add(save);

        var box = new RichTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = code, Margin = new Thickness(12) };
        save.Click += (_, _) =>
        {
            var dialog = new SaveFileDialog { Title = "Save designer code", FileName = "MyWindow.Designer.cs", Filter = "C# source (*.cs)|*.cs" };
            var path = dialog.ShowDialog(preview);
            if (!string.IsNullOrWhiteSpace(path))
            {
                System.IO.File.WriteAllText(path!, code);
                preview.Close(DialogResult.OK);
            }
        };

        preview.Controls.Add(box);
        preview.Controls.Add(bar);
        preview.ShowDialog();
    }

    /// <summary>
    /// The reverse of <see cref="ExportCode"/>: reads Designer code (pasted, or loaded from a
    /// <c>.cs</c> file) back through <see cref="CodeImporter"/> into a new document tab — the round
    /// trip the export-only flow was missing.
    /// </summary>
    private void ImportCode()
    {
        var dialog = new Window
        {
            Text = "Import Designer Code",
            ClientSize = new SKSize(760, 580),
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false, MaximizeBox = false,
        };

        var bar = new Element { Dock = DockStyle.Bottom, Height = 52, Padding = new Thickness(12, 10, 12, 10), BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0) };
        var cancel = new Button { Text = "Cancel", Dock = DockStyle.Right, Width = 96, DialogResult = DialogResult.Cancel };
        var import = new Button { Text = "Import", Dock = DockStyle.Right, Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        var load = new Button { Text = "Load .cs…", Dock = DockStyle.Left, Width = 116 };
        bar.Controls.Add(cancel);
        bar.Controls.Add(import);
        bar.Controls.Add(load);

        var box = new RichTextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = false, Margin = new Thickness(12),
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, 10.5f),
        };

        load.Click += (_, _) =>
        {
            var fileDialog = new FileSelectionDialog { Title = "Load Designer code", Filter = "C# source (*.cs)|*.cs|All files (*.*)|*.*" };
            var files = fileDialog.ShowDialog(dialog);
            if (files.Length > 0)
                box.Text = System.IO.File.ReadAllText(files[0]);
        };

        import.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(box.Text))
                return;

            var target = NewDocument();
            try
            {
                var skipped = CodeImporter.Import(target.Surface, box.Text);
                target.OriginalSourceText = box.Text;
                target.DocumentName = CodeImporter.TryGetClassName(box.Text) ?? target.DocumentName;
                target.MarkClean();
                ActivateTab(target);
                _statusHost.Text = skipped.Count == 0
                    ? "Imported designer code."
                    : $"Imported; {skipped.Count} unknown type(s) skipped: {string.Join(", ", skipped)}";
                dialog.Close(DialogResult.OK);
            }
            catch (Exception ex)
            {
                // Don't leave an empty orphaned tab behind when the pasted code failed to import.
                _documents.Controls.Remove(target);
                target.Dispose();
                MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        dialog.Controls.Add(box);
        dialog.Controls.Add(bar);
        dialog.ShowDialog();
    }

    private void SeedDemoLayout()
    {
        var catalog = ControlCatalog.Discover();
        ControlEntry Entry(string name) => catalog.First(e => e.DisplayName == name);

        var label = _active.AddControl(Entry("Element"), new SKPoint(28, 28));
        label.Text = "Sign in";
        var user = _active.AddControl(Entry("TextBox"), new SKPoint(28, 74));
        _active.AddControl(Entry("TextBox"), new SKPoint(28, 124));
        var button = _active.AddControl(Entry("Button"), new SKPoint(28, 178));
        button.Text = "Continue";
        _active.Selection.SelectOnly(button);
    }

    // ── UI factories ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a theme-reactive tint via <see cref="ElementBase.ConfigureVisualStyles"/> instead of
    /// a raw <c>BackColor =</c>/<c>ForeColor =</c> snapshot. A direct assignment freezes whatever
    /// <see cref="ColorScheme"/> returned at construction time — it never updates on a dark/light
    /// toggle. The color callbacks passed here are re-invoked live every time the theme changes.
    /// </summary>
    private static void Tint(ElementBase element, Func<SKColor>? background = null, Func<SKColor>? foreground = null, Func<SKColor>? border = null)
    {
        element.ConfigureVisualStyles(styles => styles.Base(b =>
        {
            if (background != null) b.Background(background());
            if (foreground != null) b.Foreground(foreground());
            if (border != null) b.BorderColor(border());
        }));
    }

    private static Element Panel(DockStyle dock, int width = 0, int height = 0)
    {
        var panel = new Element
        {
            Dock = dock, Border = new Thickness(0), Radius = new Radius(0), Padding = new Thickness(10),
        };
        if (width > 0) panel.Width = width;
        if (height > 0) panel.Height = height;
        return panel;
    }

    /// <summary>Styles a split-container side column. A slightly lower tone than the toolbar/status
    /// chrome keeps the three-tier depth (chrome → panel → canvas) readable at a glance.</summary>
    private static void StylePanel(Element panel, Func<SKColor>? tone = null)
    {
        Tint(panel, background: tone ?? (() => ColorScheme.SurfaceContainer));
        panel.Padding = new Thickness(10, 10, 10, 10);
    }

    /// <summary>A thin vertical rule used to separate logical clusters of toolbar controls.</summary>
    private static Element Divider()
    {
        var divider = new Element
        {
            Dock = DockStyle.Left, Width = 1, Margin = new Thickness(6, 6, 6, 6),
            Border = new Thickness(0), Radius = new Radius(0),
        };
        Tint(divider, background: () => ColorScheme.Outline.WithAlpha(65));
        return divider;
    }

    /// <summary>Section header for a side panel: a small glyph + title, with an optional action
    /// slot and an optional muted subtitle underneath, separated from its content by a hairline.</summary>
    private static Element Header(string icon, string title, ElementBase? action = null, string? subtitle = null)
    {
        var host = new Element
        {
            Dock = DockStyle.Top, Height = subtitle == null ? 30 : 44,
            Border = new Thickness(0, 0, 0, 1),
            Radius = new Radius(0), Padding = new Thickness(0, 0, 0, 6), Margin = new Thickness(0, 0, 0, 10),
        };
        Tint(host, border: () => ColorScheme.Outline.WithAlpha(45));

        if (subtitle != null)
        {
            var subtitleLabel = new Element
            {
                Text = subtitle, Dock = DockStyle.Top, Height = 16, Padding = new Thickness(22, 0, 0, 0),
                Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.TopLeft,
            };
            Tint(subtitleLabel, foreground: () => ColorScheme.ForeColor.WithAlpha(120));
            host.Controls.Add(subtitleLabel);
        }

        var titleRow = new Element
        {
            Dock = DockStyle.Top, Height = subtitle == null ? 28 : 22,
            Border = new Thickness(0), Radius = new Radius(0),
        };
        var titleLabel = new Element
        {
            Text = title, Dock = DockStyle.Fill,
            Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        };
        Tint(titleLabel, foreground: () => ColorScheme.ForeColor.WithAlpha(230));
        titleRow.Controls.Add(titleLabel);
        if (action != null)
            titleRow.Controls.Add(action);
        titleRow.Controls.Add(new IconGlyph(icon));
        host.Controls.Add(titleRow);

        return host;
    }

}
