using Orivy;
using Orivy.Controls;
using Orivy.Helpers;
using Orivy.Studio.Canvas;
using Orivy.Studio.History;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio;

/// <summary>
/// The design canvas engine. Hosts the designed root plus a transparent overlay in a zoomable,
/// pannable space (zoom rides on <see cref="ElementBase.ChildRenderScale"/>, which scales both
/// rendering and input routing symmetrically). Designed controls render live (true WYSIWYG) but
/// receive no input in design mode; the overlay implements multi-selection, marquee, group move,
/// grip resize, grid + smart-guide snapping, context menu and keyboard editing. Every document
/// mutation is routed through the <see cref="CommandStack"/> so it is undoable.
/// </summary>
public sealed class DesignSurface : Element
{
    private const float GripSize = 7f;
    private const float GridStep = 8f;
    private const float GuideSnapDistance = 6f;
    private const float MinControlSize = 12f;
    private const float MinZoom = 0.25f;
    private const float MaxZoom = 4f;

    private readonly DesignRootCanvas _root;
    private readonly DesignOverlay _overlay;
    private float _zoom = 1f;
    private int _nameCounter;

    public CommandStack Commands { get; } = new();
    public SelectionService Selection { get; } = new();

    /// <summary>Controls excluded from hit-testing and dragging (still rendered).</summary>
    public HashSet<ElementBase> Locked { get; } = new();

    public event Action? StructureChanged;
    public event Action? SelectionBoundsChanged;
    public event Action? ZoomChanged;

    public DesignSurface()
    {
        Padding = new Thickness(0);
        Border = new Thickness(0);
        Radius = new Radius(0);
        // Tint reactively (not via a snapshot BackColor=) so it follows live dark/light toggles.
        ConfigureVisualStyles(styles => styles.Base(b => b.Background(ColorScheme.SurfaceContainerLow)));

        _root = new DesignRootCanvas(this)
        {
            Name = "designRoot",
            Text = string.Empty,
            Location = new SKPoint(48, 40),
            Size = new SKSize(640, 440),
            Border = new Thickness(1),
            Radius = new Radius(8),
            Padding = new Thickness(0),
        };
        _root.ConfigureVisualStyles(styles => styles.Base(b => b
            .Background(ColorScheme.Surface)
            .BorderColor(ColorScheme.Outline.WithAlpha(140))));

        _overlay = new DesignOverlay(this)
        {
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
            ZOrder = 10_000,
            CanSelect = true,
            TabStop = true,
        };

        Controls.Add(_root);
        Controls.Add(_overlay);

        Selection.Changed += () => { Invalidate(); };
    }

    // ── Viewport ─────────────────────────────────────────────────────────────

    protected override float ChildRenderScale => _zoom;

    public float Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (Math.Abs(clamped - _zoom) < 0.001f)
                return;

