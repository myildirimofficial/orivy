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

    /// <summary>
    /// Top-level designed controls that act as control groups: a container other controls have been
    /// nested into via <see cref="GroupSelection"/> or a toolbox drop while hovering it (see
    /// <see cref="DesignOverlay"/>'s drag-highlight). A group's own children are real nested
    /// <see cref="ElementBase.Controls"/> — they render, save/load and code-gen for free through
    /// normal parent/child composition — but are NOT part of <see cref="DesignedControls"/>: the
    /// canvas selects/moves/resizes a group as one unit rather than exposing per-child grips, so
    /// hit-testing, marquee selection and alignment don't need to become nesting-aware.
    /// </summary>
    public HashSet<ElementBase> Groups { get; } = new();

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
            .Background(ColorScheme.Surface.WithAlpha(140))
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

    public ElementBase AddControl(ControlEntry entry, SKPoint? location = null) => AddControl(entry, location, null);

    /// <summary>
    /// Adds a new control. When <paramref name="group"/> is set, the control nests as that group's
    /// child instead of the root's — <paramref name="location"/> is then interpreted relative to the
    /// group, matching where it will actually render.
    /// </summary>
    public ElementBase AddControl(ControlEntry entry, SKPoint? location, ElementBase? group)
    {
        var control = entry.CreateInstance();
        PrepareForDesign(control);
        control.Name = MakeUniqueName(char.ToLowerInvariant(entry.DisplayName[0]) + entry.DisplayName[1..]);
        if (string.IsNullOrEmpty(control.Text))
            control.Text = control.Name;

        var cascade = 24f + (DesignedControls.Count % 8) * 16f;
        var at = location ?? new SKPoint(cascade, cascade);
        control.Location = new SKPoint(Snap(at.X), Snap(at.Y));

        var parentControls = group != null ? group.Controls : _root.Controls;
        Commands.Execute(new DelegateCommand(
            group != null ? $"Add {entry.DisplayName} to {group.Name}" : $"Add {entry.DisplayName}",
            () => { parentControls.Add(control); AfterStructureChange(); Selection.SelectOnly(group ?? control); },
            () => { Selection.Remove(control); parentControls.Remove(control); Locked.Remove(control); AfterStructureChange(); }));

        return control;
    }

    /// <summary>Drops a toolbox entry at a point given in this surface's client (screen-derived) space.
    /// Nests into whatever existing control is hovered (see <see cref="PreviewDrop"/>), if any — any
    /// designed control can host children, not just an explicit <see cref="Groups"/> shell.</summary>
    public ElementBase DropAt(ControlEntry entry, SKPoint clientPoint)
    {
        var size = entry.CreateInstance().Size; // default size to center the drop under the cursor
        var logical = ToLogical(clientPoint);
        var target = FindNestingTargetAt(logical);
        var location = DropTargetLocation(logical, size, target);
        ClearDropPreview();
        return AddControl(entry, location, target);
    }

    private ControlEntry? _previewEntry;
    private SKRect _previewRect;
    private ElementBase? _previewGroup;

    /// <summary>The control a toolbox drag is currently hovering, if any — drawn as a highlighted
    /// nesting target by <see cref="DesignOverlay"/> and used by <see cref="DropAt"/> on release.</summary>
    internal ElementBase? PreviewGroup => _previewGroup;

    /// <summary>
    /// Shows a live ghost of where <paramref name="entry"/> would land if dropped at
    /// <paramref name="clientPoint"/> right now — called continuously while a toolbox drag hovers
    /// over the canvas, so the user sees the snapped placement before releasing. Also detects and
    /// highlights whatever existing control is under the cursor as a nesting target.
    /// </summary>
    public void PreviewDrop(ControlEntry entry, SKPoint clientPoint)
    {
        var size = ControlCatalog.DefaultSizeFor(entry);
        var logical = ToLogical(clientPoint);
        var target = FindNestingTargetAt(logical);
        var location = DropTargetLocation(logical, size, target);
        _previewEntry = entry;
        _previewGroup = target;
        _previewRect = SKRect.Create(location, size);
        Invalidate();
    }

    /// <summary>Hides the drop ghost (drag left the canvas, was dropped, or was cancelled).</summary>
    public void ClearDropPreview()
    {
        if (_previewEntry == null && _previewGroup == null)
            return;
        _previewEntry = null;
        _previewGroup = null;
        Invalidate();
    }

    /// <summary>Client (screen-derived) point → this surface's logical (pan/zoom-undone) space.</summary>
    private SKPoint ToLogical(SKPoint clientPoint) => new(clientPoint.X / _zoom, clientPoint.Y / _zoom);

    /// <summary>Sets (or clears, with null) the nesting target highlighted while dragging an existing
    /// control over the canvas — shares the toolbox-drop preview's paint code and field.</summary>
    internal void SetHoverNestingTarget(ElementBase? target)
    {
        if (ReferenceEquals(_previewGroup, target))
            return;
        _previewGroup = target;
        Invalidate();
    }

    /// <summary>
    /// The deepest designed control — at any nesting depth, root included implicitly via a null
    /// result — whose bounds contain a point already in this surface's logical space. This is the
    /// single nesting-target rule shared by toolbox drops (<see cref="PreviewDrop"/>/<see cref="DropAt"/>)
    /// and dragging an already-placed control onto another (<see cref="TryReparentOnDrop"/>): any
    /// designed control can host children, matching the "everything can become another's container"
    /// behavior a real designer offers, rather than requiring an explicit group shell first.
    /// </summary>
    internal ElementBase? FindNestingTargetAt(SKPoint logicalPoint, ElementBase? excluding = null)
    {
        var rootRel = new SKPoint(logicalPoint.X - _root.Location.X, logicalPoint.Y - _root.Location.Y);
        return FindNestingTargetRecursive(DesignedControls, rootRel, excluding);
    }

    private ElementBase? FindNestingTargetRecursive(IEnumerable<ElementBase> candidates, SKPoint parentRel, ElementBase? excluding)
    {
        ElementBase? best = null;
        var bestZ = int.MinValue;
        foreach (var child in candidates)
        {
            if (!child.Visible || Locked.Contains(child))
                continue;
            if (excluding != null && (ReferenceEquals(child, excluding) || IsDescendantOf(child, excluding)))
                continue;
            if (!SKRect.Create(child.Location, child.Size).Contains(parentRel))
                continue;
            if (child.ZOrder >= bestZ)
            {
                best = child;
                bestZ = child.ZOrder;
            }
        }

        if (best == null)
            return null;

        var childRel = new SKPoint(parentRel.X - best.Location.X, parentRel.Y - best.Location.Y);
        var nested = FindNestingTargetRecursive(NestedDesignedChildrenOf(best), childRel, excluding);
        return nested ?? best;
    }

    private static IEnumerable<ElementBase> NestedDesignedChildrenOf(ElementBase control)
    {
        foreach (var c in control.Controls)
            if (c is ElementBase child and not ScrollBar)
                yield return child;
    }

    /// <summary>Is <paramref name="ancestorCandidate"/> an ancestor of <paramref name="control"/>?
    /// Used to keep a dragged control from being dropped into one of its own descendants.</summary>
    private static bool IsDescendantOf(ElementBase control, ElementBase ancestorCandidate)
    {
        var current = control.Parent;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestorCandidate))
                return true;
            current = current.Parent;
        }

        return false;
    }

    /// <summary>Root-relative position of a designed control at any nesting depth — walks up through
    /// container parents, accumulating their locations, until reaching the design root.</summary>
    internal SKPoint GetDesignSpaceLocation(ElementBase control)
    {
        var x = control.Location.X;
        var y = control.Location.Y;
        var current = control.Parent;
        while (current != null && !ReferenceEquals(current, _root))
        {
            x += current.Location.X;
            y += current.Location.Y;
            current = current.Parent;
        }

        return new SKPoint(x, y);
    }

    /// <summary>Snapped top-left for a <paramref name="size"/>d control centered under a logical-space
    /// point — relative to <paramref name="parent"/> when given, otherwise root-relative.</summary>
    private SKPoint DropTargetLocation(SKPoint logicalPoint, SKSize size, ElementBase? parent = null)
    {
        var rootRel = new SKPoint(logicalPoint.X - _root.Location.X, logicalPoint.Y - _root.Location.Y);
        if (parent != null)
        {
            var parentAbsolute = GetDesignSpaceLocation(parent);
            rootRel = new SKPoint(rootRel.X - parentAbsolute.X, rootRel.Y - parentAbsolute.Y);
        }

        return new SKPoint(Snap(rootRel.X - size.Width / 2f), Snap(rootRel.Y - size.Height / 2f));
    }

    /// <summary>
    /// Called when a single-selection move drag ends: if the drop point now lands over a different
    /// existing control than the one it started in, reparents it there (or back to the root, if
    /// dropped in empty canvas space), rewriting its Location to stay relative to the new parent
    /// while preserving where it visually ended up. Returns false (no-op) when the parent didn't
    /// change, so the caller can fall back to a plain <see cref="CommitBoundsChange"/>. One undo step
    /// covers the whole gesture — move and reparent together — rather than two.
    /// </summary>
    internal bool TryReparentOnDrop(ElementBase control, SKPoint logicalDropPoint, SKRect preDragRect)
    {
        if (Locked.Contains(control))
            return false;

        var newParent = FindNestingTargetAt(logicalDropPoint, excluding: control) ?? _root;
        var currentParent = control.Parent ?? _root;
        if (ReferenceEquals(newParent, currentParent))
            return false;

        // Preserve the control's current on-screen (root-relative) position across the reparent —
        // it was already dragged to this spot live; only its parent (and thus what Location is
        // relative to) is changing now.
        var absolute = GetDesignSpaceLocation(control);
        var newParentAbsolute = ReferenceEquals(newParent, _root) ? SKPoint.Empty : GetDesignSpaceLocation(newParent);
        var newLocation = new SKPoint(absolute.X - newParentAbsolute.X, absolute.Y - newParentAbsolute.Y);
        var newSize = control.Size;

        Commands.Execute(new DelegateCommand(
            ReferenceEquals(newParent, _root) ? $"Move {control.Name} to canvas" : $"Move {control.Name} into {newParent.Name}",
            () =>
            {
                currentParent.Controls.Remove(control);
                control.Location = newLocation;
                control.Size = newSize;
                newParent.Controls.Add(control);
                AfterStructureChange();
                Selection.SelectOnly(control);
            },
            () =>
            {
                newParent.Controls.Remove(control);
                control.Location = preDragRect.Location;
                control.Size = preDragRect.Size;
                currentParent.Controls.Add(control);
                AfterStructureChange();
                Selection.SelectOnly(control);
            }));

        return true;
    }

    /// <summary>Groups the current multi-selection into a new container control, nesting the
    /// selected controls as its children with locations rebased relative to it. Undoable.</summary>
    public void GroupSelection()
    {
        var items = Selection.Items.Where(c => !Locked.Contains(c) && !Groups.Contains(c)).ToList();
        if (items.Count < 2)
            return;

        var bounds = items[0].Bounds;
        for (var i = 1; i < items.Count; i++)
            bounds = SKRect.Union(bounds, items[i].Bounds);
        bounds.Inflate(12f, 12f);

        var group = new Element
        {
            Name = MakeUniqueName("group"),
            Location = new SKPoint(Snap(bounds.Left), Snap(bounds.Top)),
            Size = new SKSize(Snap(bounds.Width), Snap(bounds.Height)),
            Radius = new Radius(6),
            Border = new Thickness(1),
        };
        group.ConfigureVisualStyles(styles => styles.Base(b => b
            .Background(SKColors.Transparent)
            .BorderColor(ColorScheme.Outline.WithAlpha(70))));

        var originalLocations = items.ToDictionary(c => c, c => c.Location);
        var originalParents = items.ToDictionary(c => c, c => c.Parent ?? _root);

        Commands.Execute(new DelegateCommand(
            "Group Selection",
            () =>
            {
                _root.Controls.Add(group);
                Groups.Add(group);
                foreach (var item in items)
                {
                    var loc = originalLocations[item];
                    originalParents[item].Controls.Remove(item);
                    item.Location = new SKPoint(loc.X - group.Location.X, loc.Y - group.Location.Y);
                    group.Controls.Add(item);
                }
                AfterStructureChange();
                Selection.SelectOnly(group);
            },
            () =>
            {
                foreach (var item in items)
                {
                    group.Controls.Remove(item);
                    item.Location = originalLocations[item];
                    originalParents[item].Controls.Add(item);
                }
                Groups.Remove(group);
                _root.Controls.Remove(group);
                AfterStructureChange();
                Selection.SetMany(items);
            }));
    }

    /// <summary>Dissolves a group, reparenting its children back to the root at their absolute
    /// positions and removing the now-empty group shell. Undoable.</summary>
    public void Ungroup(ElementBase group)
    {
        if (!Groups.Contains(group))
            return;

        var children = group.Controls.OfType<ElementBase>().ToList();
        var relativeLocations = children.ToDictionary(c => c, c => c.Location);
        var groupLocation = group.Location;

        Commands.Execute(new DelegateCommand(
            "Ungroup",
            () =>
            {
                foreach (var child in children)
                {
                    group.Controls.Remove(child);
                    var rel = relativeLocations[child];
                    child.Location = new SKPoint(rel.X + groupLocation.X, rel.Y + groupLocation.Y);
                    _root.Controls.Add(child);
                }
                Groups.Remove(group);
                _root.Controls.Remove(group);
                AfterStructureChange();
                Selection.SetMany(children);
            },
            () =>
            {
                foreach (var child in children)
                {
                    _root.Controls.Remove(child);
                    child.Location = relativeLocations[child];
                    group.Controls.Add(child);
                }
                _root.Controls.Add(group);
                Groups.Add(group);
                AfterStructureChange();
                Selection.SelectOnly(group);
            }));
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
        // A locked control is meant to be protected from exactly this kind of destructive edit —
        // matches the same exclusion NudgeSelection/BeginBoundsDrag/GroupSelection already apply.
        var doomed = Selection.Items.Where(c => !Locked.Contains(c)).ToList();
        if (doomed.Count == 0)
            return;

        var wasGroup = doomed.Where(Groups.Contains).ToList();

        Commands.Execute(new DelegateCommand(
            doomed.Count == 1 ? $"Delete {doomed[0].Name}" : $"Delete {doomed.Count} controls",
            () =>
            {
                foreach (var control in doomed)
                {
                    Selection.Remove(control);
                    _root.Controls.Remove(control);
                    Groups.Remove(control);
                }
                AfterStructureChange();
            },
            () =>
            {
                foreach (var control in doomed)
                {
                    _root.Controls.Add(control);
                    if (wasGroup.Contains(control))
                        Groups.Add(control);
                }
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
            ElementBase clone;
            try
            {
                if (Activator.CreateInstance(source.GetType()) is not ElementBase created)
                    continue;
                clone = created;
            }
            catch
            {
                // A control without a working parameterless constructor just can't be duplicated —
                // skip it rather than taking the whole operation down with an uncaught exception.
                continue;
            }

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

        // Locked/Groups membership must round-trip through undo symmetrically with the controls
        // themselves, or undoing a clear silently strips lock/group state that was never touched.
        var lockedBefore = all.Where(Locked.Contains).ToList();
        var groupsBefore = all.Where(Groups.Contains).ToList();

        Commands.Execute(new DelegateCommand(
            "New document",
            () =>
            {
                Selection.Clear();
                foreach (var c in all)
                {
                    _root.Controls.Remove(c);
                    Locked.Remove(c);
                    Groups.Remove(c);
                }
                AfterStructureChange();
            },
            () =>
            {
                foreach (var c in all)
                {
                    _root.Controls.Add(c);
                    if (lockedBefore.Contains(c))
                        Locked.Add(c);
                    if (groupsBefore.Contains(c))
                        Groups.Add(c);
                }
                AfterStructureChange();
            }));
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

    /// <summary>Disposing a container never cascades to its children in this framework — without this
    /// override, closing a document tab would leak the root canvas's and overlay's native Skia paints
    /// (<see cref="DesignRootCanvas"/>, <see cref="DesignOverlay"/>) every time.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _root.Dispose();
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

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

            // Highlight the control group under the cursor while a toolbox drag hovers it — the
            // visual cue that releasing here nests the new control instead of dropping onto the root.
            if (_s._previewGroup is { } hoveredGroup)
            {
                var groupRect = SKRect.Create(
                    Root.Location.X + hoveredGroup.Location.X,
                    Root.Location.Y + hoveredGroup.Location.Y,
                    hoveredGroup.Width, hoveredGroup.Height);

                _fill.Color = ColorScheme.Primary.WithAlpha(24);
                canvas.DrawRoundRect(groupRect, 6f, 6f, _fill);
                _stroke.Color = ColorScheme.Primary;
                _stroke.StrokeWidth = 2f;
                canvas.DrawRoundRect(groupRect, 6f, 6f, _stroke);
                _stroke.StrokeWidth = 1.4f;
            }

            // Locked-control affordance: a subtle dashed outline + lock glyph even when unselected, so
            // a control that has stopped responding to clicks doesn't just look broken. Selected
            // locked controls skip this — the selection outline (with no resize grips) already tells
            // that story — to avoid drawing two competing outlines on the same control.
            foreach (var locked in _s.Locked)
            {
                if (locked.Parent == null || !locked.Visible || _s.Selection.Contains(locked))
                    continue;

                var lockedRect = ToOverlay(locked);
                _stroke.Color = ColorScheme.ForeColor.WithAlpha(90);
                _stroke.PathEffect = _previewDash;
                canvas.DrawRect(lockedRect, _stroke);
                _stroke.PathEffect = null;

                var badge = SKRect.Create(lockedRect.Right - 16f, lockedRect.Top - 8f, 16f, 16f);
                _fill.Color = ColorScheme.SurfaceContainerHigh;
                canvas.DrawOval(badge, _fill);
                _stroke.Color = ColorScheme.ForeColor.WithAlpha(170);
                ToolbarIcons.Draw(canvas, "lock", SKRect.Inflate(badge, -3.5f, -3.5f), _stroke);
            }

            // Live drop ghost — where a toolbox entry would land if released right now.
            if (_s._previewEntry is { } previewEntry)
            {
                var pr = _s._previewRect;
                var groupOffset = _s._previewGroup?.Location ?? SKPoint.Empty;
                var overlayRect = SKRect.Create(
                    Root.Location.X + groupOffset.X + pr.Left,
                    Root.Location.Y + groupOffset.Y + pr.Top,
                    pr.Width, pr.Height);

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
                    _s.SetHoverNestingTarget(null);
                    if (_dragBefore != null)
                    {
                        // A single-item move that ends over a different control reparents into it
                        // instead of just repositioning — the reparent's own undo command already
                        // captures the full before/after (parent + position), so it replaces rather
                        // than stacks with the normal bounds-change commit.
                        var reparented = _mode == Grip.Body
                            && _s.Selection.Count == 1
                            && _s.TryReparentOnDrop(_s.Selection.Items[0], e.Location, _dragBefore[_s.Selection.Items[0]]);

                        if (!reparented)
                            _s.CommitBoundsChange(_mode == Grip.Body ? "Move" : "Resize", _dragBefore);
                    }
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
                case Keys.G when e.Control && e.Shift:
                    if (_s.Selection.Count == 1 && _s.Groups.Contains(_s.Selection.Items[0]))
                        _s.Ungroup(_s.Selection.Items[0]);
                    e.Handled = true;
                    break;
                case Keys.G when e.Control:
                    _s.GroupSelection();
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

                // Live nesting-target highlight while dragging an existing control, mirroring the
                // toolbox-drop preview — only meaningful for a single selected item, since a
                // multi-item drag has no single "the dragged control" to reparent on drop.
                _s.SetHoverNestingTarget(_s.Selection.Count == 1
                    ? _s.FindNestingTargetAt(mouse, excluding: primary)
                    : null);
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
                .Where(c => c.Visible && !_s.Locked.Contains(c) && rootRel.IntersectsWith(SKRect.Create(c.Location, c.Size)))
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
                }
                // Operates over the whole selection (not just a single item) like Duplicate/Delete/
                // Group above — "Unlock" only when every selected item is already locked, otherwise
                // "Lock" locks the rest too, matching the common multi-select toggle convention.
                var selectionItems = _s.Selection.Items;
                var allLocked = selectionItems.All(_s.Locked.Contains);
                menu.AddItem(new MenuItem(allLocked ? "Unlock" : "Lock", (_, _) =>
                {
                    foreach (var item in selectionItems)
                    {
                        if (allLocked) _s.Locked.Remove(item);
                        else _s.Locked.Add(item);
                    }
                    _s.StructureChanged?.Invoke();
                    Invalidate();
                }));
                if (_s.Selection.Count >= 2)
                {
                    menu.AddItem(new MenuItem("Align left", (_, _) => _s.Align(AlignKind.Left)));
                    menu.AddItem(new MenuItem("Align top", (_, _) => _s.Align(AlignKind.Top)));
                    menu.AddItem(new MenuItem("Align centers", (_, _) => { _s.Align(AlignKind.CenterH); _s.Align(AlignKind.CenterV); }));
                    menu.AddItem(new MenuItem("Group Selection", (_, _) => _s.GroupSelection()) { ShortcutKeys = Keys.Control | Keys.G });
                }
                if (_s.Selection.Count >= 3)
                {
                    menu.AddItem(new MenuItem("Distribute horizontally", (_, _) => _s.Distribute(true)));
                    menu.AddItem(new MenuItem("Distribute vertically", (_, _) => _s.Distribute(false)));
                }
                if (single != null && _s.Groups.Contains(single))
                    menu.AddItem(new MenuItem("Ungroup", (_, _) => _s.Ungroup(single)) { ShortcutKeys = Keys.Control | Keys.Shift | Keys.G });
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

        /// <summary>Root-relative bounds of a designed control at any nesting depth.</summary>
        private SKRect ToOverlay(ElementBase designed)
        {
            var loc = _s.GetDesignSpaceLocation(designed);
            return SKRect.Create(Root.Location.X + loc.X, Root.Location.Y + loc.Y, designed.Width, designed.Height);
        }

        /// <summary>The deepest designed control under a point, at any nesting depth — clicking into
        /// an already-nested control selects it directly rather than only its top-level container.
        /// Shares <see cref="DesignSurface.FindNestingTargetAt"/>, the same "any control can host
        /// children" rule used for drag/drop nesting.</summary>
        private ElementBase? HitControl(SKPoint p) => _s.FindNestingTargetAt(p);

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
