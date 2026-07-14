using Orivy;
using Orivy.Controls;
using Orivy.Studio.Canvas;
using Orivy.Studio.Documents;
using Orivy.Studio.Panels;
using Orivy.Studio.Persistence;
using Orivy.Studio.Toolbox;
using Orivy.Windowing.Desktop.Windows;
using SkiaSharp;
using System;
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
    private readonly ToolboxPanel _toolbox = new() { Dock = DockStyle.Fill };
    private readonly LayersPanel _layers;
    private readonly PropertyGrid _inspector = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized };
    private readonly LayoutHelperBar _layoutBar;
    private readonly DragLayer _dragLayer = new();

    private readonly Button _undoButton = ToolButton("↶ Undo", 92);
    private readonly Button _redoButton = ToolButton("↷ Redo", 92);
    private readonly Button _previewButton = ToolButton("▶ Preview", 104);
    private readonly Element _zoomLabel;
    private readonly Element _statusHost;

    private DesignSurface _active = null!;
    private int _documentCounter;
    private bool _suppressInspectorCommit;

    public StudioWindow()
    {
        Text = "Orivy Studio";
        ClientSize = new SKSize(1440, 900);
        MinimumSize = new SKSize(1024, 640);
        StartPosition = FormStartPosition.CenterScreen;

        _zoomLabel = new Element
        {
            Text = "100%", Dock = DockStyle.Left, Width = 58,
            BackColor = SKColors.Transparent, Border = new Thickness(0), Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _statusHost = new Element
        {
            Dock = DockStyle.Bottom, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            BackColor = ColorScheme.SurfaceContainer, ForeColor = ColorScheme.ForeColor.WithAlpha(190),
            Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        };

        // First document must exist before panels bind to it.
        var firstDoc = NewDocument();
        _active = firstDoc.Surface;
        _layers = new LayersPanel(_active) { Dock = DockStyle.Top, Height = 230, Margin = new Thickness(0, 0, 0, 10) };
        _layoutBar = new LayoutHelperBar(() => _active) { Dock = DockStyle.Top, Height = 92, Margin = new Thickness(0, 0, 0, 10) };

        BuildLayout();
        AttachSurface(_active);
        WireShell();

        _documents.SelectedTab = firstDoc;
        SwitchActive(firstDoc.Surface);

        if (Environment.GetEnvironmentVariable("ORIVY_STUDIO_SEED") == "1")
            SeedDemoLayout();
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var toolbar = Panel(DockStyle.Top, height: 54);
        toolbar.Padding = new Thickness(10, 8, 10, 8);

        var newDocButton = ToolButton("＋ Doc", 78);
        var newButton = ToolButton("New", 62);
        var openButton = ToolButton("Open…", 78);
        var saveButton = ToolButton("Save", 66);
        var exportButton = ToolButton("Export", 82);
        var zoomOut = ToolButton("−", 40);
        var zoomIn = ToolButton("+", 40);
        var zoomFit = ToolButton("Fit", 52);
        var gridToggle = Toggle("Grid", true, 80);
        var snapToggle = Toggle("Snap", true, 84);
        var guidesToggle = Toggle("Guides", true, 96);
        var themeToggle = Toggle("Dark", ColorScheme.IsDarkMode, 82);

        foreach (var c in new ElementBase[]
        {
            themeToggle, guidesToggle, snapToggle, gridToggle,
            zoomFit, zoomIn, _zoomLabel, zoomOut,
            _previewButton, exportButton,
            saveButton, openButton, newButton, newDocButton,
            _redoButton, _undoButton,
        })
        {
            toolbar.Controls.Add(c);
        }

        // Embedded tab strip so document tabs actually render inside the control (the default
        // TitleBar mode draws them in the window chrome, i.e. invisible here).
        _documents.TabMode = TabViewMode.Embedded;

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

        StylePanel(outerSplit.Panel1);
        StylePanel(innerSplit.Panel2);

        outerSplit.Panel1.Controls.Add(_toolbox);
        outerSplit.Panel1.Controls.Add(Header("Toolbox — Orivy.Controls (auto)"));

        innerSplit.Panel1.Controls.Add(_documents);

        innerSplit.Panel2.Controls.Add(_inspector);
        innerSplit.Panel2.Controls.Add(Header("Properties"));
        innerSplit.Panel2.Controls.Add(_layoutBar);
        innerSplit.Panel2.Controls.Add(Header("Layout"));
        innerSplit.Panel2.Controls.Add(_layers);
        innerSplit.Panel2.Controls.Add(Header("Layers"));

        outerSplit.Panel2.Controls.Add(innerSplit);

        // Keep the right inspector a fixed width while the canvas absorbs horizontal growth.
        innerSplit.SizeChanged += (_, _) =>
        {
            var target = innerSplit.Width - 360f - innerSplit.SplitterWidth;
            if (target > 300f)
                innerSplit.SplitterDistance = target;
        };

        Controls.Add(outerSplit);
        Controls.Add(toolbar);
        Controls.Add(_statusHost);
        Controls.Add(_dragLayer); // topmost; hidden until a drag begins

        newDocButton.Click += (_, _) => { var d = NewDocument(); _documents.SelectedTab = d; SwitchActive(d.Surface); };
        newButton.Click += (_, _) => _active.ClearAll();
        openButton.Click += (_, _) => OpenProject();
        saveButton.Click += (_, _) => SaveProject();
        exportButton.Click += (_, _) => ExportCode();
        _undoButton.Click += (_, _) => _active.Commands.Undo();
        _redoButton.Click += (_, _) => _active.Commands.Redo();
        _previewButton.Click += (_, _) => TogglePreview();
        zoomOut.Click += (_, _) => _active.Zoom /= 1.25f;
        zoomIn.Click += (_, _) => _active.Zoom *= 1.25f;
        zoomFit.Click += (_, _) => _active.FitToView();
        gridToggle.CheckedChanged += (_, _) => _active.ShowGrid = gridToggle.Checked;
        snapToggle.CheckedChanged += (_, _) => { _active.SnapToGrid = snapToggle.Checked; _active.Invalidate(); };
        guidesToggle.CheckedChanged += (_, _) => _active.SmartGuides = guidesToggle.Checked;
        themeToggle.CheckedChanged += (_, _) => ColorScheme.SetThemeInstant(themeToggle.Checked);
    }

    private void WireShell()
    {
        _toolbox.PlaceRequested += entry => _active.AddControl(entry);
        _toolbox.DragStarted += (entry, screen) => _dragLayer.Begin(entry, screen);

        _dragLayer.Dropped += (entry, screen) =>
        {
            var client = _active.PointToClient(screen);
            if (client.X >= 0 && client.Y >= 0 && client.X <= _active.Width && client.Y <= _active.Height)
                _active.DropAt(entry, client);
        };

        _documents.SelectedIndexChanged += (_, _) =>
        {
            if (_documents.SelectedTab is DesignDocument doc)
                SwitchActive(doc.Surface);
        };

        _inspector.PropertyValueChanged += (_, e) =>
        {
            if (!_suppressInspectorCommit && e.ChangedItem != null && _inspector.SelectedObject != null)
                _active.CommitPropertyEdit(e.ChangedItem, _inspector.SelectedObject, e.OldValue);
            _active.RelayoutRoot();
            _layers.Rebuild();
            _layoutBar.Refresh();
            UpdateStatus();
        };

        KeyDown += (_, e) =>
        {
            if (e.Handled || !e.Control)
                return;
            switch (e.KeyCode)
            {
                case Keys.Z: _active.Commands.Undo(); e.Handled = true; break;
                case Keys.Y: _active.Commands.Redo(); e.Handled = true; break;
                case Keys.S: SaveProject(); e.Handled = true; break;
            }
        };
    }

    // ── Documents ──────────────────────────────────────────────────────────

    private DesignDocument NewDocument()
    {
        _documentCounter++;
        var doc = new DesignDocument($"Window{_documentCounter}");
        _documents.Controls.Add(doc);
        return doc;
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
        _layoutBar.Refresh();
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

    private void OnSelectionChanged() { SetInspectorObject(_active.ActiveObject); _layoutBar.Refresh(); UpdateStatus(); }
    private void OnStructureChanged() { SetInspectorObject(_active.ActiveObject); UpdateStatus(); }
    private void OnZoomChanged() => _zoomLabel.Text = $"{_active.Zoom * 100f:0}%";
    private void OnCommandsChanged() { UpdateHistoryButtons(); _layers.Rebuild(); }

    private void OnBoundsChanged()
    {
        _suppressInspectorCommit = true;
        try { _inspector.Refresh(); }
        finally { _suppressInspectorCommit = false; }
        _layoutBar.Refresh();
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
        if (_active.PreviewMode)
            _statusHost.Text = "PREVIEW — controls are live. Click ⏹ Design to return.";
        else if (_active.Selection.Count > 1)
            _statusHost.Text = $"{_active.Selection.Count} selected · right-click for align/distribute · {count} control(s)";
        else if (_active.Selection.Primary is { } s)
            _statusHost.Text = $"{s.Name} ({s.GetType().Name})   X={s.Location.X:0} Y={s.Location.Y:0}  W={s.Width:0} H={s.Height:0}   · {count} control(s)";
        else
            _statusHost.Text = $"{count} control(s) · drag or double-click toolbox to add · Ctrl+wheel zoom · wheel/middle-drag pan";
    }

    private void TogglePreview()
    {
        _active.PreviewMode = !_active.PreviewMode;
        _previewButton.Text = _active.PreviewMode ? "⏹ Design" : "▶ Preview";
        UpdateStatus();
    }

    // ── File / export ────────────────────────────────────────────────────────

    private void SaveProject()
    {
        var doc = _documents.SelectedTab as DesignDocument;
        if (doc == null)
            return;

        if (doc.FilePath == null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Orivy Studio project",
                FileName = $"{doc.Text}.orivy.json",
                Filter = "Orivy Studio project (*.orivy.json)|*.orivy.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            };
            var path = dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(path))
                return;
            doc.FilePath = path;
        }

        System.IO.File.WriteAllText(doc.FilePath, DesignSerializer.Save(_active));
        _statusHost.Text = $"Saved → {doc.FilePath}";
    }

    private void OpenProject()
    {
        var dialog = new FileSelectionDialog
        {
            Title = "Open Orivy Studio project",
            Filter = "Orivy Studio project (*.orivy.json;*.json)|*.orivy.json;*.json|All files (*.*)|*.*",
        };
        var files = dialog.ShowDialog(this);
        if (files.Length == 0)
            return;

        try
        {
            var target = NewDocument();
            var skipped = DesignSerializer.Load(target.Surface, System.IO.File.ReadAllText(files[0]));
            target.FilePath = files[0];
            target.Text = System.IO.Path.GetFileNameWithoutExtension(files[0]);
            _documents.SelectedTab = target;
            SwitchActive(target.Surface);
            target.Surface.NotifyStructureChanged();
            _statusHost.Text = skipped.Count == 0
                ? $"Opened {target.FilePath}"
                : $"Opened; {skipped.Count} unknown type(s) skipped: {string.Join(", ", skipped)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportCode()
    {
        var code = CodeGenerator.Generate(_active,
            (_documents.SelectedTab as DesignDocument)?.Text ?? "MyWindow");

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

        var box = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, Text = code, Margin = new Thickness(12) };
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

    private static Element Panel(DockStyle dock, int width = 0, int height = 0)
    {
        var panel = new Element
        {
            Dock = dock, BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(0), Radius = new Radius(0), Padding = new Thickness(10),
        };
        if (width > 0) panel.Width = width;
        if (height > 0) panel.Height = height;
        return panel;
    }

    private static void StylePanel(Element panel)
    {
        panel.BackColor = ColorScheme.SurfaceContainer;
        panel.Padding = new Thickness(10);
    }

    private static Element Header(string text) => new()
    {
        Text = text, Dock = DockStyle.Top, Height = 26,
        BackColor = SKColors.Transparent, ForeColor = ColorScheme.ForeColor.WithAlpha(170),
        Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Thickness(2, 0, 0, 4),
    };

    private static Button ToolButton(string text, int width) => new()
    {
        Text = text, Dock = DockStyle.Left, Width = width, Margin = new Thickness(0, 0, 8, 0),
    };

    private static CheckBox Toggle(string text, bool value, int width) => new()
    {
        Text = text, Checked = value, Dock = DockStyle.Left, Width = width, Margin = new Thickness(4, 0, 4, 0),
    };
}
