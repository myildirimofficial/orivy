using Orivy;
using Orivy.Controls;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Example;

internal sealed partial class GridListDemoPage : Container
{
    public GridListDemoPage()
    {
        InitializeComponent();
        InitializeGridListDemo();
        InitializeListBoxDemo();
        InitializePropertyGridDemo();
        InitializeListManagementDemo();
    }

    // Model behind the PropertyGrid list-management demo.
    private sealed class ServerProfile
    {
        [System.ComponentModel.Category("Profile")]
        [System.ComponentModel.Description("Display name of this profile.")]
        public string Name { get; set; } = "Production";

        [System.ComponentModel.Category("Profile")]
        [System.ComponentModel.Description("Server list — managed via the buttons or by right-clicking rows in the grid.")]
        public System.Collections.Generic.List<string> Servers { get; } = new() { "alpha", "beta", "gamma" };
    }

    private ServerProfile _listMgmtModel = null!;

    private void InitializeListManagementDemo()
    {
        _listMgmtModel = new ServerProfile();
        listMgmtGrid.SelectedObject = _listMgmtModel;
        listMgmtGrid.ExpandAllGridItems();

        void UpdateStatus()
        {
            var suffix = listMgmtGrid.TryGetSelectedCollectionElement(out _, out var selIndex)
                ? $"  Selected element [{selIndex}]: \"{_listMgmtModel.Servers[selIndex]}\"."
                : "  Select a [i] row to remove or move it.";
            listMgmtStatus.Text = $"Servers: {_listMgmtModel.Servers.Count} items.{suffix}";
        }

        void RefreshGrid()
        {
            listMgmtGrid.Refresh();
            UpdateStatus();
        }

        void Add()
        {
            var text = (listMgmtInput.Text ?? string.Empty).Trim();
            if (text.Length == 0)
                return;

            _listMgmtModel.Servers.Add(text);
            listMgmtInput.Text = string.Empty;
            listMgmtInput.Focus();
            RefreshGrid();
        }

        void Remove()
        {
            if (listMgmtGrid.TryGetSelectedCollectionElement(out var list, out var i) && list is { IsFixedSize: false })
            {
                list.RemoveAt(i);
                RefreshGrid();
            }
        }

        void Move(int delta)
        {
            if (!listMgmtGrid.TryGetSelectedCollectionElement(out var list, out var i) || list == null)
                return;

            var j = i + delta;
            if (j < 0 || j >= list.Count)
                return;

            (list[j], list[i]) = (list[i], list[j]);
            RefreshGrid();
        }

        listMgmtAdd.Click += (_, _) => Add();
        listMgmtRemove.Click += (_, _) => Remove();
        listMgmtUp.Click += (_, _) => Move(-1);
        listMgmtDown.Click += (_, _) => Move(1);
        listMgmtClear.Click += (_, _) => { _listMgmtModel.Servers.Clear(); RefreshGrid(); };
        listMgmtInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; Add(); } };
        listMgmtGrid.SelectionChanged += (_, _) => UpdateStatus();

        UpdateStatus();
    }

    private sealed record StatusItem(string Name, string Detail, SKColor Color);

    // Sample object edited live by the PropertyGrid demo.
    private sealed class AppSettings
    {
        [System.ComponentModel.Category("Appearance")]
        [System.ComponentModel.Description("The window title shown in the caption bar.")]
        public string Title { get; set; } = "Orivy Demo";

        [System.ComponentModel.Category("Appearance")]
        [System.ComponentModel.Description("Use the dark color scheme.")]
        public bool DarkMode { get; set; } = true;

        [System.ComponentModel.Category("Appearance")]
        [System.ComponentModel.Description("Corner rounding applied to panels, in pixels.")]
        public int CornerRadius { get; set; } = 12;

        [System.ComponentModel.Category("Appearance")]
        [System.ComponentModel.Description("Accent color (opens a color picker).")]
        public SkiaSharp.SKColor Accent { get; set; } = new(0, 150, 243);

        [System.ComponentModel.Category("Appearance")]
        [System.ComponentModel.Description("Release date (opens a date picker).")]
        public System.DateTime ReleaseDate { get; set; } = new(2026, 3, 14);

        [System.ComponentModel.Category("Behavior")]
        [System.ComponentModel.Description("Automatically save changes as they are made.")]
        public bool AutoSave { get; set; }

        [System.ComponentModel.Category("Behavior")]
        [System.ComponentModel.Description("Update channel to receive builds from.")]
        public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

        [System.ComponentModel.Category("Behavior")]
        [System.ComponentModel.Description("Polling interval in seconds.")]
        public double RefreshInterval { get; set; } = 2.5;

        [System.ComponentModel.Category("Data")]
        [System.ComponentModel.Description("Database connection string.")]
        public string ConnectionString { get; set; } = "server=localhost;db=orivy";

        [System.ComponentModel.Category("Data")]
        [System.ComponentModel.Description("Primary endpoint (expandable nested object).")]
        public Endpoint Primary { get; set; } = new();

        [System.ComponentModel.Category("Data")]
        [System.ComponentModel.Description("Known server names (expandable collection).")]
        public System.Collections.Generic.List<string> Servers { get; set; } = new() { "alpha", "beta", "gamma" };

        [System.ComponentModel.Category("Data")]
        [System.ComponentModel.ReadOnly(true)]
        [System.ComponentModel.Description("The current build version (read-only).")]
        public string Version { get; set; } = "3.0.1";
    }

    private sealed class Endpoint
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 8080;
        public bool Secure { get; set; } = true;
        public override string ToString() => $"{Host}:{Port}";
    }

    private enum UpdateChannel { Stable, Beta, Nightly }

    private void InitializePropertyGridDemo()
    {
        propertyGridDemo.SelectedObject = new AppSettings();

        propertyGridDemo.SelectedPropertyChanged += (_, _) =>
        {
            var pd = propertyGridDemo.SelectedProperty;
            propertyGridDescription.Text = pd == null
                ? "Select a property to see its description."
                : $"{pd.DisplayName}\n{(string.IsNullOrEmpty(pd.Description) ? "No description." : pd.Description)}";
        };

        propertyGridDemo.PropertyValueChanged += (_, e) =>
            propertyGridDescription.Text = $"Changed '{e.ChangedItem?.Name}' (was: {e.OldValue}).";
    }

    private void InitializeListBoxDemo()
    {
        // 1) Plain single-select list.
        listBoxSingle.Items.AddRange(
            "Renderer", "Layout Engine", "Input Hub", "Theme Engine", "Telemetry",
            "Scroll Lab", "Frame Trace", "Crash Watch", "Session Guard", "Audit Trail", "Vault Mirror");
        listBoxSingle.SelectedIndex = 0;
        listBoxSingle.SelectedIndexChanged += (_, _) =>
            UpdateListBoxStatus($"Selected \"{listBoxSingle.SelectedItem}\" (index {listBoxSingle.SelectedIndex}).");

        // 2) Owner-drawn rows: a colored status dot + title + detail line.
        listBoxOwnerDraw.Items.AddRange(
            new StatusItem("Core Systems", "All services healthy", new SKColor(34, 197, 94)),
            new StatusItem("Telemetry", "Queue backpressured", new SKColor(245, 158, 11)),
            new StatusItem("Security", "Session guard locked", new SKColor(239, 68, 68)),
            new StatusItem("Release Channel", "Preview ring live", new SKColor(59, 130, 246)),
            new StatusItem("Diagnostics", "Frame trace warming", new SKColor(168, 85, 247)),
            new StatusItem("Storage", "Vault mirror synced", new SKColor(20, 184, 166)));
        listBoxOwnerDraw.DrawItem += ListBoxOwnerDraw_DrawItem;
        listBoxOwnerDraw.SelectedIndexChanged += (_, _) =>
        {
            if (listBoxOwnerDraw.SelectedItem is StatusItem s)
                UpdateListBoxStatus($"Owner-draw row \"{s.Name}\": {s.Detail}.");
        };

        // 3) Checked list with pre-checked options.
        checkedListBoxDemo.Items.AddRange(
            "Enable telemetry", "Automatic updates", "Beta channel", "Hardware acceleration",
            "Verbose logging", "Send crash reports", "Usage analytics", "Experimental features");
        checkedListBoxDemo.SetItemChecked(0, true);
        checkedListBoxDemo.SetItemChecked(3, true);
        checkedListBoxDemo.ItemCheck += (_, e) =>
            UpdateListBoxStatus($"\"{checkedListBoxDemo.Items[e.Index]}\" -> {e.NewValue}. Checked: {checkedListBoxDemo.CheckedIndices.Count}.");
    }

    private void ListBoxOwnerDraw_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (listBoxOwnerDraw.Items[e.Index] is not StatusItem item)
            return;

        // Selection / hover background (rounded) supplied by the control via e.BackColor.
        e.DrawBackground(8f);

        var b = e.Bounds;
        var scale = listBoxOwnerDraw.ScaleFactor;

        using (var dot = new SKPaint { Color = item.Color, IsAntialias = true })
            e.Canvas.DrawCircle(b.Left + 18f * scale, b.MidY, 6f * scale, dot);

        var textLeft = b.Left + 34f * scale;
        using (var title = new SKPaint { Color = e.ForeColor, IsAntialias = true })
        {
            var titleRect = new SKRect(textLeft, b.Top + 5f * scale, b.Right - 10f * scale, b.MidY + 2f * scale);
            TextRenderer.DrawText(e.Canvas, item.Name, titleRect, title, e.Font, ContentAlignment.BottomLeft, autoEllipsis: true);
        }

        using (var detail = new SKPaint { Color = e.ForeColor.WithAlpha(e.Selected ? (byte)210 : (byte)150), IsAntialias = true })
        {
            var detailRect = new SKRect(textLeft, b.MidY, b.Right - 10f * scale, b.Bottom - 5f * scale);
            TextRenderer.DrawText(e.Canvas, item.Detail, detailRect, detail, e.Font, ContentAlignment.TopLeft, autoEllipsis: true);
        }
    }

    private void UpdateListBoxStatus(string text) => listBoxStatus.Text = text;


    private readonly List<SKImage> _gridListImages = new();

    private void InitializeGridListDemo()
    {
        var healthyIcon = CreateExampleIcon(new SKColor(34, 197, 94), ExampleIconKind.Healthy);
        var warningIcon = CreateExampleIcon(new SKColor(245, 158, 11), ExampleIconKind.Warning);
        var lockedIcon = CreateExampleIcon(new SKColor(239, 68, 68), ExampleIconKind.Locked);
        var pulseIcon = CreateExampleIcon(new SKColor(59, 130, 246), ExampleIconKind.Pulse);

        ConfigurePrimaryGridList(healthyIcon, warningIcon, lockedIcon, pulseIcon);
        ConfigureCompactGridList(healthyIcon, pulseIcon, warningIcon);

        // Item / cell / column color showcase (WinForms ListViewItem.BackColor/ForeColor style):
        // row 1 gets a full-row tint, row 2 highlights a single cell, and the "note" column text
        // is dimmed column-wide (cell/item colors still win over the column color).
        if (gridListCompact.Items.Count > 2)
        {
            gridListCompact.Items[0].ToolTipText = "Row tooltip: this feed watches repository commits.";
            gridListCompact.Items[1].BackColor = new SKColor(59, 130, 246, 26);
            gridListCompact.Items[1].ForeColor = new SKColor(59, 130, 246);

            gridListCompact.Items[2].Cells[1].BackColor = new SKColor(245, 158, 11, 40);
            gridListCompact.Items[2].Cells[1].ForeColor = new SKColor(245, 158, 11);
        }

        if (gridListCompact.Columns.Count > 2)
            gridListCompact.Columns[2].ForeColor = ColorScheme.ForeColor.WithAlpha(150);

        gridListPrimary.SelectedIndex = 0;
        gridListCompact.SelectedIndex = 0;
        UpdateGridListButtons();
        UpdateGridListStatus("Ready", "Primary grid now has enough rows to test sticky header, animated group collapse and optional row resizing in-place.");
    }

    private void ConfigurePrimaryGridList(SKImage healthyIcon, SKImage warningIcon, SKImage lockedIcon, SKImage pulseIcon)
    {
        gridListPrimary.Columns.Clear();
        gridListPrimary.Items.Clear();

        gridListPrimary.Columns.Add(new GridListColumn { Name = "workload", Text = "Workload", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "live", Text = "Live", Width = 92f, MinWidth = 72f, MaxWidth = 108f, ShowCheckBox = true, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "owner", Text = "Owner", Width = 138f, MinWidth = 110f, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "latency", Text = "Latency", Width = 110f, MinWidth = 88f, CellTextAlign = ContentAlignment.MiddleRight, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "summary", Text = "Summary", Width = 320f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.65f });

        AddPrimaryRow("core", "Core Systems", healthyIcon, "Renderer", true, "Graphics", "14 ms", "DirectX11 path is stable; cache hit ratio above target.");
        AddPrimaryRow("core", "Core Systems", pulseIcon, "Layout", true, "UI", "18 ms", "Measure/arrange pass includes nested cards and sticky regions.");
        AddPrimaryRow("core", "Core Systems", healthyIcon, "Input Hub", true, "Platform", "16 ms", "Pointer capture and wheel routing stay deterministic through overlays.");
        AddPrimaryRow("core", "Core Systems", pulseIcon, "Theme Engine", true, "Design", "19 ms", "Palette interpolation is synchronized with visual-state transitions.");
        AddPrimaryRow("diag", "Diagnostics", warningIcon, "Telemetry", false, "Platform", "41 ms", "Event batcher is backpressured; investigate queue saturation.");
        AddPrimaryRow("diag", "Diagnostics", pulseIcon, "Scroll Lab", true, "QA", "22 ms", "Wheel routing and thumb drag stay stable under nested hosts.");
        AddPrimaryRow("diag", "Diagnostics", warningIcon, "Frame Trace", true, "Rendering", "27 ms", "GPU timings are sampled, but capture export is still warming the pipeline.");
        AddPrimaryRow("diag", "Diagnostics", healthyIcon, "Crash Watch", true, "Ops", "13 ms", "Guard rails are live and no fatal exceptions were observed in the last pass.");
        AddPrimaryRow("secure", "Security", lockedIcon, "Session Guard", true, "Identity", "11 ms", "Lock escalation rules loaded and group policy sync is complete.");
        AddPrimaryRow("secure", "Security", warningIcon, "Audit Trail", false, "Compliance", "35 ms", "Retention sweep delayed because archive lane is warming up.");
        AddPrimaryRow("secure", "Security", lockedIcon, "Vault Mirror", true, "Storage", "17 ms", "Encrypted snapshots are mirrored and signature verification passed.");
        AddPrimaryRow("secure", "Security", pulseIcon, "Access Review", true, "Risk", "24 ms", "Review queue is active and staged approvals refresh every minute.");
        AddPrimaryRow("ship", "Release Channel", pulseIcon, "Preview Ring", true, "Release", "21 ms", "Preview users received the latest package and rollback marker is set.");
        AddPrimaryRow("ship", "Release Channel", warningIcon, "Canary Ring", false, "Release", "38 ms", "Canary deployment paused because health probes dipped below threshold.");
        AddPrimaryRow("ship", "Release Channel", healthyIcon, "Stable Ring", true, "Release", "12 ms", "Stable channel remains green with no pending incidents.");

        gridListPrimary.SortByColumn(0, GridListSortDirection.Ascending);
        gridListPrimary.SelectionChanged += GridListPrimary_SelectionChanged;
        gridListPrimary.CellCheckChanged += GridListPrimary_CellCheckChanged;
        gridListPrimary.ColumnClick += GridListPrimary_ColumnClick;
        gridListPrimary.CellClick += GridListPrimary_CellClick;
    }

    private void ConfigureCompactGridList(SKImage healthyIcon, SKImage pulseIcon, SKImage warningIcon)
    {
        gridListCompact.Columns.Clear();
        gridListCompact.Items.Clear();

        gridListCompact.Columns.Add(new GridListColumn { Name = "stream", Text = "Stream", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
        gridListCompact.Columns.Add(new GridListColumn { Name = "state", Text = "State", Width = 100f, MinWidth = 80f, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        gridListCompact.Columns.Add(new GridListColumn { Name = "note", Text = "Note", Width = 420f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.4f });

        AddCompactRow(healthyIcon, "Commit Watcher", "Live", "High-frequency feed without a header bar.");
        AddCompactRow(pulseIcon, "Animation Bus", "Sync", "Transition snapshots update while list selection remains stable.");
        AddCompactRow(warningIcon, "Alert Stream", "Warn", "Compact list mode still paints icons and supports selection.");
    }

    private void AddPrimaryRow(string groupKey, string groupText, SKImage icon, string workload,
        bool isLive, string owner, string latency, string summary)
    {
        var item = new GridListItem { GroupKey = groupKey, GroupText = groupText, Icon = icon };
        item.Cells.Add(new GridListCell { Text = workload, Icon = icon });
        item.Cells.Add(new GridListCell { CheckState = isLive ? CheckState.Checked : CheckState.Unchecked, Text = isLive ? "On" : "Off" });
        item.Cells.Add(new GridListCell { Text = owner });
        item.Cells.Add(new GridListCell { Text = latency });
        item.Cells.Add(new GridListCell { Text = summary });
        gridListPrimary.Items.Add(item);
    }

    private void AddCompactRow(SKImage icon, string stream, string state, string note)
    {
        var item = new GridListItem { Icon = icon };
        item.Cells.Add(new GridListCell { Text = stream, Icon = icon });
        item.Cells.Add(new GridListCell { Text = state });
        item.Cells.Add(new GridListCell { Text = note });
        gridListCompact.Items.Add(item);
    }

    private SKImage CreateExampleIcon(SKColor accent, ExampleIconKind kind)
    {
        var image = ExampleHelper.CreateIcon(accent, kind);
        _gridListImages.Add(image);
        return image;
    }

    private void GridListPrimary_SelectionChanged(object? sender, GridListSelectionChangedEventArgs e)
    {
        var selected = gridListPrimary.SelectedItem;
        var workload = selected?.Cells.Count > 0 ? selected.Cells[0].Text : "None";
        UpdateGridListStatus("Selection", $"Active row: {workload}. Selected index: {e.SelectedIndex}. Multi-select count: {gridListPrimary.SelectedIndices.Length}.");
    }

    private void GridListPrimary_CellCheckChanged(object? sender, GridListCellCheckChangedEventArgs e)
    {
        UpdateGridListStatus("Checkbox", $"{e.Item.Cells[0].Text} changed from {e.PreviousState} to {e.CurrentState}.");
    }

    private void GridListPrimary_ColumnClick(object? sender, GridListColumnClickEventArgs e)
    {
        UpdateGridListStatus("Sort", $"Column '{e.Column.Text}' clicked. Direction: {e.SortDirection}.");
    }

    private void GridListPrimary_CellClick(object? sender, GridListCellEventArgs e)
    {
        UpdateGridListStatus("Cell Click", $"Row '{e.Item.Cells[0].Text}', column '{e.Column.Text}' was activated.");
    }

    private void GridListToggleHeaderButton_Click(object? sender, EventArgs e)
    {
        gridListPrimary.HeaderVisible = !gridListPrimary.HeaderVisible;
        if (!gridListPrimary.HeaderVisible)
            gridListPrimary.StickyHeader = false;
        UpdateGridListButtons();
        UpdateGridListStatus("Display", gridListPrimary.HeaderVisible ? "Primary grid header is visible again." : "Primary grid is now in headerless mode.");
    }

    private void GridListToggleStickyButton_Click(object? sender, EventArgs e)
    {
        if (!gridListPrimary.HeaderVisible)
            gridListPrimary.HeaderVisible = true;
        gridListPrimary.StickyHeader = !gridListPrimary.StickyHeader;
        UpdateGridListButtons();
        UpdateGridListStatus("Display", gridListPrimary.StickyHeader ? "Sticky header enabled for the primary grid." : "Sticky header disabled; header scrolls with content.");
    }

    private void GridListToggleGroupingButton_Click(object? sender, EventArgs e)
    {
        gridListPrimary.GroupingEnabled = !gridListPrimary.GroupingEnabled;
        UpdateGridListButtons();
        UpdateGridListStatus("Grouping", gridListPrimary.GroupingEnabled ? "Group headers are enabled. Click a group row to collapse it." : "Grouping disabled; rows now render as a flat sorted list.");
    }

    private void GridListToggleGridLinesButton_Click(object? sender, EventArgs e)
    {
        var next = !gridListPrimary.ShowGridLines;
        gridListPrimary.ShowGridLines = next;
        gridListCompact.ShowGridLines = next;
        UpdateGridListButtons();
        UpdateGridListStatus("Grid Lines", next ? "Row and column separators are visible." : "Grid lines hidden for a cleaner card-like presentation.");
    }

    private void GridListToggleRowResizeButton_Click(object? sender, EventArgs e)
    {
        var next = !gridListPrimary.AllowRowResize;
        gridListPrimary.AllowRowResize = next;
        gridListCompact.AllowRowResize = next;
        UpdateGridListButtons();
        UpdateGridListStatus("Row Density", next ? "Row resize enabled. Drag the lower edge of a visible row to change row height." : "Row resize disabled and the grid returns to a fixed rhythm.");
    }

    private void UpdateGridListButtons()
    {
        gridListToggleHeaderButton.Text = gridListPrimary.HeaderVisible ? "Header: On" : "Header: Off";
        gridListToggleStickyButton.Text = gridListPrimary.StickyHeader ? "Sticky: On" : "Sticky: Off";
        gridListToggleGroupingButton.Text = gridListPrimary.GroupingEnabled ? "Grouping: On" : "Grouping: Off";
        gridListToggleGridLinesButton.Text = gridListPrimary.ShowGridLines ? "Grid Lines: On" : "Grid Lines: Off";
        gridListToggleRowResizeButton.Text = gridListPrimary.AllowRowResize ? "Row Resize: On" : "Row Resize: Off";
    }

    private void UpdateGridListStatus(string title, string body)
    {
        gridListStatus.Text = $"{title}\n{body}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (var i = 0; i < _gridListImages.Count; i++)
                _gridListImages[i].Dispose();
            _gridListImages.Clear();
        }

        base.Dispose(disposing);
    }
}