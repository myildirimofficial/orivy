using Orivy;
using Orivy.Controls;
using Orivy.Studio.Panels;
using Orivy.Studio.Persistence;
using Orivy.Windowing.Desktop.Windows;
using SkiaSharp;
using System;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// Orivy.Studio shell — Figma-style visual designer for Orivy.
/// Layout: toolbar (undo/redo · file · export/preview · zoom · toggles · theme) on top, dynamic
/// reflection-driven Toolbox on the left, infinite zoom/pan canvas in the middle, Layers +
/// PropertyGrid inspector on the right, live status bar at the bottom.
/// </summary>
public sealed class StudioWindow : Window
{
    private readonly DesignSurface _surface = new() { Dock = DockStyle.Fill };
    private readonly ToolboxPanel _toolbox;
    private readonly LayersPanel _layers;
    private readonly PropertyGrid _inspector = new() { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized };

    private readonly Button _undoButton = ToolButton("↶ Undo", 92);
    private readonly Button _redoButton = ToolButton("↷ Redo", 92);
    private readonly Button _previewButton = ToolButton("▶ Preview", 104);
    private readonly Element _zoomLabel;
    private string? _projectPath;
    private bool _suppressInspectorCommit;

    public StudioWindow()
    {
        Text = "Orivy Studio";
        ClientSize = new SKSize(1380, 860);
        MinimumSize = new SKSize(1024, 640);
        StartPosition = FormStartPosition.CenterScreen;

        _toolbox = new ToolboxPanel { Dock = DockStyle.Fill };
        _layers = new LayersPanel(_surface) { Dock = DockStyle.Top, Height = 240, Margin = new Thickness(0, 0, 0, 10) };

        _zoomLabel = new Element
        {
            Text = "100%",
            Dock = DockStyle.Left,
            Width = 58,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleCenter,
        };

        BuildLayout();
        WireEvents();
        UpdateHistoryButtons();
        UpdateStatus();

        if (Environment.GetEnvironmentVariable("ORIVY_STUDIO_SEED") == "1")
            SeedDemoLayout();
    }

    /// <summary>Populates a small demo layout — used by automated UI verification.</summary>
    private void SeedDemoLayout()
    {
        var catalog = Toolbox.ControlCatalog.Discover();
        Toolbox.ControlEntry Entry(string name) => catalog.First(e => e.DisplayName == name);

        var label = _surface.AddControl(Entry("Element"), new SKPoint(28, 28));
        label.Text = "Sign in";
        var user = _surface.AddControl(Entry("TextBox"), new SKPoint(28, 74));
        var pass = _surface.AddControl(Entry("TextBox"), new SKPoint(28, 124));
        var button = _surface.AddControl(Entry("Button"), new SKPoint(28, 178));
        button.Text = "Continue";
        _surface.Selection.SetMany(new[] { user, pass });
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        // Toolbar
        var toolbar = Panel(DockStyle.Top, height: 54);
        toolbar.Padding = new Thickness(10, 8, 10, 8);

        var newButton = ToolButton("New", 66);
        var openButton = ToolButton("Open…", 82);
        var saveButton = ToolButton("Save", 70);
        var exportButton = ToolButton("Export Code", 118);
        var zoomOut = ToolButton("−", 40);
        var zoomIn = ToolButton("+", 40);
        var zoomFit = ToolButton("Fit", 56);
        var snapToggle = Toggle("Snap", true, 84);
        var guidesToggle = Toggle("Guides", true, 100);
        var themeToggle = Toggle("Dark", ColorScheme.IsDarkMode, 84);

        // Dock=Left stacks in reverse-add order; add right-to-left visual order accordingly.
        foreach (var c in new ElementBase[]
        {
            themeToggle, guidesToggle, snapToggle,
            zoomFit, zoomIn, _zoomLabel, zoomOut,
            _previewButton, exportButton,
            saveButton, openButton, newButton,
            _redoButton, _undoButton,
        })
        {
            toolbar.Controls.Add(c);
        }

        // Left: toolbox
        var left = Panel(DockStyle.Left, width: 230);
        left.Controls.Add(_toolbox);
        left.Controls.Add(Header("Toolbox — Orivy.Controls (auto-discovered)"));

        // Right: layers + inspector
        var right = Panel(DockStyle.Right, width: 350);
        right.Controls.Add(_inspector);
        right.Controls.Add(Header("Properties"));
        right.Controls.Add(_layers);
        right.Controls.Add(Header("Layers"));

        _statusInit(out var status);
        Controls.Add(_surface);
        Controls.Add(left);
        Controls.Add(right);
        Controls.Add(toolbar);
        Controls.Add(status);

        // Toolbar actions
        newButton.Click += (_, _) => { _surface.ClearAll(); _projectPath = null; };
        openButton.Click += (_, _) => OpenProject();
        saveButton.Click += (_, _) => SaveProject();
        exportButton.Click += (_, _) => ExportCode();
        _undoButton.Click += (_, _) => _surface.Commands.Undo();
        _redoButton.Click += (_, _) => _surface.Commands.Redo();
        _previewButton.Click += (_, _) => TogglePreview();
        zoomOut.Click += (_, _) => _surface.Zoom /= 1.25f;
        zoomIn.Click += (_, _) => _surface.Zoom *= 1.25f;
        zoomFit.Click += (_, _) => _surface.FitToView();
        snapToggle.CheckedChanged += (_, _) => { _surface.SnapToGrid = snapToggle.Checked; _surface.Invalidate(); };
        guidesToggle.CheckedChanged += (_, _) => _surface.SmartGuides = guidesToggle.Checked;
        themeToggle.CheckedChanged += (_, _) => ColorScheme.SetThemeInstant(themeToggle.Checked);
    }

