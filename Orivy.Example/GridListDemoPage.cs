using Orivy.Controls;
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
    }


    private readonly List<SKImage> _gridListImages = new();

    private void InitializeGridListDemo()
    {
        var healthyIcon = CreateExampleIcon(new SKColor(34, 197, 94), ExampleIconKind.Healthy);
        var warningIcon = CreateExampleIcon(new SKColor(245, 158, 11), ExampleIconKind.Warning);
        var lockedIcon = CreateExampleIcon(new SKColor(239, 68, 68), ExampleIconKind.Locked);
        var pulseIcon = CreateExampleIcon(new SKColor(59, 130, 246), ExampleIconKind.Pulse);

        ConfigurePrimaryGridList(healthyIcon, warningIcon, lockedIcon, pulseIcon);
        ConfigureCompactGridList(healthyIcon, pulseIcon, warningIcon);

        gridListPrimary.SelectedIndex = 0;
        gridListCompact.SelectedIndex = 0;
        UpdateGridListButtons();
        UpdateGridListStatus("Ready", "Primary grid now has enough rows to test sticky header, animated group collapse and optional row resizing in-place.");
    }

    private void ConfigurePrimaryGridList(SKImage healthyIcon, SKImage warningIcon, SKImage lockedIcon, SKImage pulseIcon)
    {
        gridListPrimary.Columns.Clear();
        gridListPrimary.Items.Clear();

        gridListPrimary.Columns.Add(new GridListColumn { Name = "workload", HeaderText = "Workload", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "live", HeaderText = "Live", Width = 92f, MinWidth = 72f, MaxWidth = 108f, ShowCheckBox = true, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "owner", HeaderText = "Owner", Width = 138f, MinWidth = 110f, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "latency", HeaderText = "Latency", Width = 110f, MinWidth = 88f, CellTextAlign = ContentAlignment.MiddleRight, SizeMode = GridListColumnSizeMode.Auto });
        gridListPrimary.Columns.Add(new GridListColumn { Name = "summary", HeaderText = "Summary", Width = 320f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.65f });

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

        gridListCompact.Columns.Add(new GridListColumn { Name = "stream", HeaderText = "Stream", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
        gridListCompact.Columns.Add(new GridListColumn { Name = "state", HeaderText = "State", Width = 100f, MinWidth = 80f, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
        gridListCompact.Columns.Add(new GridListColumn { Name = "note", HeaderText = "Note", Width = 420f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.4f });

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
        UpdateGridListStatus("Selection", $"Active row: {workload}. Selected index: {e.SelectedIndex}. Multi-select count: {gridListPrimary.SelectedIndices.Count}.");
    }

    private void GridListPrimary_CellCheckChanged(object? sender, GridListCellCheckChangedEventArgs e)
    {
        UpdateGridListStatus("Checkbox", $"{e.Item.Cells[0].Text} changed from {e.PreviousState} to {e.CurrentState}.");
    }

    private void GridListPrimary_ColumnClick(object? sender, GridListColumnClickEventArgs e)
    {
        UpdateGridListStatus("Sort", $"Column '{e.Column.HeaderText}' clicked. Direction: {e.SortDirection}.");
    }

    private void GridListPrimary_CellClick(object? sender, GridListCellEventArgs e)
    {
        UpdateGridListStatus("Cell Click", $"Row '{e.Item.Cells[0].Text}', column '{e.Column.HeaderText}' was activated.");
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