            _zoom = clamped;
            ZoomChanged?.Invoke();
            Invalidate();
        }
    }

    /// <summary>Zooms keeping the given logical (pre-zoom child-space) point stationary on screen.</summary>
    public void ZoomAt(float newZoom, SKPoint logicalAnchor)
    {
        var oldZoom = _zoom;
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.001f)
            return;

        // Keep anchor's screen position: screen = logical * zoom → shift the whole logical space
        // (root + everything with it) so the anchor stays put.
        var screenX = logicalAnchor.X * oldZoom;
        var screenY = logicalAnchor.Y * oldZoom;
        var newLogicalX = screenX / newZoom;
        var newLogicalY = screenY / newZoom;
        var dx = newLogicalX - logicalAnchor.X;
        var dy = newLogicalY - logicalAnchor.Y;

        _root.Location = new SKPoint(_root.Location.X + dx, _root.Location.Y + dy);
        Zoom = newZoom;
    }

    public void Pan(float dx, float dy)
    {
        _root.Location = new SKPoint(_root.Location.X + dx, _root.Location.Y + dy);
        Invalidate();
    }

    public void FitToView()
    {
        if (Width <= 0 || Height <= 0)
            return;

        var margin = 48f;
        var zx = (Width - margin * 2f) / Math.Max(1f, _root.Width);
        var zy = (Height - margin * 2f) / Math.Max(1f, _root.Height);
        _zoom = Math.Clamp(MathF.Min(zx, zy), MinZoom, MaxZoom);

        var logicalW = Width / _zoom;
        var logicalH = Height / _zoom;
        _root.Location = new SKPoint((logicalW - _root.Width) / 2f, (logicalH - _root.Height) / 2f);
        ZoomChanged?.Invoke();
        Invalidate();
    }

    // ── Document state ───────────────────────────────────────────────────────

    public Element DesignRoot => _root;
    public bool SnapToGrid { get; set; } = true;
    public bool SmartGuides { get; set; } = true;

    private bool _showGrid = true;

    /// <summary>Draws the design grid inside the root frame.</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; Invalidate(); }
    }

    private bool _previewMode;

    /// <summary>Preview mode hides the overlay: designed controls become fully interactive.</summary>
    public bool PreviewMode
    {
        get => _previewMode;
        set
        {
            if (_previewMode == value)
                return;
            _previewMode = value;
            _overlay.Visible = !value;
            if (value)
                Selection.Clear();
            Invalidate();
        }
    }

    public ElementBase ActiveObject => Selection.Primary ?? _root;

    public IReadOnlyList<ElementBase> DesignedControls
    {
        get
        {
            var list = new List<ElementBase>();
            for (var i = 0; i < _root.Controls.Count; i++)
                if (_root.Controls[i] is ElementBase child && IsDesignedControl(child))
                    list.Add(child);
            return list;
        }
    }

    private static bool IsDesignedControl(ElementBase child) => child is not ScrollBar;

    // ── Editing operations (all undoable) ────────────────────────────────────

    public ElementBase AddControl(ControlEntry entry, SKPoint? location = null)
    {
        var control = entry.CreateInstance();
        PrepareForDesign(control);
        control.Name = MakeUniqueName(char.ToLowerInvariant(entry.DisplayName[0]) + entry.DisplayName[1..]);
        if (string.IsNullOrEmpty(control.Text))
            control.Text = control.Name;

        var cascade = 24f + (DesignedControls.Count % 8) * 16f;
        var at = location ?? new SKPoint(cascade, cascade);
        control.Location = new SKPoint(Snap(at.X), Snap(at.Y));

        Commands.Execute(new DelegateCommand(
            $"Add {entry.DisplayName}",
            () => { _root.Controls.Add(control); AfterStructureChange(); Selection.SelectOnly(control); },
            () => { Selection.Remove(control); _root.Controls.Remove(control); Locked.Remove(control); AfterStructureChange(); }));

        return control;
    }

    /// <summary>Drops a toolbox entry at a point given in this surface's client (screen-derived) space.</summary>
    public ElementBase DropAt(ControlEntry entry, SKPoint clientPoint)
    {
        var size = entry.CreateInstance().Size; // default size to center the drop under the cursor
        var target = DropTargetLocation(clientPoint, size);
        ClearDropPreview();
        return AddControl(entry, target);
    }

    private ControlEntry? _previewEntry;
    private SKRect _previewRect;

    /// <summary>
    /// Shows a live ghost of where <paramref name="entry"/> would land if dropped at
    /// <paramref name="clientPoint"/> right now — called continuously while a toolbox drag hovers
    /// over the canvas, so the user sees the snapped placement before releasing.
    /// </summary>
    public void PreviewDrop(ControlEntry entry, SKPoint clientPoint)
    {
        var size = ControlCatalog.DefaultSizeFor(entry);
        var location = DropTargetLocation(clientPoint, size);
        _previewEntry = entry;
        _previewRect = SKRect.Create(location, size);
        Invalidate();
    }

    /// <summary>Hides the drop ghost (drag left the canvas, was dropped, or was cancelled).</summary>
    public void ClearDropPreview()
    {
        if (_previewEntry == null)
            return;
        _previewEntry = null;
        Invalidate();
    }

    /// <summary>Snapped, root-relative top-left for a <paramref name="size"/>d control centered under a client-space point.</summary>
    private SKPoint DropTargetLocation(SKPoint clientPoint, SKSize size)
    {
        // Convert client → logical child space (undo pan + zoom), then to root-relative.
        var logical = new SKPoint(clientPoint.X / _zoom, clientPoint.Y / _zoom);
        var rootRel = new SKPoint(logical.X - _root.Location.X, logical.Y - _root.Location.Y);
        return new SKPoint(Snap(rootRel.X - size.Width / 2f), Snap(rootRel.Y - size.Height / 2f));
    }

    /// <summary>
    /// Frees a control from content-driven auto-sizing so the designer fully owns its bounds. Many
    /// Orivy controls (Button, CheckBox…) enable AutoSize=GrowOnly in their constructor, which is
    /// exactly why their Location/Size would otherwise refuse to change on the canvas.
    /// </summary>
    public static void PrepareForDesign(ElementBase control)
    {
        control.AutoSize = false;
        control.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        if (control.Dock == DockStyle.None)
            control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
    }

    public void DeleteSelection()
    {
        var doomed = Selection.Items.ToList();
        if (doomed.Count == 0)
            return;

        Commands.Execute(new DelegateCommand(
            doomed.Count == 1 ? $"Delete {doomed[0].Name}" : $"Delete {doomed.Count} controls",
            () =>
            {
                foreach (var control in doomed)
                {
                    Selection.Remove(control);
                    _root.Controls.Remove(control);
                }
                AfterStructureChange();
            },
            () =>
            {
                foreach (var control in doomed)
                    _root.Controls.Add(control);
                Selection.SetMany(doomed);
                AfterStructureChange();
            }));
    }

    public void DuplicateSelection()
    {
        var sources = Selection.Items.ToList();
        if (sources.Count == 0)
            return;

        var clones = new List<ElementBase>();
        foreach (var source in sources)
        {
            if (Activator.CreateInstance(source.GetType()) is not ElementBase clone)
                continue;

            ControlCatalog.ApplyDesignDefaults(clone);
            PrepareForDesign(clone);
            clone.Name = MakeUniqueName(source.GetType().Name.ToLowerInvariant());
            clone.Text = source.Text;
            clone.Size = source.Size;
            clone.BackColor = source.BackColor;
            clone.Dock = source.Dock;
            clone.Anchor = source.Anchor;
            clone.Location = new SKPoint(source.Location.X + 16f, source.Location.Y + 16f);
            clones.Add(clone);
        }

        if (clones.Count == 0)
            return;

        Commands.Execute(new DelegateCommand(
            $"Duplicate {clones.Count} control(s)",
            () => { foreach (var c in clones) _root.Controls.Add(c); Selection.SetMany(clones); AfterStructureChange(); },
            () => { foreach (var c in clones) { Selection.Remove(c); _root.Controls.Remove(c); } AfterStructureChange(); }));
    }

    public void ClearAll()
    {
        var all = DesignedControls.ToList();
        if (all.Count == 0)
            return;

        Commands.Execute(new DelegateCommand(
            "New document",
            () => { Selection.Clear(); foreach (var c in all) _root.Controls.Remove(c); Locked.Clear(); AfterStructureChange(); },
            () => { foreach (var c in all) _root.Controls.Add(c); AfterStructureChange(); }));
    }

    /// <summary>Records an already-applied bounds change (drag/resize) so it can be undone.</summary>
    internal void CommitBoundsChange(string label, Dictionary<ElementBase, SKRect> before)
    {
        var after = before.Keys.ToDictionary(c => c, c => SKRect.Create(c.Location, c.Size));
        if (before.All(kv => kv.Value == after[kv.Key]))
            return;

        Commands.Push(new DelegateCommand(
            label,
            () => { ApplyBounds(after); },
            () => { ApplyBounds(before); }));
        SelectionBoundsChanged?.Invoke();
    }

    private void ApplyBounds(Dictionary<ElementBase, SKRect> bounds)
    {
        foreach (var (control, rect) in bounds)
        {
            control.Location = new SKPoint(rect.Left, rect.Top);
            control.Size = new SKSize(rect.Width, rect.Height);
        }

        SelectionBoundsChanged?.Invoke();
        Invalidate();
    }

    /// <summary>Records an already-applied property edit (from the inspector) as undoable.</summary>
    public void CommitPropertyEdit(System.ComponentModel.PropertyDescriptor descriptor, object component, object? oldValue)
    {
        object? newValue;
        try { newValue = descriptor.GetValue(component); }
        catch { return; }

        RelayoutRoot();
        Commands.Push(new DelegateCommand(
            $"Edit {descriptor.Name}",
            () => { TrySetValue(descriptor, component, newValue); RelayoutRoot(); AfterStructureChange(); },
            () => { TrySetValue(descriptor, component, oldValue); RelayoutRoot(); AfterStructureChange(); }));
    }

    /// <summary>
    /// Property setters are reached through reflection from the inspector; a bad value or a
    /// control-specific validation failure must not crash the designer or corrupt the undo stack.
    /// </summary>
    private static void TrySetValue(System.ComponentModel.PropertyDescriptor descriptor, object component, object? value)
    {
        try { descriptor.SetValue(component, value); }
        catch { /* keep the previous value in place */ }
    }

    /// <summary>Forces the design root to re-run layout so Dock/Anchor/Size edits take effect immediately.</summary>
    public void RelayoutRoot()
    {
        _root.PerformLayout();
        Invalidate();
    }

    public void BringToFront(ElementBase control) => ShiftZ(control, +1_000, "Bring to front");
    public void SendToBack(ElementBase control) => ShiftZ(control, -1_000, "Send to back");

    private void ShiftZ(ElementBase control, int delta, string label)
    {
        var old = control.ZOrder;
        var max = DesignedControls.Count == 0 ? 0 : DesignedControls.Max(c => c.ZOrder);
        var min = DesignedControls.Count == 0 ? 0 : DesignedControls.Min(c => c.ZOrder);
        var target = delta > 0 ? max + 1 : min - 1;

        Commands.Execute(new DelegateCommand(
            label,
            () => { control.ZOrder = target; Invalidate(); },
            () => { control.ZOrder = old; Invalidate(); }));
    }

    // ── Alignment / distribution (Figma-style, over the selection) ──────────

    public void Align(AlignKind kind)
    {
        var items = Selection.Items;
        if (items.Count < 2)
            return;

        var before = items.ToDictionary(c => c, c => SKRect.Create(c.Location, c.Size));
        var anchor = SKRect.Create(items[0].Location, items[0].Size);
        foreach (var c in items.Skip(1))
            anchor = SKRect.Union(anchor, SKRect.Create(c.Location, c.Size));

        foreach (var c in items)
        {
            var b = SKRect.Create(c.Location, c.Size);
            var x = c.Location.X;
            var y = c.Location.Y;
            switch (kind)
            {
                case AlignKind.Left: x = anchor.Left; break;
                case AlignKind.CenterH: x = anchor.MidX - b.Width / 2f; break;
                case AlignKind.Right: x = anchor.Right - b.Width; break;
                case AlignKind.Top: y = anchor.Top; break;
                case AlignKind.CenterV: y = anchor.MidY - b.Height / 2f; break;
                case AlignKind.Bottom: y = anchor.Bottom - b.Height; break;
            }
            c.Location = new SKPoint(x, y);
        }

        CommitBoundsChange($"Align {kind}", before);
        Invalidate();
    }

    public void Distribute(bool horizontal)
    {
        var items = Selection.Items.OrderBy(c => horizontal ? c.Location.X : c.Location.Y).ToList();
        if (items.Count < 3)
            return;

        var before = items.ToDictionary(c => c, c => SKRect.Create(c.Location, c.Size));
        var first = items[0];
        var last = items[^1];

        if (horizontal)
        {
            var span = last.Location.X - first.Location.X;
            for (var i = 1; i < items.Count - 1; i++)
                items[i].Location = new SKPoint(first.Location.X + span * i / (items.Count - 1), items[i].Location.Y);
        }
        else
        {
            var span = last.Location.Y - first.Location.Y;
            for (var i = 1; i < items.Count - 1; i++)
                items[i].Location = new SKPoint(items[i].Location.X, first.Location.Y + span * i / (items.Count - 1));
        }

        CommitBoundsChange(horizontal ? "Distribute horizontally" : "Distribute vertically", before);
        Invalidate();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void AfterStructureChange()
    {
        StructureChanged?.Invoke();
        Invalidate();
    }

    /// <summary>Raises StructureChanged for external bulk edits (e.g. after loading a project).</summary>
    public void NotifyStructureChanged() => AfterStructureChange();

    internal float Snap(float value) =>
        SnapToGrid ? MathF.Round(value / GridStep) * GridStep : MathF.Round(value);

    private string MakeUniqueName(string baseName)
    {
        _nameCounter++;
        var candidate = $"{baseName}{_nameCounter}";
        while (DesignedControls.Any(c => string.Equals(c.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            _nameCounter++;
            candidate = $"{baseName}{_nameCounter}";
        }

        return candidate;
    }

    public enum AlignKind { Left, CenterH, Right, Top, CenterV, Bottom }

    /// <summary>
    /// The designed "page" itself. Draws the reference grid as part of its own background — i.e.
    /// BEFORE its children render — so placed controls sit visually on top of the grid instead of
    /// the grid being painted over them by the (necessarily topmost, input-owning) overlay.
    /// </summary>
    private sealed class DesignRootCanvas : Element
    {
        private readonly DesignSurface _s;
        private readonly SKPaint _grid = new() { IsAntialias = false, Style = SKPaintStyle.Stroke };

        public DesignRootCanvas(DesignSurface surface) => _s = surface;

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);

            if (!_s.ShowGrid)
                return;

            var r = new SKRect(0, 0, Width, Height);
            var minor = GridStep;

            _grid.StrokeWidth = 1f;
            _grid.Color = ColorScheme.Outline.WithAlpha(34);
            for (var x = r.Left + minor; x < r.Right; x += minor)
                canvas.DrawLine(x, r.Top, x, r.Bottom, _grid);
            for (var y = r.Top + minor; y < r.Bottom; y += minor)
                canvas.DrawLine(r.Left, y, r.Right, y, _grid);

            // Major lines every 5 cells for readability.
            _grid.Color = ColorScheme.Outline.WithAlpha(70);
            for (var x = r.Left + minor * 5; x < r.Right; x += minor * 5)
                canvas.DrawLine(x, r.Top, x, r.Bottom, _grid);
            for (var y = r.Top + minor * 5; y < r.Bottom; y += minor * 5)
                canvas.DrawLine(r.Left, y, r.Right, y, _grid);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _grid.Dispose();
            base.Dispose(disposing);
        }
    }

    // ═════════════════════════ Overlay ══════════════════════════════════════

    private enum Grip
    {
        None, Body, Marquee, PanView,
        TopLeft, Top, TopRight, Left, Right, BottomLeft, Bottom, BottomRight
    }

    private sealed class DesignOverlay : Element
    {
        private readonly DesignSurface _s;

        private Grip _mode = Grip.None;
        private SKPoint _dragStart;
        private SKRect _marquee;
        private Dictionary<ElementBase, SKRect>? _dragBefore;
        private readonly List<(SKPoint A, SKPoint B)> _activeGuides = new();

        private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };
        private readonly SKPaint _fill = new() { IsAntialias = true };
        private readonly SKPaint _guide = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
        private readonly SKPathEffect _previewDash = SKPathEffect.CreateDash(new[] { 5f, 4f }, 0f);
        private readonly SKFont _labelFont = Application.DefaultFont;

        public DesignOverlay(DesignSurface surface) => _s = surface;

        private Element Root => _s._root;

        // ── Painting ──

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);

            // Live drop ghost — where a toolbox entry would land if released right now.
            if (_s._previewEntry is { } previewEntry)
            {
                var pr = _s._previewRect;
                var overlayRect = SKRect.Create(Root.Location.X + pr.Left, Root.Location.Y + pr.Top, pr.Width, pr.Height);

                _fill.Color = ColorScheme.Primary.WithAlpha(38);
                canvas.DrawRoundRect(overlayRect, 6f, 6f, _fill);

                _stroke.Color = ColorScheme.Primary.WithAlpha(210);
                _stroke.PathEffect = _previewDash;
                canvas.DrawRoundRect(overlayRect, 6f, 6f, _stroke);
                _stroke.PathEffect = null;

                var label = previewEntry.DisplayName;
                var textWidth = _labelFont.MeasureText(label);
                var chip = SKRect.Create(overlayRect.Left, overlayRect.Top - 24f, textWidth + 16f, 19f);
                _fill.Color = ColorScheme.Primary;
                canvas.DrawRoundRect(chip, 4f, 4f, _fill);
                _fill.Color = SKColors.White;
                TextRenderer.DrawText(canvas, label, chip.Left + 8f, chip.MidY + _labelFont.Size * 0.35f, _labelFont, _fill);
            }

            // Selection adorners.
            var items = _s.Selection.Items;
            if (items.Count > 0)
            {
                _stroke.Color = ColorScheme.Primary;
                foreach (var c in items)
                    canvas.DrawRect(ToOverlay(c), _stroke);

                if (items.Count == 1 && !_s.Locked.Contains(items[0]))
                {
                    var bounds = ToOverlay(items[0]);
                    _fill.Color = ColorScheme.Surface;
                    foreach (var (_, rect) in EnumerateGrips(bounds))
                    {
                        canvas.DrawRect(rect, _fill);
                        canvas.DrawRect(rect, _stroke);
                    }
                }
                else if (items.Count > 1)
                {
                    var union = ToOverlay(items[0]);
                    foreach (var c in items.Skip(1))
                        union = SKRect.Union(union, ToOverlay(c));
                    _stroke.Color = ColorScheme.Primary.WithAlpha(140);
                    canvas.DrawRect(SKRect.Inflate(union, 3f, 3f), _stroke);
                }
            }

            // Marquee.
            if (_mode == Grip.Marquee)
            {
                _fill.Color = ColorScheme.Primary.WithAlpha(26);
                canvas.DrawRect(_marquee, _fill);
                _stroke.Color = ColorScheme.Primary.WithAlpha(160);
                canvas.DrawRect(_marquee, _stroke);
            }

            // Smart guides.
            if (_activeGuides.Count > 0)
            {
                _guide.Color = new SKColor(236, 72, 153); // guide magenta
                foreach (var (a, b) in _activeGuides)
                    canvas.DrawLine(a, b, _guide);
            }
        }

        // ── Input ──

        public override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Middle)
            {
                _mode = Grip.PanView;
                _dragStart = e.Location;
                Capture();
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                var hitR = HitControl(e.Location);
                if (hitR != null && !_s.Selection.Contains(hitR))
                    _s.Selection.SelectOnly(hitR);
                ShowContextMenu(e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            var ctrl = (ModifierKeys & Keys.Control) == Keys.Control;

            // Resize grips (single selection only).
            if (_s.Selection.Items.Count == 1 && !_s.Locked.Contains(_s.Selection.Items[0]))
            {
                var grip = HitGrip(ToOverlay(_s.Selection.Items[0]), e.Location);
                if (grip != Grip.None)
                {
                    BeginBoundsDrag(grip, e.Location);
                    return;
                }
            }

            var hit = HitControl(e.Location);
            if (hit == null)
            {
                if (!ctrl)
                    _s.Selection.Clear();
                _mode = Grip.Marquee;
                _dragStart = e.Location;
                _marquee = SKRect.Create(e.Location, SKSize.Empty);
                Capture();
                return;
            }

            if (ctrl)
            {
                _s.Selection.Toggle(hit);
            }
            else if (!_s.Selection.Contains(hit))
            {
                _s.Selection.SelectOnly(hit);
            }

            if (_s.Selection.Contains(hit) && !_s.Locked.Contains(hit))
                BeginBoundsDrag(Grip.Body, e.Location);
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            switch (_mode)
            {
                case Grip.PanView:
                    _s.Pan(e.X - _dragStart.X, e.Y - _dragStart.Y);
                    _dragStart = e.Location;
                    return;
                case Grip.Marquee:
                    _marquee = RectFromPoints(_dragStart, e.Location);
                    Invalidate();
                    return;
                case Grip.None:
                    UpdateHoverCursor(e.Location);
                    return;
                default:
                    ApplyBoundsDrag(e.Location);
                    return;
            }
        }

        public override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            switch (_mode)
            {
                case Grip.Marquee:
                    CommitMarquee();
                    break;
                case Grip.PanView:
                    break;
                case Grip.None:
                    return;
                default:
                    _activeGuides.Clear();
                    if (_dragBefore != null)
                        _s.CommitBoundsChange(_mode == Grip.Body ? "Move" : "Resize", _dragBefore);
                    _dragBefore = null;
                    break;
            }

            _mode = Grip.None;
            Release();
            Invalidate();
        }

        /// <summary>
        /// This overlay drives pan/zoom straight off the wheel instead of the built-in
        /// scrollbar-backed AutoScroll path, so it must opt in explicitly — otherwise
        /// <c>ElementBase</c>'s wheel routing never considers it a wheel target and
        /// <see cref="OnMouseWheel"/> below is never actually invoked.
        /// </summary>
        protected override bool HandlesMouseWheelInput => true;

        public override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                var factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
                _s.ZoomAt(_s.Zoom * factor, e.Location);
                e.Handled = true;
                return;
            }

            // Plain wheel pans vertically, Shift+wheel horizontally (Figma-style).
            var step = 48f;
            var shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
            var d = e.Delta > 0 ? step : -step;
            _s.Pan(shift ? d : 0f, shift ? 0f : d);
            e.Handled = true;
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled)
                return;

            var step = e.Shift ? GridStep : 1f;
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    _s.DeleteSelection();
                    e.Handled = true;
                    break;
                case Keys.D when e.Control:
                    _s.DuplicateSelection();
                    e.Handled = true;
                    break;
                case Keys.A when e.Control:
                    _s.Selection.SetMany(_s.DesignedControls);
                    e.Handled = true;
                    break;
                case Keys.Left: NudgeSelection(-step, 0); e.Handled = true; break;
                case Keys.Right: NudgeSelection(step, 0); e.Handled = true; break;
                case Keys.Up: NudgeSelection(0, -step); e.Handled = true; break;
                case Keys.Down: NudgeSelection(0, step); e.Handled = true; break;
            }
        }

        // ── Drag machinery ──

        private void BeginBoundsDrag(Grip grip, SKPoint mouse)
        {
            _mode = grip;
            _dragStart = mouse;
            _dragBefore = _s.Selection.Items
                .Where(c => !_s.Locked.Contains(c))
                .ToDictionary(c => c, c => SKRect.Create(c.Location, c.Size));
            Capture();
        }

        private void ApplyBoundsDrag(SKPoint mouse)
        {
            if (_dragBefore == null || _dragBefore.Count == 0)
                return;

            var dx = mouse.X - _dragStart.X;
            var dy = mouse.Y - _dragStart.Y;
            _activeGuides.Clear();

            if (_mode == Grip.Body)
            {
                // Snap using the primary item's would-be bounds; the same delta applies to all.
                var primary = _s.Selection.Primary!;
                var pb = _dragBefore[primary];
                var targetLeft = _s.Snap(pb.Left + dx);
                var targetTop = _s.Snap(pb.Top + dy);
                var snapped = ApplySmartGuides(primary, SKRect.Create(targetLeft, targetTop, pb.Width, pb.Height));
                var fx = snapped.Left - pb.Left;
                var fy = snapped.Top - pb.Top;

                foreach (var (control, before) in _dragBefore)
                    control.Location = new SKPoint(before.Left + fx, before.Top + fy);
            }
            else
            {
                var control = _s.Selection.Items[0];
                var b = _dragBefore[control];
                float L = b.Left, T = b.Top, R = b.Right, Bt = b.Bottom;

                if (_mode is Grip.TopLeft or Grip.Left or Grip.BottomLeft) L = _s.Snap(L + dx);
                if (_mode is Grip.TopRight or Grip.Right or Grip.BottomRight) R = _s.Snap(R + dx);
                if (_mode is Grip.TopLeft or Grip.Top or Grip.TopRight) T = _s.Snap(T + dy);
                if (_mode is Grip.BottomLeft or Grip.Bottom or Grip.BottomRight) Bt = _s.Snap(Bt + dy);

                if (R - L < MinControlSize) { if (_mode is Grip.Left or Grip.TopLeft or Grip.BottomLeft) L = R - MinControlSize; else R = L + MinControlSize; }
                if (Bt - T < MinControlSize) { if (_mode is Grip.Top or Grip.TopLeft or Grip.TopRight) T = Bt - MinControlSize; else Bt = T + MinControlSize; }

                control.Location = new SKPoint(L, T);
                control.Size = new SKSize(R - L, Bt - T);
            }

            _s.SelectionBoundsChanged?.Invoke();
            Invalidate();
        }

        /// <summary>Aligns the dragged bounds to sibling edges/centers within threshold and records guide lines.</summary>
        private SKRect ApplySmartGuides(ElementBase dragged, SKRect bounds)
        {
            if (!_s.SmartGuides)
                return bounds;

            var candidatesX = new List<float> { 0f, Root.Width / 2f, Root.Width };
            var candidatesY = new List<float> { 0f, Root.Height / 2f, Root.Height };
            foreach (var other in _s.DesignedControls)
            {
                if (ReferenceEquals(other, dragged) || !other.Visible || _s.Selection.Contains(other))
                    continue;
                var ob = SKRect.Create(other.Location, other.Size);
                candidatesX.Add(ob.Left); candidatesX.Add(ob.MidX); candidatesX.Add(ob.Right);
                candidatesY.Add(ob.Top); candidatesY.Add(ob.MidY); candidatesY.Add(ob.Bottom);
            }

            var result = bounds;
            foreach (var (edge, set) in new (float, string)[] { (bounds.Left, "L"), (bounds.MidX, "C"), (bounds.Right, "R") })
            {
                var best = candidatesX.OrderBy(c => Math.Abs(c - edge)).First();
                if (Math.Abs(best - edge) <= GuideSnapDistance)
                {
                    var shift = best - edge;
                    result = SKRect.Create(result.Left + shift, result.Top, result.Width, result.Height);
                    var gx = Root.Location.X + best;
                    _activeGuides.Add((new SKPoint(gx, Root.Location.Y), new SKPoint(gx, Root.Location.Y + Root.Height)));
                    break;
                }
            }

            foreach (var (edge, set) in new (float, string)[] { (result.Top, "T"), (result.MidY, "M"), (result.Bottom, "B") })
            {
                var best = candidatesY.OrderBy(c => Math.Abs(c - edge)).First();
                if (Math.Abs(best - edge) <= GuideSnapDistance)
                {
                    var shift = best - edge;
                    result = SKRect.Create(result.Left, result.Top + shift, result.Width, result.Height);
                    var gy = Root.Location.Y + best;
                    _activeGuides.Add((new SKPoint(Root.Location.X, gy), new SKPoint(Root.Location.X + Root.Width, gy)));
                    break;
                }
            }

            return result;
        }

        private void NudgeSelection(float dx, float dy)
        {
            var movable = _s.Selection.Items.Where(c => !_s.Locked.Contains(c)).ToList();
            if (movable.Count == 0)
                return;

            var before = movable.ToDictionary(c => c, c => SKRect.Create(c.Location, c.Size));
            foreach (var c in movable)
                c.Location = new SKPoint(c.Location.X + dx, c.Location.Y + dy);
            _s.CommitBoundsChange("Nudge", before);
            Invalidate();
        }

        private void CommitMarquee()
        {
            var rootRel = new SKRect(
                _marquee.Left - Root.Location.X, _marquee.Top - Root.Location.Y,
                _marquee.Right - Root.Location.X, _marquee.Bottom - Root.Location.Y);

            var hits = _s.DesignedControls
                .Where(c => c.Visible && rootRel.IntersectsWith(SKRect.Create(c.Location, c.Size)))
                .ToList();

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                var merged = _s.Selection.Items.Concat(hits);
                _s.Selection.SetMany(merged);
            }
            else
            {
                _s.Selection.SetMany(hits);
            }
        }

        private void ShowContextMenu(SKPoint at)
        {
            var menu = new ContextMenuStrip();
            var hasSelection = _s.Selection.Count > 0;
            var single = _s.Selection.Count == 1 ? _s.Selection.Items[0] : null;

            if (hasSelection)
            {
                menu.AddItem(new MenuItem("Duplicate", (_, _) => _s.DuplicateSelection()) { ShortcutKeys = Keys.Control | Keys.D });
                menu.AddItem(new MenuItem("Delete", (_, _) => _s.DeleteSelection()) { ShortcutKeys = Keys.Delete });
                if (single != null)
                {
                    menu.AddItem(new MenuItem("Bring to front", (_, _) => _s.BringToFront(single)));
                    menu.AddItem(new MenuItem("Send to back", (_, _) => _s.SendToBack(single)));
                    var locked = _s.Locked.Contains(single);
                    menu.AddItem(new MenuItem(locked ? "Unlock" : "Lock", (_, _) =>
                    {
                        if (!_s.Locked.Remove(single)) _s.Locked.Add(single);
                        _s.StructureChanged?.Invoke();
                        Invalidate();
                    }));
                }
                if (_s.Selection.Count >= 2)
                {
                    menu.AddItem(new MenuItem("Align left", (_, _) => _s.Align(AlignKind.Left)));
                    menu.AddItem(new MenuItem("Align top", (_, _) => _s.Align(AlignKind.Top)));
                    menu.AddItem(new MenuItem("Align centers", (_, _) => { _s.Align(AlignKind.CenterH); _s.Align(AlignKind.CenterV); }));
                }
                if (_s.Selection.Count >= 3)
                {
                    menu.AddItem(new MenuItem("Distribute horizontally", (_, _) => _s.Distribute(true)));
                    menu.AddItem(new MenuItem("Distribute vertically", (_, _) => _s.Distribute(false)));
                }
            }
            else
            {
                menu.AddItem(new MenuItem("Select all", (_, _) => _s.Selection.SetMany(_s.DesignedControls)) { ShortcutKeys = Keys.Control | Keys.A });
                menu.AddItem(new MenuItem("Fit to view", (_, _) => _s.FitToView()));
            }

            menu.Closed += (_, _) => menu.Dispose();
            menu.Show(this, PointToScreen(at));
        }

        // ── Hit testing (overlay space == logical child space of the surface) ──

        private SKRect ToOverlay(ElementBase designed) =>
            SKRect.Create(
                Root.Location.X + designed.Location.X,
                Root.Location.Y + designed.Location.Y,
                designed.Width, designed.Height);

        private ElementBase? HitControl(SKPoint p)
        {
            var rel = new SKPoint(p.X - Root.Location.X, p.Y - Root.Location.Y);
            ElementBase? best = null;
            var bestZ = int.MinValue;
            foreach (var child in _s.DesignedControls)
            {
                if (!child.Visible || _s.Locked.Contains(child))
                    continue;
                if (!SKRect.Create(child.Location, child.Size).Contains(rel))
                    continue;
                if (child.ZOrder >= bestZ)
                {
                    best = child;
                    bestZ = child.ZOrder;
                }
            }

            return best;
        }

        private void UpdateHoverCursor(SKPoint p)
        {
            var cursor = Cursors.Default;
            if (_s.Selection.Items.Count == 1)
            {
                cursor = HitGrip(ToOverlay(_s.Selection.Items[0]), p) switch
                {
                    Grip.TopLeft or Grip.BottomRight => Cursors.SizeNWSE,
                    Grip.TopRight or Grip.BottomLeft => Cursors.SizeNESW,
                    Grip.Left or Grip.Right => Cursors.SizeWE,
                    Grip.Top or Grip.Bottom => Cursors.SizeNS,
                    _ => HitControl(p) != null ? Cursors.SizeAll : Cursors.Default,
                };
            }
            else if (HitControl(p) != null)
            {
                cursor = Cursors.SizeAll;
            }

            Cursor = cursor;
        }

        private static SKRect RectFromPoints(SKPoint a, SKPoint b) =>
            new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));

        private static IEnumerable<(Grip Grip, SKRect Rect)> EnumerateGrips(SKRect b)
        {
            const float h = GripSize / 2f;
            yield return (Grip.TopLeft, SKRect.Create(b.Left - h, b.Top - h, GripSize, GripSize));
            yield return (Grip.Top, SKRect.Create(b.MidX - h, b.Top - h, GripSize, GripSize));
            yield return (Grip.TopRight, SKRect.Create(b.Right - h, b.Top - h, GripSize, GripSize));
            yield return (Grip.Left, SKRect.Create(b.Left - h, b.MidY - h, GripSize, GripSize));
            yield return (Grip.Right, SKRect.Create(b.Right - h, b.MidY - h, GripSize, GripSize));
            yield return (Grip.BottomLeft, SKRect.Create(b.Left - h, b.Bottom - h, GripSize, GripSize));
            yield return (Grip.Bottom, SKRect.Create(b.MidX - h, b.Bottom - h, GripSize, GripSize));
            yield return (Grip.BottomRight, SKRect.Create(b.Right - h, b.Bottom - h, GripSize, GripSize));
        }

        private static Grip HitGrip(SKRect bounds, SKPoint p)
        {
            foreach (var (grip, rect) in EnumerateGrips(bounds))
                if (SKRect.Inflate(rect, 2f, 2f).Contains(p))
                    return grip;
            return Grip.None;
        }

        private void Capture() => GetParentWindow()?.SetMouseCapture(this);
        private void Release() => GetParentWindow()?.ReleaseMouseCapture(this);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stroke.Dispose();
                _fill.Dispose();
                _guide.Dispose();
                _previewDash.Dispose();
                _labelFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