    private Element _statusHost = null!;

    private void _statusInit(out Element status)
    {
        _statusHost = new Element
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            BackColor = ColorScheme.SurfaceContainer,
            ForeColor = ColorScheme.ForeColor.WithAlpha(190),
            Border = new Thickness(0),
            Radius = new Radius(0),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        status = _statusHost;
    }

    private Element _statusRef => _statusHost;

    // ── Wiring ───────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        _toolbox.PlaceRequested += entry => _surface.AddControl(entry);

        _surface.Selection.Changed += () =>
        {
            SetInspectorObject(_surface.ActiveObject);
            UpdateStatus();
        };
        _surface.StructureChanged += () =>
        {
            SetInspectorObject(_surface.ActiveObject);
            UpdateStatus();
        };
        _surface.SelectionBoundsChanged += () =>
        {
            _suppressInspectorCommit = true;
            try { _inspector.Refresh(); }
            finally { _suppressInspectorCommit = false; }
            UpdateStatus();
        };
        _surface.ZoomChanged += () => _zoomLabel.Text = $"{_surface.Zoom * 100f:0}%";
        _surface.Commands.Changed += () =>
        {
            UpdateHistoryButtons();
            _layers.Rebuild();
        };

        // Inspector edits become undoable document commands.
        _inspector.PropertyValueChanged += (_, e) =>
        {
            if (!_suppressInspectorCommit && e.ChangedItem != null)
                _surface.CommitPropertyEdit(e.ChangedItem, _inspector.SelectedObject!, e.OldValue);
            _surface.Invalidate();
            _layers.Rebuild();
            UpdateStatus();
        };

        // Global shortcuts (work regardless of focused panel).
        KeyDown += (_, e) =>
        {
            if (e.Handled || !e.Control)
                return;

            switch (e.KeyCode)
            {
                case Keys.Z: _surface.Commands.Undo(); e.Handled = true; break;
                case Keys.Y: _surface.Commands.Redo(); e.Handled = true; break;
                case Keys.S: SaveProject(); e.Handled = true; break;
            }
        };

        SetInspectorObject(_surface.ActiveObject);
    }

    private void SetInspectorObject(object target)
    {
        _suppressInspectorCommit = true;
        try { _inspector.SelectedObject = target; }
        finally { _suppressInspectorCommit = false; }
    }

    private void UpdateHistoryButtons()
    {
        _undoButton.Enabled = _surface.Commands.CanUndo;
        _redoButton.Enabled = _surface.Commands.CanRedo;
        _undoButton.ToolTipText = _surface.Commands.UndoLabel ?? string.Empty;
        _redoButton.ToolTipText = _surface.Commands.RedoLabel ?? string.Empty;
    }

    private void UpdateStatus()
    {
        var count = _surface.DesignedControls.Count;
        if (_surface.PreviewMode)
            _statusRef.Text = "PREVIEW — controls are live. Click ⏹ Design to return.";
        else if (_surface.Selection.Count > 1)
            _statusRef.Text = $"{_surface.Selection.Count} selected · right-click for align/distribute · {count} control(s)";
        else if (_surface.Selection.Primary is { } s)
            _statusRef.Text = $"{s.Name} ({s.GetType().Name})   X={s.Location.X:0} Y={s.Location.Y:0}  W={s.Width:0} H={s.Height:0}   · {count} control(s)";
        else
            _statusRef.Text = $"Design root — {count} control(s) · double-click toolbox to add · Ctrl+wheel zoom · wheel pan · middle-drag pan";
    }

    private void TogglePreview()
    {
        _surface.PreviewMode = !_surface.PreviewMode;
        _previewButton.Text = _surface.PreviewMode ? "⏹ Design" : "▶ Preview";
        UpdateStatus();
    }

    // ── File / export ────────────────────────────────────────────────────────

    private void SaveProject()
    {
        if (_projectPath == null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Orivy Studio project",
                FileName = "design.orivy.json",
                Filter = "Orivy Studio project (*.orivy.json)|*.orivy.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            };
            var path = dialog.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(path))
                return;
            _projectPath = path;
        }

        System.IO.File.WriteAllText(_projectPath, DesignSerializer.Save(_surface));
        _statusRef.Text = $"Saved → {_projectPath}";
    }

    private void OpenProject()
    {
        var dialog = new FileSelectionDialog
        {
            Title = "Open Orivy Studio project",
            Filter = "Orivy Studio project (*.orivy.json;*.json)|*.orivy.json;*.json|All files (*.*)|*.*",
        };

        var selectedFiles = dialog.ShowDialog(this);

        if (selectedFiles.Length == 0)
            return;

        try
        {
            var skipped = DesignSerializer.Load(_surface, System.IO.File.ReadAllText(dialog.FileName));
            _projectPath = dialog.FileName;
            _surface.NotifyStructureChanged();
            _statusRef.Text = skipped.Count == 0
                ? $"Opened {_projectPath}"
                : $"Opened with {skipped.Count} unknown type(s) skipped: {string.Join(", ", skipped)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportCode()
    {
        var code = CodeGenerator.Generate(_surface);
        var preview = new Window
        {
            Text = "Generated Designer Code",
            ClientSize = new SKSize(760, 580),
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
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

    // ── UI factories ─────────────────────────────────────────────────────────

    private static Element Panel(DockStyle dock, int width = 0, int height = 0)
    {
        var panel = new Element
        {
            Dock = dock,
            BackColor = ColorScheme.SurfaceContainer,
            Border = new Thickness(0),
            Radius = new Radius(0),
            Padding = new Thickness(10),
        };
        if (width > 0) panel.Width = width;
        if (height > 0) panel.Height = height;
        return panel;
    }

    private static Element Header(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        Height = 28,
        BackColor = SKColors.Transparent,
        ForeColor = ColorScheme.ForeColor.WithAlpha(170),
        Border = new Thickness(0),
        Radius = new Radius(0),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Thickness(2, 0, 0, 6),
    };

    private static Button ToolButton(string text, int width) => new()
    {
        Text = text,
        Dock = DockStyle.Left,
        Width = width,
        Margin = new Thickness(0, 0, 8, 0),
    };

    private static CheckBox Toggle(string text, bool value, int width) => new()
    {
        Text = text,
        Checked = value,
        Dock = DockStyle.Left,
        Width = width,
        Margin = new Thickness(4, 0, 4, 0),
    };
}
