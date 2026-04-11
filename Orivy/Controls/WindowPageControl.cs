using Orivy.Animation;
using Orivy.Extensions;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Orivy.Controls;

public class WindowPageControl : ElementBase
{
    private const float DefaultTabGap = 0f;
    private const float TabHorizontalPadding = 14f;
    private const float TabVerticalInset = 4f;
    private const float TabIndicatorHeight = 3f;
    private const float TabIconSize = 16f;
    private const float TabIconSpacing = 8f;
    private const float TabCloseButtonSize = 18f;
    private const float TabCloseButtonSpacing = 8f;
    private const float TabMinWidth = 130f;
    private const float TabMaxWidth = 240f;
    private const float NewTabButtonSize = 22f;
    private const float TabSelectionAnimationSpeed = 0.14f;
    private const float TabDragThreshold = 6f;
    private const float WindowChromeTabHorizontalPadding = 10f;
    private const float WindowChromeTabIconSize = 16f;
    private const float WindowChromeTabIconSpacing = 8f;
    private const float WindowChromeTabCloseButtonSize = 20f;
    private const float WindowChromeTabCloseButtonInset = 4.5f;
    private const float WindowChromeTabSelectionAnimationSpeed = 0.10f;
    private const float WindowChromeHoverAnimationSpeed = 0.10f;
    private const float TabFontSize = 9.5f;
    private const float WindowChromeTabFontSize = 8.5f;
    private const float WindowChromeTabFontSizeWithIcon = 9.25f;

    private readonly AnimationManager _transitionAnimation;
    private readonly AnimationManager _tabSelectionAnimation;
    private readonly AnimationManager _windowChromeTabSelectionAnimation;
    private readonly AnimationManager _windowChromeTabCloseHoverAnimation;
    private readonly AnimationManager _windowChromeNewTabHoverAnimation;
    private readonly object _transitionSnapshotSync = new();
    private readonly List<SKRect> _tabCloseButtonRects = new();
    private readonly List<SKRect> _tabRects = new();
    private readonly List<float> _tabWidthBuffer = new();
    private readonly List<SKRect> _windowChromeTabRects = new();
    private readonly List<float> _windowChromeTabWidthBuffer = new();
    private readonly SKPaint _tabBackgroundPaint;
    private readonly SKPaint _tabBorderPaint;
    private readonly SKPaint _tabIndicatorPaint;
    private readonly SKPaint _tabTextPaint;
    private readonly SKPath _tabChromePath;
    private readonly SKFont _tabFont;
    private EventHandler? _onNewTabButtonClick;
    private EventHandler<int>? _onSelectedIndexChanged;
    private EventHandler<int>? _onTabCloseButtonClick;
    private bool _drawTabIcons;
    private SKRect _newTabButtonRect = SKRect.Empty;
    private int _previousSelectedIndex = -1;
    private int _hoveredTabCloseIndex = -1;
    private bool _hoveredNewTabButton;
    private SKRect _windowChromeCloseButtonRect = SKRect.Empty;
    private SKRect _windowChromeNewTabButtonRect = SKRect.Empty;
    private int _windowChromePreviousSelectedIndex = -1;
    private int _hoveredWindowChromeTabIndex = -1;
    private bool _hoveredWindowChromeCloseButton;
    private bool _hoveredWindowChromeNewTabButton;
    private WindowPageChromeLayoutContext _lastWindowChromeLayoutContext;
    private bool _hasWindowChromeLayoutContext;
    private int _windowChromeLayoutPageCount = -1;
    private float _tabStripHeight = 44f;
    private int _hoveredTabIndex = -1;
    private bool _isTransitionDirty;
    private bool _newTabButton;
    private int _selectedIndex = -1;
    private bool _tabCloseButton;
    private float _tabGap = DefaultTabGap;
    private WindowPageTabDesignMode _tabDesignMode = WindowPageTabDesignMode.Rectangle;
    private WindowPageTabAlignment _tabAlignment = WindowPageTabAlignment.Start;
    private WindowPageTabMode _tabMode = WindowPageTabMode.WindowChrome;
    private SKColor _tabStripBackground = SKColors.Transparent;
    private bool _allowTabDrag = true;
    private List<ElementBase>? _pageOrder;
    private int _dragTabSourceIndex = -1;
    private bool _isDraggingTab;
    private float _dragTabGrabX;
    private float _dragTabCurrentX;
    private int _dragTabInsertIndex = -1;
    private float[] _tabDodgeAnimOffsets = Array.Empty<float>();
    private int _transitionFinalizationPending;
    private int _transitionFromIndex = -1;
    private int _transitionToIndex = -1;
    private SKImage? _transitionFromSnapshot;
    private SKImage? _transitionToSnapshot;
    private readonly SKPaint _transitionPaint;

    public override SKColor BackColor
    {
        get => SKColors.Transparent;
        set { }
    }

    public override SKRect DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            if (!ShouldDrawTabStrip)
                return rect;

            var headerHeight = Math.Min(rect.Height, GetTabHeaderHeight());
            var contentTop = Math.Min(rect.Bottom, rect.Top + headerHeight);
            return new SKRect(rect.Left, contentTop, rect.Right, rect.Bottom);
        }
    }

    public WindowPageControl()
    {
        _tabBackgroundPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _tabBorderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f
        };
        _tabIndicatorPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _tabTextPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        _tabChromePath = new SKPath();
        _tabFont = new SKFont();

        _tabSelectionAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = TabSelectionAnimationSpeed,
            SecondaryIncrement = TabSelectionAnimationSpeed,
            AnimationType = AnimationType.CubicEaseOut
        };
        _tabSelectionAnimation.OnAnimationProgress += HandleTabSelectionProgress;
        _tabSelectionAnimation.OnAnimationFinished += HandleTabSelectionFinished;

        _windowChromeTabSelectionAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = WindowChromeTabSelectionAnimationSpeed,
            SecondaryIncrement = WindowChromeTabSelectionAnimationSpeed,
            AnimationType = AnimationType.CubicEaseOut
        };
        _windowChromeTabSelectionAnimation.OnAnimationProgress += HandleWindowChromeSelectionProgress;
        _windowChromeTabSelectionAnimation.OnAnimationFinished += HandleWindowChromeSelectionFinished;

        _windowChromeTabCloseHoverAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = WindowChromeHoverAnimationSpeed,
            SecondaryIncrement = WindowChromeHoverAnimationSpeed,
            AnimationType = AnimationType.EaseInOut
        };
        _windowChromeTabCloseHoverAnimation.OnAnimationProgress += HandleWindowChromeHoverProgress;

        _windowChromeNewTabHoverAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = WindowChromeHoverAnimationSpeed,
            SecondaryIncrement = WindowChromeHoverAnimationSpeed,
            AnimationType = AnimationType.EaseInOut
        };
        _windowChromeNewTabHoverAnimation.OnAnimationProgress += HandleWindowChromeHoverProgress;

        _transitionPaint = new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.SrcOver
        };

        _transitionAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = 0.22,
            SecondaryIncrement = 0.22,
            AnimationType = AnimationType.CubicEaseOut
        };
        _transitionAnimation.OnAnimationProgress += HandleTransitionProgress;
        _transitionAnimation.OnAnimationFinished += HandleTransitionFinished;
    }

    [Category("Behavior")]
    [DefaultValue(WindowPageTabMode.WindowChrome)]
    public WindowPageTabMode TabMode
    {
        get => _tabMode;
        set
        {
            if (_tabMode == value)
                return;

            _tabMode = value;
            _hoveredTabIndex = -1;
            ResetTabSelectionAnimation();
            ResetWindowChromeState();

            CancelTransitionPreservingSelection();
            PerformLayout();
            InvalidateRenderTree();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(44f)]
    public float TabStripHeight
    {
        get => _tabStripHeight;
        set
        {
            var clamped = Math.Max(32f, value);
            if (Math.Abs(_tabStripHeight - clamped) < 0.001f)
                return;

            _tabStripHeight = clamped;

            CancelTransitionPreservingSelection();
            PerformLayout();
            InvalidateRenderTree();
            Invalidate();
        }
    }

    [Browsable(false)]
    public bool UsesTabStrip => TabMode == WindowPageTabMode.Embedded;

    [Category("Layout")]
    [Description("Sets space between tabs.")]
    [DefaultValue(0f)]
    public float TabGap
    {
        get => _tabGap;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_tabGap - normalized) < 0.001f)
                return;

            _tabGap = normalized;
            InvalidateTabChrome();
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool DrawTabIcons
    {
        get => _drawTabIcons;
        set
        {
            if (_drawTabIcons == value)
                return;

            _drawTabIcons = value;
            InvalidateTabChrome();
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool TabCloseButton
    {
        get => _tabCloseButton;
        set
        {
            if (_tabCloseButton == value)
                return;

            _tabCloseButton = value;
            InvalidateTabChrome();
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool NewTabButton
    {
        get => _newTabButton;
        set
        {
            if (_newTabButton == value)
                return;

            _newTabButton = value;
            InvalidateTabChrome();
        }
    }

    [Category("Appearance")]
    [DefaultValue(WindowPageTabDesignMode.Rectangle)]
    public WindowPageTabDesignMode TabDesignMode
    {
        get => _tabDesignMode;
        set
        {
            if (_tabDesignMode == value)
                return;

            _tabDesignMode = value;
            InvalidateTabChrome();
        }
    }

    [Category("Appearance")]
    [Description("Background color of the embedded tab strip area. Overrides the design-mode default when not Transparent.")]
    public SKColor TabStripBackground
    {
        get => _tabStripBackground;
        set
        {
            _tabStripBackground = value;
            InvalidateTabChrome();
        }
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Allows the user to reorder tabs by dragging within the embedded tab strip.")]
    public bool AllowTabDrag
    {
        get => _allowTabDrag;
        set => _allowTabDrag = value;
    }

    [Category("Appearance")]
    [DefaultValue(WindowPageTabAlignment.Start)]
    [Description("Controls horizontal alignment of tabs within the tab strip. Applies to Embedded mode only.")]
    public WindowPageTabAlignment TabAlignment
    {
        get => _tabAlignment;
        set
        {
            if (_tabAlignment == value)
                return;

            _tabAlignment = value;
            InvalidateTabChrome();
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WindowPageTabDesignMode WindowChromeTabDesignMode
    {
        get => TabDesignMode;
        set => TabDesignMode = value;
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool EnableTransitions { get; set; } = true;

    [Category("Behavior")]
    [DefaultValue(WindowPageTransitionEffect.SlideHorizontal)]
    public WindowPageTransitionEffect TransitionEffect { get; set; } = WindowPageTransitionEffect.SlideHorizontal;

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool LockInputDuringTransition { get; set; } = true;

    [Category("Behavior")]
    [DefaultValue(AnimationType.CubicEaseOut)]
    public AnimationType TransitionAnimationType
    {
        get => _transitionAnimation.AnimationType;
        set => _transitionAnimation.AnimationType = value;
    }

    [Category("Behavior")]
    [DefaultValue(0.18d)]
    public double TransitionIncrement
    {
        get => _transitionAnimation.Increment;
        set => _transitionAnimation.Increment = ValidateIncrement(value);
    }

    [Category("Behavior")]
    [DefaultValue(0.18d)]
    public double TransitionSecondaryIncrement
    {
        get => _transitionAnimation.SecondaryIncrement;
        set => _transitionAnimation.SecondaryIncrement = ValidateIncrement(value);
    }

    [Category("Behavior")]
    [DefaultValue(300)]
    public int TransitionDurationMs
    {
        get => (int)Math.Round(16.0 / _transitionAnimation.Increment);
        set
        {
            var clamped = Math.Max(50, Math.Min(5000, value));
            var inc = 16.0 / clamped;
            _transitionAnimation.Increment = inc;
            _transitionAnimation.SecondaryIncrement = inc;
        }
    }

    [Browsable(false)]
    public bool IsTransitioning => _transitionAnimation.IsAnimating() && HasTransitionSnapshots();

    [Browsable(false)]
    internal float ResolvedTabGap => _tabGap;

    private bool ShouldDrawTabStrip => TabMode == WindowPageTabMode.Embedded && Count > 0;
    private bool ShouldDrawTabIcons => ShouldDrawTabStrip && DrawTabIcons;
    private bool ShouldDrawTabCloseButtons => ShouldDrawTabStrip && TabCloseButton;
    private bool ShouldDrawNewTabButton => ShouldDrawTabStrip && NewTabButton;

    private bool IsPageControl(ElementBase element)
    {
        return element is Container;
    }

    private int GetPageCount()
    {
        var count = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is ElementBase element && IsPageControl(element))
                count++;
        }

        return count;
    }

    public ElementBase? GetPageAt(int pageIndex)
    {
        if (pageIndex < 0)
            return null;

        if (_pageOrder != null)
            return pageIndex < _pageOrder.Count ? _pageOrder[pageIndex] : null;

        var currentPageIndex = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase element || !IsPageControl(element))
                continue;

            if (currentPageIndex == pageIndex)
                return element;

            currentPageIndex++;
        }

        return null;
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var sys = Stopwatch.StartNew();

            if (_selectedIndex == value)
                return;

            var pageCount = GetPageCount();
            if (pageCount > 0)
            {
                if (value < 0)
                    value = pageCount - 1;

                if (value > pageCount - 1)
                    value = 0;
            }
            else
            {
                value = -1;
            }

            var previousSelectedIndex = _selectedIndex;
            _selectedIndex = value;
            StartTabSelectionAnimation(previousSelectedIndex, _selectedIndex);
            StartWindowChromeSelectionAnimation(previousSelectedIndex, _selectedIndex);

            var transitionStarted = TryStartTransition(previousSelectedIndex, _selectedIndex);
            if (!transitionStarted)
                CommitSelectedPageVisibility();

            _onSelectedIndexChanged?.Invoke(this, previousSelectedIndex);

            InvalidateRenderTree();
            Invalidate();

            Debug.WriteLine($"Index: {_selectedIndex} Finished: {sys.ElapsedMilliseconds} ms");
        }
    }

    public int Count => GetPageCount();

    public event EventHandler<int> SelectedIndexChanged
    {
        add => _onSelectedIndexChanged += value;
        remove => _onSelectedIndexChanged -= value;
    }

    public event EventHandler<int> TabCloseButtonClick
    {
        add => _onTabCloseButtonClick += value;
        remove => _onTabCloseButtonClick -= value;
    }

    public event EventHandler NewTabButtonClick
    {
        add => _onNewTabButtonClick += value;
        remove => _onNewTabButtonClick -= value;
    }

    public void StopTransition(bool commitTargetPage = true)
    {
        ReleaseTransitionSnapshots();
        _transitionAnimation.SetProgress(commitTargetPage ? 1 : 0);
        _transitionFromIndex = -1;
        _transitionToIndex = -1;
        _isTransitionDirty = false;

        if (commitTargetPage)
            CommitSelectedPageVisibility();

        InvalidateRenderTree();
        Invalidate();
    }

    internal void RaiseTabCloseButtonClick(int tabIndex)
    {
        _onTabCloseButtonClick?.Invoke(this, tabIndex);
    }

    internal void RaiseNewTabButtonClick()
    {
        _onNewTabButtonClick?.Invoke(this, EventArgs.Empty);
    }

    internal override void OnControlAdded(ElementEventArgs e)
    {
        base.OnControlAdded(e);

        if (e.Element is not ElementBase element || !IsPageControl(element))
            return;

        element.Dock = DockStyle.Fill;
        SyncPageBounds(element);
        element.Visible = Count == 1;

        if (Count == 1)
            _selectedIndex = 0;
        else if (element.Visible)
            CommitSelectedPageVisibility();

        if (_pageOrder != null)
            _pageOrder.Add(element);

        ResetTabSelectionAnimation();

        CancelTransitionPreservingSelection();
    }

    internal override void OnControlRemoved(ElementEventArgs e)
    {
        base.OnControlRemoved(e);

        if (e.Element is not ElementBase element || !IsPageControl(element))
            return;

        if (_pageOrder != null)
            _pageOrder.Remove(element);

        if (Count == 0)
            _selectedIndex = -1;
        else if (_selectedIndex >= Count)
            SelectedIndex = Count - 1;

        ResetTabSelectionAnimation();

        CancelTransitionPreservingSelection();
    }

    internal override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible && IsTransitioning)
            StopTransition();

        if (!Visible)
            ResetWindowChromeHoverState();
    }

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        SyncAllPageBounds();
        InvalidateWindowChromeLayout();

        if (IsTransitioning)
            _isTransitionDirty = true;
    }

    protected override bool ShouldIncludeHitTestElement(ElementBase element, bool requireEnabled)
    {
        if (!base.ShouldIncludeHitTestElement(element, requireEnabled))
            return false;

        if (!IsPageControl(element) || !IsTransitioning || !LockInputDuringTransition)
            return true;

        var targetPage = GetPageAt(_transitionToIndex);
        return ReferenceEquals(targetPage, element);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        FinalizeCompletedTransitionIfPending();
        base.OnPaint(canvas);

        if (ShouldDrawTabStrip)
            DrawTabStrip(canvas);
    }

    internal override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && ShouldDrawTabStrip)
        {
            if (!TryGetTabCloseButtonIndexAtPoint(e.Location, out _) &&
                !IsPointOverNewTabButton(e.Location) &&
                TryGetTabIndexAtPoint(e.Location, out var tabIndex))
            {
                SelectedIndex = tabIndex;
                if (_allowTabDrag)
                {
                    _dragTabSourceIndex = tabIndex;
                    _dragTabGrabX = tabIndex < _tabRects.Count ? e.Location.X - _tabRects[tabIndex].Left : 0f;
                    _dragTabCurrentX = e.Location.X;
                }
            }
        }

        base.OnMouseDown(e);
    }

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        if (!ShouldDrawTabStrip)
            return;

        if (e.Button == MouseButtons.Left)
        {
            if (TryGetTabCloseButtonIndexAtPoint(e.Location, out var closeTabIndex))
            {
                RaiseTabCloseButtonClick(closeTabIndex);
                return;
            }

            if (IsPointOverNewTabButton(e.Location))
            {
                RaiseNewTabButtonClick();
                return;
            }
        }

        if (e.Button == MouseButtons.Middle && ShouldDrawTabCloseButtons && TryGetTabIndexAtPoint(e.Location, out var middleClickTabIndex))
            RaiseTabCloseButtonClick(middleClickTabIndex);
    }

    internal override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!ShouldDrawTabStrip)
        {
            if (_hoveredTabIndex >= 0 || _hoveredTabCloseIndex >= 0 || _hoveredNewTabButton)
            {
                _hoveredTabIndex = -1;
                _hoveredTabCloseIndex = -1;
                _hoveredNewTabButton = false;
                Invalidate();
            }

            return;
        }

        if (_dragTabSourceIndex >= 0)
        {
            var grabOriginX = _dragTabSourceIndex < _tabRects.Count
                ? _tabRects[_dragTabSourceIndex].Left + _dragTabGrabX
                : _dragTabCurrentX;

            if (!_isDraggingTab && Math.Abs(e.Location.X - grabOriginX) > TabDragThreshold * ScaleFactor)
                _isDraggingTab = true;

            if (_isDraggingTab)
            {
                _dragTabCurrentX = e.Location.X;
                _dragTabInsertIndex = ComputeDragInsertIndex(e.Location.X);
                Invalidate();
                return;
            }
        }

        var hoveredTabIndex = TryGetTabIndexAtPoint(e.Location, out var tabIndex) ? tabIndex : -1;
        var hoveredCloseTabIndex = TryGetTabCloseButtonIndexAtPoint(e.Location, out var closeTabIndex) ? closeTabIndex : -1;
        var hoveredNewTabButton = IsPointOverNewTabButton(e.Location);
        if (_hoveredTabIndex == hoveredTabIndex &&
            _hoveredTabCloseIndex == hoveredCloseTabIndex &&
            _hoveredNewTabButton == hoveredNewTabButton)
            return;

        _hoveredTabIndex = hoveredTabIndex;
        _hoveredTabCloseIndex = hoveredCloseTabIndex;
        _hoveredNewTabButton = hoveredNewTabButton;
        Invalidate();
    }

    internal override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        var invalidate = false;

        if (_isDraggingTab)
        {
            _isDraggingTab = false;
            _dragTabSourceIndex = -1;
            _dragTabInsertIndex = -1;
            _tabDodgeAnimOffsets = Array.Empty<float>();
            invalidate = true;
        }

        if (_hoveredTabIndex >= 0 || _hoveredTabCloseIndex >= 0 || _hoveredNewTabButton)
        {
            _hoveredTabIndex = -1;
            _hoveredTabCloseIndex = -1;
            _hoveredNewTabButton = false;
            invalidate = true;
        }

        if (invalidate)
            Invalidate();
    }

    protected override bool TryRenderChildContent(SKCanvas canvas)
    {
        FinalizeCompletedTransitionIfPending();

        if (!IsTransitioning)
            return false;

        if (!EnsureTransitionSnapshots())
            return false;

        var viewport = GetTransitionViewport();
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;

        var progress = Math.Clamp((float)_transitionAnimation.GetProgress(), 0f, 1f);

        lock (_transitionSnapshotSync)
        {
            var fromSnapshot = _transitionFromSnapshot;
            var toSnapshot = _transitionToSnapshot;
            if (fromSnapshot == null || toSnapshot == null)
                return false;

            var saved = canvas.Save();
            canvas.ClipRect(viewport);
            DrawTransitionEffect(canvas, fromSnapshot, toSnapshot, viewport, progress);
            canvas.RestoreToCount(saved);
            return true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _windowChromeTabSelectionAnimation.OnAnimationProgress -= HandleWindowChromeSelectionProgress;
            _windowChromeTabSelectionAnimation.OnAnimationFinished -= HandleWindowChromeSelectionFinished;
            _windowChromeTabSelectionAnimation.Dispose();
            _windowChromeTabCloseHoverAnimation.OnAnimationProgress -= HandleWindowChromeHoverProgress;
            _windowChromeTabCloseHoverAnimation.Dispose();
            _windowChromeNewTabHoverAnimation.OnAnimationProgress -= HandleWindowChromeHoverProgress;
            _windowChromeNewTabHoverAnimation.Dispose();
            _tabSelectionAnimation.OnAnimationProgress -= HandleTabSelectionProgress;
            _tabSelectionAnimation.OnAnimationFinished -= HandleTabSelectionFinished;
            _tabSelectionAnimation.Dispose();
            _transitionAnimation.OnAnimationProgress -= HandleTransitionProgress;
            _transitionAnimation.OnAnimationFinished -= HandleTransitionFinished;
            _transitionAnimation.Dispose();
            _tabBackgroundPaint.Dispose();
            _tabBorderPaint.Dispose();
            _tabIndicatorPaint.Dispose();
            _tabTextPaint.Dispose();
            _tabChromePath.Dispose();
            _tabFont.Dispose();
            _transitionPaint.Dispose();
            ReleaseTransitionSnapshots();
        }

        base.Dispose(disposing);
    }

    private void HandleTransitionProgress(object _)
    {
        Invalidate();
    }

    private void HandleTabSelectionProgress(object _)
    {
        Invalidate();
    }

    private void HandleWindowChromeSelectionProgress(object _)
    {
        Invalidate();
    }

    private void HandleWindowChromeSelectionFinished(object _)
    {
        _windowChromePreviousSelectedIndex = _selectedIndex;
        UpdateWindowChromeAuxiliaryRects();
        Invalidate();
    }

    private void HandleWindowChromeHoverProgress(object _)
    {
        Invalidate();
    }

    private void HandleTabSelectionFinished(object _)
    {
        _previousSelectedIndex = _selectedIndex;
        Invalidate();
    }

    private void HandleTransitionFinished(object _)
    {
        Interlocked.Exchange(ref _transitionFinalizationPending, 1);
        Invalidate();
    }

    private bool TryStartTransition(int previousSelectedIndex, int nextSelectedIndex)
    {
        FinalizeCompletedTransitionIfPending();

        var carryForwardSnapshot = IsTransitioning ? CaptureActiveTransitionSnapshot() : null;

        if (!ShouldAnimateTransition(previousSelectedIndex, nextSelectedIndex))
        {
            carryForwardSnapshot?.Dispose();
            return false;
        }

        SyncAllPageBounds();

        _transitionFromIndex = previousSelectedIndex;
        _transitionToIndex = nextSelectedIndex;
        _isTransitionDirty = false;

        if (!RebuildTransitionSnapshots(carryForwardSnapshot))
        {
            carryForwardSnapshot?.Dispose();
            ReleaseTransitionSnapshots();
            _transitionFromIndex = -1;
            _transitionToIndex = -1;
            return false;
        }

        CommitTransitionVisibilityState();

        _transitionAnimation.SetProgress(0);
        _transitionAnimation.StartNewAnimation(AnimationDirection.In);
        return true;
    }

    private bool ShouldAnimateTransition(int previousSelectedIndex, int nextSelectedIndex)
    {
        if (!EnableTransitions || TransitionEffect == WindowPageTransitionEffect.None)
            return false;

        if (previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
            return false;

        var fromPage = GetPageAt(previousSelectedIndex);
        var toPage = GetPageAt(nextSelectedIndex);
        if (fromPage == null || toPage == null)
            return false;

        var viewport = GetTransitionViewport();
        return viewport.Width > 0 && viewport.Height > 0;
    }

    private void CommitSelectedPageVisibility()
    {
        var selectedPage = GetPageAt(_selectedIndex);
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase element || !IsPageControl(element))
                continue;

            SyncPageBounds(element);
            element.Visible = ReferenceEquals(element, selectedPage);
        }
    }

    private void CommitTransitionVisibilityState()
    {
        var targetPage = GetPageAt(_transitionToIndex);

        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            SyncPageBounds(page);
            page.Visible = ReferenceEquals(page, targetPage);
        }
    }

    private void CancelTransitionPreservingSelection()
    {
        Interlocked.Exchange(ref _transitionFinalizationPending, 0);
        ReleaseTransitionSnapshots();
        _transitionFromIndex = -1;
        _transitionToIndex = -1;
        _isTransitionDirty = false;
        CommitSelectedPageVisibility();
        InvalidateRenderTree();
        Invalidate();
    }

    private bool EnsureTransitionSnapshots()
    {
        if (_isTransitionDirty)
            return RebuildTransitionSnapshots();

        return HasTransitionSnapshots();
    }

    private bool HasTransitionSnapshots()
    {
        lock (_transitionSnapshotSync)
            return _transitionFromSnapshot != null && _transitionToSnapshot != null;
    }

    private bool RebuildTransitionSnapshots(SKImage? fromSnapshotOverride = null)
    {
        ReleaseTransitionSnapshots();

        var fromPage = GetPageAt(_transitionFromIndex);
        var toPage = GetPageAt(_transitionToIndex);
        if (fromPage == null || toPage == null)
            return false;

        SyncPageBounds(fromPage);
        SyncPageBounds(toPage);

        var fromSnapshot = fromSnapshotOverride ?? CapturePageSnapshot(fromPage);
        var toSnapshot = CapturePageSnapshot(toPage);
        if (fromSnapshot == null || toSnapshot == null)
        {
            fromSnapshot?.Dispose();
            toSnapshot?.Dispose();
            return false;
        }

        lock (_transitionSnapshotSync)
        {
            _transitionFromSnapshot = fromSnapshot;
            _transitionToSnapshot = toSnapshot;
        }

        _isTransitionDirty = false;
        return true;
    }

    private SKImage? CaptureActiveTransitionSnapshot()
    {
        var viewport = GetTransitionViewport();
        var width = Math.Max(1, (int)Math.Ceiling(viewport.Width));
        var height = Math.Max(1, (int)Math.Ceiling(viewport.Height));
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface == null)
            return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        lock (_transitionSnapshotSync)
        {
            var fromSnapshot = _transitionFromSnapshot;
            var toSnapshot = _transitionToSnapshot;
            if (fromSnapshot == null || toSnapshot == null)
                return null;

            DrawTransitionEffect(canvas, fromSnapshot, toSnapshot, SKRect.Create(0, 0, width, height),
                Math.Clamp((float)_transitionAnimation.GetProgress(), 0f, 1f));
        }

        surface.Flush();
        return surface.Snapshot();
    }

    private SKImage? CapturePageSnapshot(ElementBase page)
    {
        var bounds = page.Bounds;
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        if (width <= 0 || height <= 0)
            return null;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface == null)
            return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var originalVisible = page.Visible;
        var originalLocation = page.Location;
        var originalBounds = page.Bounds;

        page.Visible = true;
        page.Location = SKPoint.Empty;

        try
        {
            page.Render(canvas);
        }
        finally
        {
            page.Bounds = originalBounds;
            page.Location = originalLocation;
            page.Visible = originalVisible;
        }

        surface.Flush();
        return surface.Snapshot();
    }

    private void DrawTransitionEffect(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport,
        float progress)
    {
        switch (TransitionEffect)
        {
            case WindowPageTransitionEffect.Fade:
                DrawFade(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.SlideHorizontal:
                DrawSlideHorizontal(canvas, fromSnapshot, toSnapshot, viewport, progress, pushExistingPage: false);
                break;
            case WindowPageTransitionEffect.SlideVertical:
                DrawSlideVertical(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.ScaleFade:
                DrawScaleFade(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Push:
                DrawSlideHorizontal(canvas, fromSnapshot, toSnapshot, viewport, progress, pushExistingPage: true, drawJunctionShadow: true);
                break;
            case WindowPageTransitionEffect.Cover:
                DrawCover(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Reveal:
                DrawReveal(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Uncover:
                DrawUncover(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Flip:
                DrawFlip(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Iris:
                DrawIris(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Morph:
                DrawMorph(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Zoom:
                DrawZoom(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.CrossZoom:
                DrawCrossZoom(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Split:
                DrawSplit(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case WindowPageTransitionEffect.Wipe:
                DrawWipe(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            default:
                DrawSnapshot(canvas, toSnapshot, viewport, 255);
                break;
        }
    }

    private void DrawFade(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        DrawSnapshot(canvas, fromSnapshot, viewport, (byte)(255f * (1f - progress)));
        DrawSnapshot(canvas, toSnapshot, viewport, (byte)(255f * progress));
    }

    private void DrawSlideHorizontal(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport,
        float progress, bool pushExistingPage, bool drawJunctionShadow = false)
    {
        var direction = GetDirectionalSign();
        var offset = viewport.Width * progress * direction;

        var fromRect = viewport;
        fromRect.Offset(pushExistingPage ? -offset : 0, 0);

        var toRect = viewport;
        toRect.Offset((direction > 0 ? viewport.Width : -viewport.Width) - offset, 0);

        DrawSnapshot(canvas, fromSnapshot, fromRect, 255);
        DrawSnapshot(canvas, toSnapshot, toRect, 255);

        if (drawJunctionShadow)
        {
            var junctionX = direction > 0 ? fromRect.Right : fromRect.Left;
            const float shadowHalfWidth = 14f;
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(junctionX - shadowHalfWidth, 0f),
                new SKPoint(junctionX + shadowHalfWidth, 0f),
                new[] { SKColors.Transparent, SKColors.Black.WithAlpha(55), SKColors.Transparent },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp);
            using var shadowPaint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect(SKRect.Create(
                junctionX - shadowHalfWidth, viewport.Top, shadowHalfWidth * 2f, viewport.Height), shadowPaint);
        }
    }

    private void DrawSlideVertical(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport,
        float progress)
    {
        var direction = GetDirectionalSign();
        var offset = viewport.Height * progress * direction;

        var fromRect = viewport;
        fromRect.Offset(0, -offset);

        var toRect = viewport;
        toRect.Offset(0, (direction > 0 ? viewport.Height : -viewport.Height) - offset);

        DrawSnapshot(canvas, fromSnapshot, fromRect, 255);
        DrawSnapshot(canvas, toSnapshot, toRect, 255);

        // horizontal shadow line at the junction between pages
        var junctionY = direction > 0 ? fromRect.Bottom : fromRect.Top;
        const float shadowHalfHeight = 14f;
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0f, junctionY - shadowHalfHeight),
            new SKPoint(0f, junctionY + shadowHalfHeight),
            new[] { SKColors.Transparent, SKColors.Black.WithAlpha(55), SKColors.Transparent },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);
        using var shadowPaint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawRect(SKRect.Create(
            viewport.Left, junctionY - shadowHalfHeight, viewport.Width, shadowHalfHeight * 2f), shadowPaint);
    }

    private void DrawReveal(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // from: fades out and scales down slightly, conveying it is leaving
        var fromScale = 1f - 0.08f * progress;
        var fromW = viewport.Width * fromScale;
        var fromH = viewport.Height * fromScale;
        var fromRect = SKRect.Create(
            viewport.MidX - fromW / 2f,
            viewport.MidY - fromH / 2f,
            fromW, fromH);
        DrawSnapshot(canvas, fromSnapshot, fromRect, (byte)(255f * (1f - progress)));

        // to: scales up from 0.94 to 1.0 and fades in
        var toScale = 0.94f + 0.06f * progress;
        var toW = viewport.Width * toScale;
        var toH = viewport.Height * toScale;
        var toRect = SKRect.Create(
            viewport.MidX - toW / 2f,
            viewport.MidY - toH / 2f,
            toW, toH);
        DrawSnapshot(canvas, toSnapshot, toRect, (byte)(255f * progress));
    }

    private void DrawUncover(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // to: sits in place underneath, revealed as from slides away
        DrawSnapshot(canvas, toSnapshot, viewport, 255);

        // from: slides out in the transition direction
        var direction = GetDirectionalSign();
        var fromRect = viewport;
        fromRect.Offset(direction * viewport.Width * progress, 0f);
        DrawSnapshot(canvas, fromSnapshot, fromRect, 255);

        // shadow on the trailing (inward-facing) edge of the from page
        DrawLeadingEdgeShadow(canvas, fromRect, viewport, direction);
    }

    private void DrawFlip(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // Horizontal card flip via horizontal squash
        // 0→0.5: from squashes from full width to zero
        // 0.5→1: to expands from zero to full width
        // dark overlay peaks at the flip midpoint
        var darkness = 1f - Math.Abs(progress - 0.5f) * 2f;

        if (progress < 0.5f)
        {
            var t = progress * 2f;
            var w = Math.Max(0f, viewport.Width * (1f - t));
            var flipRect = SKRect.Create(viewport.MidX - w / 2f, viewport.Top, w, viewport.Height);
            DrawSnapshot(canvas, fromSnapshot, flipRect, 255);
        }
        else
        {
            var t = (progress - 0.5f) * 2f;
            var w = Math.Max(0f, viewport.Width * t);
            var flipRect = SKRect.Create(viewport.MidX - w / 2f, viewport.Top, w, viewport.Height);
            DrawSnapshot(canvas, toSnapshot, flipRect, 255);
        }

        if (darkness > 0.01f)
        {
            using var darkPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha((byte)(88 * darkness)),
                IsAntialias = true
            };
            canvas.DrawRect(viewport, darkPaint);
        }
    }

    private void DrawIris(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // from: fades slightly as the iris expands over it
        DrawSnapshot(canvas, fromSnapshot, viewport, (byte)(255f * (1f - progress * 0.35f)));

        if (progress <= 0f)
            return;

        var cx = viewport.MidX;
        var cy = viewport.MidY;
        var maxRadius = MathF.Sqrt(
            viewport.Width * viewport.Width + viewport.Height * viewport.Height) / 2f * 1.06f;
        var radius = maxRadius * progress;

        using var clipPath = new SKPath();
        clipPath.AddCircle(cx, cy, radius);

        var saved = canvas.Save();
        canvas.ClipPath(clipPath, antialias: true);
        DrawSnapshot(canvas, toSnapshot, viewport, 255);
        canvas.RestoreToCount(saved);
    }

    private void DrawScaleFade(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport,
        float progress)
    {
        DrawSnapshot(canvas, fromSnapshot, viewport, (byte)(255f * (1f - progress)));

        var scale = 0.92f + (0.08f * progress);
        var scaledWidth = viewport.Width * scale;
        var scaledHeight = viewport.Height * scale;
        var scaledRect = SKRect.Create(
            viewport.MidX - scaledWidth / 2f,
            viewport.MidY - scaledHeight / 2f,
            scaledWidth,
            scaledHeight);

        DrawSnapshot(canvas, toSnapshot, scaledRect, (byte)(255f * progress));
    }

    private void DrawCover(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // from: stays in place, scales down slightly to convey it is being covered
        var fromScale = 1f - 0.05f * progress;
        var sw = viewport.Width * fromScale;
        var sh = viewport.Height * fromScale;
        var fromRect = SKRect.Create(
            viewport.MidX - sw / 2f,
            viewport.MidY - sh / 2f,
            sw, sh);
        DrawSnapshot(canvas, fromSnapshot, fromRect, (byte)(255f * (1f - progress * 0.3f)));

        // to: slides in from the side, fully opaque, covering the from page
        var direction = GetDirectionalSign();
        var toRect = viewport;
        toRect.Offset((direction > 0 ? viewport.Width : -viewport.Width) * (1f - progress), 0f);
        DrawSnapshot(canvas, toSnapshot, toRect, 255);

        // leading-edge shadow on the incoming page to convey depth
        DrawLeadingEdgeShadow(canvas, toRect, viewport, direction);
    }

    private static void DrawLeadingEdgeShadow(SKCanvas canvas, SKRect pageRect, SKRect clip, int direction)
    {
        const float shadowWidth = 22f;
        float shadowLeft;
        SKPoint gradStart, gradEnd;

        if (direction > 0)
        {
            shadowLeft = pageRect.Left;
            gradStart = new SKPoint(shadowLeft, 0f);
            gradEnd = new SKPoint(shadowLeft + shadowWidth, 0f);
        }
        else
        {
            shadowLeft = pageRect.Right - shadowWidth;
            gradStart = new SKPoint(pageRect.Right, 0f);
            gradEnd = new SKPoint(pageRect.Right - shadowWidth, 0f);
        }

        var shadowRect = SKRect.Create(shadowLeft, clip.Top, shadowWidth, clip.Height);
        if (shadowRect.Right <= clip.Left || shadowRect.Left >= clip.Right)
            return;

        using var shader = SKShader.CreateLinearGradient(
            gradStart, gradEnd,
            new[] { SKColors.Black.WithAlpha(75), SKColors.Transparent },
            null,
            SKShaderTileMode.Clamp);
        using var shadowPaint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawRect(shadowRect, shadowPaint);
    }

    private void DrawSnapshot(SKCanvas canvas, SKImage snapshot, SKRect destinationRect, byte alpha)
    {
        if (snapshot == null || snapshot.Width <= 0 || snapshot.Height <= 0 || destinationRect.Width <= 0 || destinationRect.Height <= 0)
            return;

        _transitionPaint.Color = SKColors.White.WithAlpha(alpha);
        canvas.DrawImage(snapshot, destinationRect, _transitionPaint);
    }

    private void DrawMorph(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // Both pages cross-fade while simultaneously counter-scaling:
        // from shrinks 1.0 → 0.96, to grows 1.04 → 1.0, giving a soft dissolve-morph feel.
        var fromScale = 1f - 0.04f * progress;
        var toScale   = 1.04f - 0.04f * progress;

        var fw = viewport.Width * fromScale;
        var fh = viewport.Height * fromScale;
        var fromRect = SKRect.Create(viewport.MidX - fw / 2f, viewport.MidY - fh / 2f, fw, fh);

        var tw = viewport.Width * toScale;
        var th = viewport.Height * toScale;
        var toRect = SKRect.Create(viewport.MidX - tw / 2f, viewport.MidY - th / 2f, tw, th);

        DrawSnapshot(canvas, fromSnapshot, fromRect, (byte)(255f * (1f - progress)));
        DrawSnapshot(canvas, toSnapshot,   toRect,   (byte)(255f * progress));
    }

    private void DrawZoom(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // FROM stays at full size and simply fades out.
        DrawSnapshot(canvas, fromSnapshot, viewport, (byte)(255f * (1f - progress)));

        // TO zooms in from a tiny point (0.05x) to full size with ease-out curve.
        // The alpha ramps in quickly (sqrt curve) so the zoom reads as a punch-in.
        var eased = 1f - (1f - progress) * (1f - progress);
        var scale = 0.05f + 0.95f * eased;
        var tw = viewport.Width  * scale;
        var th = viewport.Height * scale;
        var toRect = SKRect.Create(viewport.MidX - tw / 2f, viewport.MidY - th / 2f, tw, th);
        var toAlpha = (byte)(255f * Math.Min(1f, progress * 2f));
        DrawSnapshot(canvas, toSnapshot, toRect, toAlpha);
    }

    private void DrawCrossZoom(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // Two-phase transition with a brief mid-point gap (like a lens blink):
        //   0.0 → 0.55 : FROM shrinks (1.0 → 0.6) and fades completely out.
        //   0.45 → 1.0 : TO grows (0.6 → 1.0) and fades fully in.
        // The overlap zone (0.45–0.55) lets both briefly coexist at the crossover.
        var fromAlpha = (byte)(255f * Math.Max(0f, 1f - progress / 0.55f));
        var fromScale = 1f - 0.4f * (progress / 0.55f);
        fromScale = Math.Clamp(fromScale, 0.6f, 1f);
        var fw = viewport.Width  * fromScale;
        var fh = viewport.Height * fromScale;
        DrawSnapshot(canvas, fromSnapshot,
            SKRect.Create(viewport.MidX - fw / 2f, viewport.MidY - fh / 2f, fw, fh),
            fromAlpha);

        var toProgress = Math.Max(0f, (progress - 0.45f) / 0.55f);
        var toAlpha = (byte)(255f * toProgress);
        var toScale = 0.6f + 0.4f * toProgress;
        var tw = viewport.Width  * toScale;
        var th = viewport.Height * toScale;
        DrawSnapshot(canvas, toSnapshot,
            SKRect.Create(viewport.MidX - tw / 2f, viewport.MidY - th / 2f, tw, th),
            toAlpha);
    }

    private void DrawSplit(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // to fades in full-screen underneath.
        DrawSnapshot(canvas, toSnapshot, viewport, (byte)(255f * progress));

        if (progress >= 1f)
            return;

        var alpha  = (byte)(255f * (1f - progress));
        var offset = viewport.Width * 0.5f * progress;

        // Left half: the clip rect shrinks rightward together with the image edge,
        // so only the still-visible portion of the left half is shown.
        var saved = canvas.Save();
        canvas.ClipRect(new SKRect(viewport.Left, viewport.Top, viewport.MidX - offset, viewport.Bottom));
        canvas.Translate(-offset, 0f);
        DrawSnapshot(canvas, fromSnapshot, viewport, alpha);
        canvas.RestoreToCount(saved);

        // Right half: clip shrinks leftward symmetrically.
        saved = canvas.Save();
        canvas.ClipRect(new SKRect(viewport.MidX + offset, viewport.Top, viewport.Right, viewport.Bottom));
        canvas.Translate(offset, 0f);
        DrawSnapshot(canvas, fromSnapshot, viewport, alpha);
        canvas.RestoreToCount(saved);
    }

    private void DrawWipe(SKCanvas canvas, SKImage fromSnapshot, SKImage toSnapshot, SKRect viewport, float progress)
    {
        // Smooth ink-wash wipe: FROM fades out while TO is revealed by an eased clip.
        // A soft feathered edge (horizontal linear gradient mask) prevents the harsh
        // straight-line look and gives an organic, fluid feel.
        var eased = 1f - (1f - progress) * (1f - progress); // ease-out
        var revealRight = viewport.Left + viewport.Width * eased;
        var feather = Math.Min(viewport.Width * 0.12f, 60f * ScaleFactor);

        // FROM fades out entirely as the wipe progresses.
        DrawSnapshot(canvas, fromSnapshot, viewport, (byte)(255f * (1f - eased)));

        if (revealRight <= viewport.Left)
            return;

        // Clip TO to the revealed area.
        var saved = canvas.Save();
        canvas.ClipRect(new SKRect(viewport.Left, viewport.Top, revealRight, viewport.Bottom));
        DrawSnapshot(canvas, toSnapshot, viewport, 255);
        canvas.RestoreToCount(saved);

        // Soft feather: a narrow gradient patch blends the edge so it doesn't feel hard.
        var featherLeft = revealRight - feather;
        if (feather > 1f && featherLeft < revealRight && featherLeft >= viewport.Left)
        {
            using var shader = SKShader.CreateLinearGradient(
                new SKPoint(featherLeft, 0), new SKPoint(revealRight, 0),
                new[] { SKColors.Transparent, SKColors.Black.WithAlpha(30) },
                SKShaderTileMode.Clamp);
            using var featherPaint = new SKPaint { Shader = shader, BlendMode = SKBlendMode.DstOut, IsAntialias = true };
            canvas.DrawRect(new SKRect(featherLeft, viewport.Top, revealRight, viewport.Bottom), featherPaint);
        }
    }

    internal override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isDraggingTab && _dragTabSourceIndex >= 0 && _dragTabInsertIndex >= 0)
            CommitTabDrag(_dragTabSourceIndex, _dragTabInsertIndex);

        _isDraggingTab = false;
        _dragTabSourceIndex = -1;
        _dragTabInsertIndex = -1;
        _tabDodgeAnimOffsets = Array.Empty<float>();
        Invalidate();
    }

    private int ComputeDragInsertIndex(float mouseX)
    {
        for (var i = 0; i < _tabRects.Count; i++)
        {
            if (i == _dragTabSourceIndex)
                continue;

            if (mouseX < _tabRects[i].MidX)
                return i;
        }

        return _tabRects.Count;
    }

    private void EnsurePageOrder()
    {
        if (_pageOrder != null)
            return;

        _pageOrder = new List<ElementBase>();
        for (var i = 0; i < Controls.Count; i++)
            if (Controls[i] is ElementBase el && IsPageControl(el))
                _pageOrder.Add(el);
    }

    private void CommitTabDrag(int fromVisualIndex, int insertBeforeIndex)
    {
        if (fromVisualIndex == insertBeforeIndex || fromVisualIndex < 0)
            return;

        EnsurePageOrder();

        var count = _pageOrder!.Count;
        if (fromVisualIndex >= count)
            return;

        var destIndex = insertBeforeIndex > fromVisualIndex ? insertBeforeIndex - 1 : insertBeforeIndex;
        destIndex = Math.Clamp(destIndex, 0, count - 1);

        if (destIndex == fromVisualIndex)
            return;

        var page = _pageOrder[fromVisualIndex];
        _pageOrder.RemoveAt(fromVisualIndex);
        _pageOrder.Insert(destIndex, page);
        _selectedIndex = destIndex;
        ResetTabSelectionAnimation();
        CancelTransitionPreservingSelection();
    }

    private void DrawTabStrip(SKCanvas canvas)
    {
        UpdateTabRects();

        var headerRect = GetTabHeaderRect();
        if (headerRect.Width <= 0 || headerRect.Height <= 0)
            return;

        PrepareTabFont();

        var indicatorHeight = TabIndicatorHeight * ScaleFactor;
        var tabGap = ResolvedTabGap * ScaleFactor;
        var iconSize = TabIconSize * ScaleFactor;
        var iconSpacing = TabIconSpacing * ScaleFactor;
        var horizontalPadding = TabHorizontalPadding * ScaleFactor;
        var closeButtonSpacing = TabCloseButtonSpacing * ScaleFactor;
        var isDark = ColorScheme.IsDarkMode;

        // --- Unified, clean color palette per design mode ---
        SKColor headerBackground, headerBorderColor,
                inactiveBackground, hoverBackground, selectedBackground,
                inactiveBorderColor, selectedBorderColor,
                activeTextColor, inactiveTextColor;

        switch (TabDesignMode)
        {
            case WindowPageTabDesignMode.Rectangle:
                // Tailwind underline tabs: zero fill, bold primary indicator, subtle hover ghost
                headerBackground    = SKColors.Transparent;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)72 : (byte)52);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)12 : (byte)8);
                selectedBackground  = SKColors.Transparent;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = SKColors.Transparent;
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)165 : (byte)148) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.Rounded:
                // Segmented control / pill tabs: muted container, solid tinted fill on selected
                headerBackground    = ColorScheme.SurfaceContainerHigh;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)60 : (byte)44);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)14 : (byte)9);
                selectedBackground  = ColorScheme.Surface;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Outline.WithAlpha(isDark ? (byte)90 : (byte)68);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)162 : (byte)148) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.RoundedCompact:
                // bg-muted container, bg-background card on selected, crisp border
                headerBackground    = ColorScheme.SurfaceVariant;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)48 : (byte)36);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)10 : (byte)7);
                selectedBackground  = ColorScheme.Surface;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Outline.WithAlpha(isDark ? (byte)88 : (byte)68);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)158 : (byte)142) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.Pill:
                // GitHub/Vercel pill nav: filled Primary pill on selected, no container background
                headerBackground    = SKColors.Transparent;
                headerBorderColor   = SKColors.Transparent;
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ColorScheme.Primary.WithAlpha(isDark ? (byte)18 : (byte)13);
                selectedBackground  = ColorScheme.Primary;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = SKColors.Transparent;
                activeTextColor     = Enabled ? ColorScheme.Primary.Determine() : ColorScheme.Primary.Determine().WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)160 : (byte)144) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.Outlined:
                // Classic 3-sided tab: open bottom, selected sits on the bottom divider
                headerBackground    = SKColors.Transparent;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)72 : (byte)52);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)11 : (byte)7);
                selectedBackground  = ColorScheme.Surface;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Outline.WithAlpha(isDark ? (byte)96 : (byte)72);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)160 : (byte)144) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.Minimal:
                // Linear/Raycast sidebar: no chrome, Primary left-accent bar on selected
                headerBackground    = SKColors.Transparent;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)38 : (byte)28);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)9 : (byte)6);
                selectedBackground  = ColorScheme.Primary.WithAlpha(isDark ? (byte)12 : (byte)9);
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Primary;
                activeTextColor     = Enabled ? ColorScheme.Primary : ColorScheme.Primary.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)155 : (byte)138) : ForeColor.WithAlpha(110);
                break;

            case WindowPageTabDesignMode.Chromed:
            default:
                // Browser chrome tabs: surface strip, elevated selected card, divider line
                headerBackground    = ColorScheme.SurfaceContainer;
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)96 : (byte)70);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ForeColor.WithAlpha(isDark ? (byte)13 : (byte)9);
                selectedBackground  = ColorScheme.Surface;
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Outline.WithAlpha(isDark ? (byte)110 : (byte)82);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)168 : (byte)152) : ForeColor.WithAlpha(110);
                break;
        }
        var shouldAnimateSelection = _tabSelectionAnimation.IsAnimating() &&
                                     _previousSelectedIndex >= 0 &&
                                     _previousSelectedIndex < _tabRects.Count &&
                                     _selectedIndex >= 0 &&
                                     _selectedIndex < _tabRects.Count &&
                                     _previousSelectedIndex != _selectedIndex;
        var animatedSelectionRect = SKRect.Empty;

        if (_selectedIndex >= 0 && _selectedIndex < _tabRects.Count)
        {
            animatedSelectionRect = _tabRects[_selectedIndex];

            if (shouldAnimateSelection)
            {
                animatedSelectionRect = WindowPageTabGeometry.InterpolateRect(
                    _tabRects[_previousSelectedIndex],
                    _tabRects[_selectedIndex],
                    Math.Clamp((float)_tabSelectionAnimation.GetProgress(), 0f, 1f));
            }
        }

        DrawTabHeaderSurface(canvas, headerRect,
            _tabStripBackground == SKColors.Transparent ? headerBackground : _tabStripBackground,
            headerBorderColor);

        float ComputeDragTabTarget(int tIdx)
        {
            var srcSlotWidth = _tabRects[_dragTabSourceIndex].Width + tabGap;
            var adjIns = _dragTabInsertIndex > _dragTabSourceIndex ? _dragTabInsertIndex - 1 : _dragTabInsertIndex;
            var j = tIdx < _dragTabSourceIndex ? tIdx : tIdx - 1;
            return (tIdx > _dragTabSourceIndex ? -srcSlotWidth : 0f) + (j >= adjIns ? srcSlotWidth : 0f);
        }

        if (_isDraggingTab && _dragTabSourceIndex >= 0)
        {
            if (_tabDodgeAnimOffsets.Length != _tabRects.Count)
            {
                var prev = _tabDodgeAnimOffsets;
                _tabDodgeAnimOffsets = new float[_tabRects.Count];
                for (var k = 0; k < Math.Min(prev.Length, _tabDodgeAnimOffsets.Length); k++)
                    _tabDodgeAnimOffsets[k] = prev[k];
            }

            const float LerpFactor = 0.3f;
            const float SettleTolerance = 0.5f;
            var needsMoreFrames = false;

            for (var k = 0; k < _tabDodgeAnimOffsets.Length; k++)
            {
                if (k == _dragTabSourceIndex)
                {
                    _tabDodgeAnimOffsets[k] = 0f;
                    continue;
                }
                var target = _dragTabInsertIndex >= 0 ? ComputeDragTabTarget(k) : 0f;
                var delta = target - _tabDodgeAnimOffsets[k];
                if (MathF.Abs(delta) > SettleTolerance)
                {
                    _tabDodgeAnimOffsets[k] += delta * LerpFactor;
                    needsMoreFrames = true;
                }
                else
                {
                    _tabDodgeAnimOffsets[k] = target;
                }
            }

            if (needsMoreFrames)
                Invalidate();
        }
        else
        {
            _tabDodgeAnimOffsets = Array.Empty<float>();
        }

        for (var tabIndex = 0; tabIndex < _tabRects.Count; tabIndex++)
        {
            if (_isDraggingTab && tabIndex == _dragTabSourceIndex)
                continue;

            var rect = _tabRects[tabIndex];
            if (_isDraggingTab && tabIndex < _tabDodgeAnimOffsets.Length)
                rect = SKRect.Create(rect.Left + _tabDodgeAnimOffsets[tabIndex], rect.Top, rect.Width, rect.Height);

            var isSelected = tabIndex == _selectedIndex && !shouldAnimateSelection;
            var isHovered = tabIndex == _hoveredTabIndex;
            DrawTabBackground(canvas, rect, isSelected, isHovered,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight);
        }

        if (animatedSelectionRect.Width > 0f && shouldAnimateSelection)
        {
            DrawTabBackground(canvas, animatedSelectionRect, true, false,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight);
        }

        for (var tabIndex = 0; tabIndex < _tabRects.Count; tabIndex++)
        {
            if (_isDraggingTab && tabIndex == _dragTabSourceIndex)
                continue;

            var page = GetPageAt(tabIndex);
            if (page == null)
                continue;

            var rect = _tabRects[tabIndex];
            var isSelected = tabIndex == _selectedIndex;
            var iconRect = SKRect.Empty;
            var closeButtonRect = tabIndex < _tabCloseButtonRects.Count ? _tabCloseButtonRects[tabIndex] : SKRect.Empty;
            if (_isDraggingTab && tabIndex < _tabDodgeAnimOffsets.Length)
            {
                var shift = _tabDodgeAnimOffsets[tabIndex];
                rect = SKRect.Create(rect.Left + shift, rect.Top, rect.Width, rect.Height);
                if (closeButtonRect.Width > 0)
                    closeButtonRect = SKRect.Create(closeButtonRect.Left + shift, closeButtonRect.Top, closeButtonRect.Width, closeButtonRect.Height);
            }

            if (ShouldDrawTabIcons && page.Image != null)
            {
                iconRect = SKRect.Create(
                    rect.Left + horizontalPadding,
                    rect.MidY - iconSize / 2f,
                    iconSize,
                    iconSize);

                canvas.DrawImage(page.Image, iconRect);
            }

            if (closeButtonRect.Width > 0)
                DrawTabCloseButton(canvas, closeButtonRect, tabIndex == _hoveredTabCloseIndex, activeTextColor);

            var textRect = WindowPageTabGeometry.CreateTextRect(rect, horizontalPadding, iconRect, iconSpacing,
                closeButtonRect, closeButtonSpacing + (tabGap > 0f ? tabGap * 0.5f : 0f));

            _tabTextPaint.Color = isSelected ? activeTextColor : inactiveTextColor;
            TextRenderer.DrawText(canvas, page.Text ?? string.Empty, textRect, _tabTextPaint, _tabFont, TextAlign, true, false);
        }

        if (_newTabButtonRect.Width > 0)
            DrawNewTabButton(canvas, _newTabButtonRect, _hoveredNewTabButton, activeTextColor);

        if (_isDraggingTab && _dragTabSourceIndex >= 0 && _dragTabSourceIndex < _tabRects.Count)
        {
            var srcRect  = _tabRects[_dragTabSourceIndex];
            var ghostLeft = Math.Clamp(
                _dragTabCurrentX - _dragTabGrabX,
                headerRect.Left,
                headerRect.Right - srcRect.Width);
            var ghostRect = SKRect.Create(ghostLeft, srcRect.Top, srcRect.Width, srcRect.Height);

            using var ghostLayerPaint = new SKPaint { Color = new SKColor(255, 255, 255, 210) };
            var layerSaved = canvas.SaveLayer(ghostLayerPaint);

            DrawTabBackground(canvas, ghostRect, true, false,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight);

            var ghostPage = GetPageAt(_dragTabSourceIndex);
            if (ghostPage != null)
            {
                var ghostIconRect = SKRect.Empty;
                if (ShouldDrawTabIcons && ghostPage.Image != null)
                {
                    ghostIconRect = SKRect.Create(
                        ghostRect.Left + horizontalPadding,
                        ghostRect.MidY - iconSize / 2f,
                        iconSize, iconSize);
                    canvas.DrawImage(ghostPage.Image, ghostIconRect);
                }

                var textRect = WindowPageTabGeometry.CreateTextRect(ghostRect, horizontalPadding, ghostIconRect, iconSpacing, SKRect.Empty, 0f);
                _tabTextPaint.Color = activeTextColor;
                TextRenderer.DrawText(canvas, ghostPage.Text ?? string.Empty, textRect, _tabTextPaint, _tabFont, TextAlign, true, false);
            }

            canvas.RestoreToCount(layerSaved);
        }
    }

    internal void HandleWindowChromeSelectionChanged(int previousSelectedIndex)
    {
        StartWindowChromeSelectionAnimation(previousSelectedIndex, _selectedIndex);
    }

    internal void InvalidateWindowChromeLayout()
    {
        _hasWindowChromeLayoutContext = false;
        _windowChromeLayoutPageCount = -1;
        _windowChromeTabRects.Clear();
        _windowChromeTabWidthBuffer.Clear();
        _windowChromeCloseButtonRect = SKRect.Empty;
        _windowChromeNewTabButtonRect = SKRect.Empty;
    }

    internal void DrawWindowChromeTabs(SKCanvas canvas, WindowPageChromeLayoutContext context, SKColor foreColor, SKColor hoverColor, SKColor titleColor)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || Count <= 0)
            return;

        UpdateWindowChromeLayout(context);
        UpdateWindowChromeAuxiliaryRects();

        DrawWindowChromeTabDividers(canvas, context, titleColor);

        if (_selectedIndex < 0 || _selectedIndex >= _windowChromeTabRects.Count)
            return;

        var effectiveHoverColor = titleColor != SKColor.Empty && !titleColor.IsDark()
            ? foreColor.WithAlpha(60)
            : hoverColor;

        if (_hoveredWindowChromeTabIndex >= 0 && _hoveredWindowChromeTabIndex < _windowChromeTabRects.Count && _hoveredWindowChromeTabIndex != _selectedIndex)
            DrawWindowChromeTabSurface(canvas, _windowChromeTabRects[_hoveredWindowChromeTabIndex], false, true, effectiveHoverColor, foreColor, titleColor);

        DrawWindowChromeTabSurface(canvas, GetWindowChromeSelectedVisualRect(), true, false, effectiveHoverColor, foreColor, titleColor);
        PrepareTabFont((DrawTabIcons ? WindowChromeTabFontSizeWithIcon : WindowChromeTabFontSize).Topx(this));

        for (var pageIndex = 0; pageIndex < _windowChromeTabRects.Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var rect = _windowChromeTabRects[pageIndex];
            var iconRect = SKRect.Empty;
            var isSelected = pageIndex == _selectedIndex;
            var isHovered = pageIndex == _hoveredWindowChromeTabIndex;

            _tabTextPaint.Color = GetWindowChromeTextColor(isSelected, isHovered, foreColor);

            if (DrawTabIcons && page.Image != null)
            {
                var iconSize = WindowChromeTabIconSize * ScaleFactor;
                iconRect = SKRect.Create(
                    rect.Left + WindowChromeTabHorizontalPadding * ScaleFactor,
                    context.CenterY - iconSize / 2f,
                    iconSize,
                    iconSize);
                canvas.DrawImage(page.Image, iconRect);
            }

            var trailingRect = pageIndex == _selectedIndex ? _windowChromeCloseButtonRect : SKRect.Empty;
            var textRect = WindowPageTabGeometry.CreateTextRect(
                rect,
                WindowChromeTabHorizontalPadding * ScaleFactor,
                iconRect,
                WindowChromeTabIconSpacing * ScaleFactor,
                trailingRect,
                Math.Max(6f * ScaleFactor, ResolvedTabGap * ScaleFactor * 0.5f));

            TextRenderer.DrawText(canvas, page.Text ?? string.Empty, textRect, _tabTextPaint, _tabFont,
                TextAlign, true, false);
        }

        if (_windowChromeCloseButtonRect.Width > 0)
            DrawWindowChromeCloseButton(canvas, _windowChromeCloseButtonRect, _hoveredWindowChromeCloseButton, foreColor, effectiveHoverColor);

        if (_windowChromeNewTabButtonRect.Width > 0)
            DrawWindowChromeNewTabButton(canvas, _windowChromeNewTabButtonRect, _hoveredWindowChromeNewTabButton, foreColor, effectiveHoverColor);
    }

    internal bool TryGetWindowChromeTabIndexAtPoint(SKPoint point, WindowPageChromeLayoutContext context, out int tabIndex)
    {
        tabIndex = -1;

        if (TabMode != WindowPageTabMode.WindowChrome || Count <= 0)
            return false;

        UpdateWindowChromeLayout(context);
        for (var i = 0; i < _windowChromeTabRects.Count; i++)
        {
            if (_windowChromeTabRects[i].Contains(point))
            {
                tabIndex = i;
                return true;
            }
        }

        return false;
    }

    internal bool IsPointOverWindowChromeCloseButton(SKPoint point, WindowPageChromeLayoutContext context)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || !TabCloseButton)
            return false;

        UpdateWindowChromeLayout(context);
        UpdateWindowChromeAuxiliaryRects();
        return _windowChromeCloseButtonRect.Contains(point);
    }

    internal bool IsPointOverWindowChromeNewTabButton(SKPoint point, WindowPageChromeLayoutContext context)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || !NewTabButton)
            return false;

        UpdateWindowChromeLayout(context);
        UpdateWindowChromeAuxiliaryRects();
        return _windowChromeNewTabButtonRect.Contains(point);
    }

    internal bool UpdateWindowChromeHoverState(SKPoint point, WindowPageChromeLayoutContext context)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || Count <= 0)
            return ResetWindowChromeHoverState();

        UpdateWindowChromeLayout(context);
        UpdateWindowChromeAuxiliaryRects();

        var hoveredTabIndex = TryGetWindowChromeTabIndexAtPoint(point, context, out var tabIndex) ? tabIndex : -1;
        var hoveredCloseButton = _windowChromeCloseButtonRect.Contains(point);
        var hoveredNewTabButton = _windowChromeNewTabButtonRect.Contains(point);

        if (_hoveredWindowChromeTabIndex == hoveredTabIndex &&
            _hoveredWindowChromeCloseButton == hoveredCloseButton &&
            _hoveredWindowChromeNewTabButton == hoveredNewTabButton)
            return false;

        _hoveredWindowChromeTabIndex = hoveredTabIndex;

        if (_hoveredWindowChromeCloseButton != hoveredCloseButton)
        {
            _hoveredWindowChromeCloseButton = hoveredCloseButton;
            _windowChromeTabCloseHoverAnimation.StartNewAnimation(hoveredCloseButton ? AnimationDirection.In : AnimationDirection.Out);
        }

        if (_hoveredWindowChromeNewTabButton != hoveredNewTabButton)
        {
            _hoveredWindowChromeNewTabButton = hoveredNewTabButton;
            _windowChromeNewTabHoverAnimation.StartNewAnimation(hoveredNewTabButton ? AnimationDirection.In : AnimationDirection.Out);
        }

        Invalidate();
        return true;
    }

    internal bool ResetWindowChromeHoverState()
    {
        if (_hoveredWindowChromeTabIndex < 0 && !_hoveredWindowChromeCloseButton && !_hoveredWindowChromeNewTabButton)
            return false;

        _hoveredWindowChromeTabIndex = -1;

        if (_hoveredWindowChromeCloseButton)
        {
            _hoveredWindowChromeCloseButton = false;
            _windowChromeTabCloseHoverAnimation.StartNewAnimation(AnimationDirection.Out);
        }

        if (_hoveredWindowChromeNewTabButton)
        {
            _hoveredWindowChromeNewTabButton = false;
            _windowChromeNewTabHoverAnimation.StartNewAnimation(AnimationDirection.Out);
        }

        Invalidate();
        return true;
    }

    private void UpdateWindowChromeLayout(WindowPageChromeLayoutContext context)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || Count <= 0)
        {
            InvalidateWindowChromeLayout();
            return;
        }

        if (_hasWindowChromeLayoutContext && _lastWindowChromeLayoutContext == context && _windowChromeLayoutPageCount == Count)
            return;

        _windowChromeTabWidthBuffer.Clear();
        PrepareTabFont((DrawTabIcons ? WindowChromeTabFontSizeWithIcon : WindowChromeTabFontSize).Topx(this));

        var horizontalPadding = WindowChromeTabHorizontalPadding * ScaleFactor;
        var iconAllowance = (WindowChromeTabIconSize + WindowChromeTabIconSpacing) * ScaleFactor;
        var closeButtonAllowance = TabCloseButton ? (WindowChromeTabCloseButtonSize + WindowChromeTabIconSpacing) * ScaleFactor : 0f;
        var newTabButtonSize = 24f * ScaleFactor;
        var newTabButtonGap = Math.Max(ResolvedTabGap * ScaleFactor, newTabButtonSize / 2f);
        var availableWidth = Math.Max(0f, context.AvailableWidth - (NewTabButton ? newTabButtonSize + newTabButtonGap : 0f));
        var maxTabWidth = Math.Max(0f, context.MaxTabWidth);

        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var desiredWidth = WindowPageTabGeometry.MeasureDesiredTabWidth(
                page,
                _tabFont,
                horizontalPadding,
                iconAllowance,
                closeButtonAllowance,
                0f,
                maxTabWidth,
                DrawTabIcons,
                TabCloseButton);

            _windowChromeTabWidthBuffer.Add(desiredWidth);
        }

        WindowPageTabGeometry.LayoutTabs(
            _windowChromeTabWidthBuffer,
            context.StartX,
            context.Top,
            context.Height,
            availableWidth,
            ResolvedTabGap * ScaleFactor,
            maxTabWidth,
            true,
            _windowChromeTabRects);

        _lastWindowChromeLayoutContext = context;
        _hasWindowChromeLayoutContext = true;
        _windowChromeLayoutPageCount = Count;
    }

    private void UpdateWindowChromeAuxiliaryRects()
    {
        _windowChromeCloseButtonRect = SKRect.Empty;
        _windowChromeNewTabButtonRect = SKRect.Empty;

        if (_selectedIndex >= 0 && _selectedIndex < _windowChromeTabRects.Count && TabCloseButton)
        {
            _windowChromeCloseButtonRect = WindowPageTabGeometry.CreateTrailingButtonRect(
                GetWindowChromeSelectedVisualRect(),
                WindowChromeTabCloseButtonSize * ScaleFactor,
                WindowChromeTabCloseButtonInset * ScaleFactor,
                0f,
                1f);
        }

        if (NewTabButton && _windowChromeTabRects.Count > 0)
        {
            var size = 24f * ScaleFactor;
            var gap = Math.Max(ResolvedTabGap * ScaleFactor, size / 2f);
            var lastTabRect = _windowChromeTabRects[_windowChromeTabRects.Count - 1];
            _windowChromeNewTabButtonRect = SKRect.Create(
                lastTabRect.Right + gap,
                _lastWindowChromeLayoutContext.CenterY - size / 2f,
                size,
                size);
        }
    }

    private SKRect GetWindowChromeSelectedVisualRect()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _windowChromeTabRects.Count)
            return SKRect.Empty;

        var activeRect = _windowChromeTabRects[_selectedIndex];
        if (!_windowChromeTabSelectionAnimation.IsAnimating() ||
            _windowChromePreviousSelectedIndex < 0 ||
            _windowChromePreviousSelectedIndex >= _windowChromeTabRects.Count ||
            _windowChromePreviousSelectedIndex == _selectedIndex)
            return activeRect;

        return WindowPageTabGeometry.InterpolateRect(
            _windowChromeTabRects[_windowChromePreviousSelectedIndex],
            activeRect,
            Math.Clamp((float)_windowChromeTabSelectionAnimation.GetProgress(), 0f, 1f));
    }

    private void DrawWindowChromeDivider(SKCanvas canvas, WindowPageChromeLayoutContext context, SKColor titleColor)
    {
        var dividerColor = titleColor != SKColor.Empty
            ? titleColor.Determine().WithAlpha(30)
            : ColorScheme.BorderColor;

        _tabBorderPaint.Color = dividerColor;
        _tabBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        var y = context.Bottom - (_tabBorderPaint.StrokeWidth / 2f);
        canvas.DrawLine(context.StartX, y, context.StartX + context.AvailableWidth, y, _tabBorderPaint);
    }

    private void DrawWindowChromeTabDividers(SKCanvas canvas, WindowPageChromeLayoutContext context, SKColor titleColor)
    {
        if (_windowChromeTabRects.Count < 2 || ResolvedTabGap * ScaleFactor > 0.5f ||
            TabDesignMode is WindowPageTabDesignMode.RoundedCompact or WindowPageTabDesignMode.Pill or WindowPageTabDesignMode.Minimal)
            return;

        var dividerColor = titleColor != SKColor.Empty
            ? titleColor.Determine().WithAlpha(42)
            : ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)78 : (byte)62);

        _tabBorderPaint.Color = dividerColor;
        _tabBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        var top = context.Top + 9f * ScaleFactor;
        var bottom = context.Bottom - 8f * ScaleFactor;

        for (var index = 0; index < _windowChromeTabRects.Count - 1; index++)
        {
            if (index == _selectedIndex || index + 1 == _selectedIndex ||
                index == _hoveredWindowChromeTabIndex || index + 1 == _hoveredWindowChromeTabIndex)
                continue;

            var x = (_windowChromeTabRects[index].Right + _windowChromeTabRects[index + 1].Left) * 0.5f;
            canvas.DrawLine(x, top, x, bottom, _tabBorderPaint);
        }
    }

    private SKColor GetWindowChromeTextColor(bool isSelected, bool isHovered, SKColor foreColor)
    {
        if (!Enabled)
            return foreColor.WithAlpha(156);

        if (isSelected && TabDesignMode == WindowPageTabDesignMode.Pill)
            return ColorScheme.Primary.Determine();

        if (isSelected && TabDesignMode == WindowPageTabDesignMode.Minimal)
            return ColorScheme.Primary;

        if (isSelected)
            return foreColor;

        if (isHovered)
            return foreColor.WithAlpha(232);

        return foreColor.WithAlpha(194);
    }

    private void DrawWindowChromeTabSurface(SKCanvas canvas, SKRect rect, bool isSelected, bool isHovered, SKColor hoverColor, SKColor foreColor, SKColor titleColor)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var isDark = ColorScheme.IsDarkMode;
        var isLightTitle = titleColor != SKColor.Empty && !titleColor.IsDark();
        var sf = ScaleFactor;

        var selectedBg = (isDark ? ColorScheme.SurfaceContainerHigh : ColorScheme.Surface).WithAlpha(150);
        var hoverBg = foreColor.WithAlpha(isLightTitle ? (byte)14 : (byte)18);
        var backgroundColor = isSelected ? selectedBg : hoverBg;
        var borderColor = isSelected
            ? ColorScheme.Outline.WithAlpha(isDark ? (byte)90 : (byte)68)
            : ColorScheme.Outline.WithAlpha(isDark ? (byte)36 : (byte)28);

        _tabBackgroundPaint.Color = backgroundColor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        switch (TabDesignMode)
        {
            case WindowPageTabDesignMode.Rectangle:
            {
                var flatRect = new SKRect(
                    MathF.Round(rect.Left),
                    MathF.Round(rect.Top + sf),
                    MathF.Round(rect.Right),
                    MathF.Round(rect.Bottom - sf));
                canvas.DrawRect(flatRect, _tabBackgroundPaint);
                if (isSelected)
                {
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(3f * sf));
                    canvas.DrawRect(flatRect.Left, flatRect.Bottom - indH, flatRect.Width, indH, _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Rounded:
            {
                var lift = 1.5f * sf;
                var roundedRect = new SKRect(
                    MathF.Round(rect.Left + sf),
                    MathF.Round(rect.Top + 5f * sf - lift),
                    MathF.Round(rect.Right - sf),
                    MathF.Round(rect.Bottom - 2f * sf - lift));
                var radius = MathF.Min(roundedRect.Height / 2f, MathF.Round(10f * sf));
                canvas.DrawRoundRect(roundedRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                {
                    canvas.DrawRoundRect(roundedRect, radius, radius, _tabBorderPaint);
                    _tabIndicatorPaint.Color = ColorScheme.Primary.WithAlpha(isLightTitle ? (byte)176 : (byte)208);
                    var indH = MathF.Max(2f, MathF.Round(3f * sf));
                    var indL = roundedRect.Left + MathF.Round(10f * sf);
                    var indW = MathF.Max(0f, roundedRect.Width - MathF.Round(20f * sf));
                    canvas.DrawRoundRect(SKRect.Create(indL, roundedRect.Bottom - indH, indW, indH),
                        MathF.Round(sf), MathF.Round(sf), _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.RoundedCompact:
            {
                var lift = 1.5f * sf;
                var shadcnRect = new SKRect(
                    MathF.Round(rect.Left + 2f * sf),
                    MathF.Round(rect.Top + 7f * sf - lift),
                    MathF.Round(rect.Right - 2f * sf),
                    MathF.Round(rect.Bottom - 4f * sf - lift));
                var radius = MathF.Min(shadcnRect.Height / 2f, MathF.Round(6f * sf));
                _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));
                canvas.DrawRoundRect(shadcnRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                {
                    canvas.DrawRoundRect(shadcnRect, radius, radius, _tabBorderPaint);
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(2f * sf));
                    var indW = shadcnRect.Width - MathF.Round(16f * sf);
                    var indL = shadcnRect.Left + MathF.Round(8f * sf);
                    canvas.DrawRoundRect(SKRect.Create(indL, shadcnRect.Bottom - indH, indW, indH),
                        MathF.Round(sf), MathF.Round(sf), _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Outlined:
            {
                if (!isSelected && !isHovered)
                    break;
                var lift = 2f * sf;
                var outRect = new SKRect(
                    MathF.Round(rect.Left),
                    MathF.Round(rect.Top + 4f * sf - lift),
                    MathF.Round(rect.Right),
                    MathF.Round(rect.Bottom - lift));
                var outRadius = MathF.Round(4f * sf);
                canvas.DrawRoundRect(outRect, outRadius, outRadius, _tabBackgroundPaint);
                if (isSelected)
                {
                    canvas.DrawRoundRect(outRect, outRadius, outRadius, _tabBorderPaint);
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(3f * sf));
                    canvas.DrawRect(outRect.Left + outRadius, outRect.Bottom - indH, outRect.Width - outRadius * 2f, indH, _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Pill:
            {
                if (!isSelected && !isHovered)
                    break;
                var lift = 1.5f * sf;
                var pillRect = new SKRect(
                    MathF.Round(rect.Left + 4f * sf),
                    MathF.Round(rect.Top + 7f * sf - lift),
                    MathF.Round(rect.Right - 4f * sf),
                    MathF.Round(rect.Bottom - 4f * sf - lift));
                var pillRadius = pillRect.Height / 2f;
                _tabBackgroundPaint.Color = isSelected
                    ? ColorScheme.Primary
                    : ColorScheme.Primary.WithAlpha(isDark ? (byte)22 : (byte)16);
                canvas.DrawRoundRect(pillRect, pillRadius, pillRadius, _tabBackgroundPaint);
                break;
            }

            case WindowPageTabDesignMode.Minimal:
            {
                if (!isSelected && !isHovered)
                    break;
                var minRect = new SKRect(
                    MathF.Round(rect.Left),
                    MathF.Round(rect.Top + sf),
                    MathF.Round(rect.Right),
                    MathF.Round(rect.Bottom - sf));
                _tabBackgroundPaint.Color = isSelected
                    ? ColorScheme.Primary.WithAlpha(isDark ? (byte)12 : (byte)9)
                    : hoverBg;
                canvas.DrawRect(minRect, _tabBackgroundPaint);
                if (isSelected)
                {
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(3f * sf));
                    canvas.DrawRect(minRect.Left, minRect.Bottom - indH, minRect.Width, indH, _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Chromed:
            default:
            {
                var lift = 2f * sf;
                var offTop = isSelected ? MathF.Round(4f * sf) : MathF.Round(7f * sf);
                var offBot = isSelected ? MathF.Max(1f, MathF.Round(sf)) : MathF.Round(2f * sf);
                var chromedRect = new SKRect(
                    MathF.Round(rect.Left),
                    MathF.Round(rect.Top + offTop - lift),
                    MathF.Round(rect.Right),
                    isSelected ? MathF.Round(rect.Bottom + offBot - lift) : MathF.Round(rect.Bottom - offBot - lift));
                WindowPageTabGeometry.BuildTopRoundedTabPath(_tabChromePath, chromedRect, MathF.Round(12f * sf));
                canvas.DrawPath(_tabChromePath, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawPath(_tabChromePath, _tabBorderPaint);
                break;
            }
        }
    }

    private void DrawWindowChromeCloseButton(SKCanvas canvas, SKRect rect, bool isHovered, SKColor foreColor, SKColor hoverColor)
    {
        var sf = ScaleFactor;
        var progress = Math.Clamp((float)_windowChromeTabCloseHoverAnimation.GetProgress(), 0f, 1f);
        _tabBackgroundPaint.Color = hoverColor.WithAlpha((byte)(28 + progress * 52));
        
        var midX = MathF.Round(rect.MidX);
        var midY = MathF.Round(rect.MidY);
        var width = MathF.Round(rect.Width / 2f);
        
        canvas.DrawCircle(midX, midY, width, _tabBackgroundPaint);

        if (progress > 0f)
        {
            _tabBorderPaint.Color = ColorScheme.Outline.WithAlpha((byte)(72 + progress * 70));
            _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));
            canvas.DrawCircle(midX, midY, Math.Max(0f, width - _tabBorderPaint.StrokeWidth * 0.5f), _tabBorderPaint);
        }

        var stroke = MathF.Max(1f, MathF.Round(sf));
        var crispOffset = (stroke % 2 != 0) ? 0.5f : 0f;
        
        using var linePaint = new SKPaint
        {
            Color = foreColor.WithAlpha(isHovered ? (byte)255 : (byte)222),
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        var size = MathF.Round(3.5f * sf);
        canvas.DrawLine(midX - size, midY - size, midX + size, midY + size, linePaint);
        canvas.DrawLine(midX - size, midY + size, midX + size, midY - size, linePaint);
    }

    private void DrawWindowChromeNewTabButton(SKCanvas canvas, SKRect rect, bool isHovered, SKColor foreColor, SKColor hoverColor)
    {
        var sf = ScaleFactor;
        var progress = Math.Clamp((float)_windowChromeNewTabHoverAnimation.GetProgress(), 0f, 1f);
        var baseFill = ColorScheme.SurfaceContainerHigh.WithAlpha(ColorScheme.IsDarkMode ? (byte)56 : (byte)72);
        var hoverFill = ColorScheme.SurfaceVariant.InterpolateColor(hoverColor, 0.16f).WithAlpha(ColorScheme.IsDarkMode ? (byte)132 : (byte)118);

        _tabBackgroundPaint.Color = baseFill.InterpolateColor(hoverFill, progress);
        _tabBorderPaint.Color = ColorScheme.Outline.WithAlpha((byte)(96 + progress * 44));
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        var roundedRect = new SKRect(MathF.Round(rect.Left), MathF.Round(rect.Top), MathF.Round(rect.Right), MathF.Round(rect.Bottom));
        var rad = MathF.Round(6f * sf);
        
        canvas.DrawRoundRect(roundedRect, rad, rad, _tabBackgroundPaint);
        canvas.DrawRoundRect(roundedRect, rad, rad, _tabBorderPaint);

        var stroke = MathF.Max(1.1f, MathF.Round(sf * 1.5f));
        var crispOffset = (stroke % 2 != 0) ? 0.5f : 0f;

        using var linePaint = new SKPaint
        {
            Color = foreColor.WithAlpha(isHovered ? (byte)255 : (byte)228),
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = false
        };

        var size = MathF.Round(5f * sf);
        var midX = MathF.Round(roundedRect.MidX) + crispOffset;
        var midY = MathF.Round(roundedRect.MidY) + crispOffset;
        
        canvas.DrawLine(midX - size, midY, midX + size, midY, linePaint);
        canvas.DrawLine(midX, midY - size, midX, midY + size, linePaint);
    }

    private void DrawTabHeaderSurface(SKCanvas canvas, SKRect headerRect, SKColor backgroundColor, SKColor borderColor)
    {
        var sf = ScaleFactor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        switch (TabDesignMode)
        {
            case WindowPageTabDesignMode.Rectangle:
            case WindowPageTabDesignMode.Chromed:
            case WindowPageTabDesignMode.Outlined:
            case WindowPageTabDesignMode.Minimal:
            {
                var divY = MathF.Round(headerRect.Bottom) - _tabBorderPaint.StrokeWidth * 0.5f;
                canvas.DrawLine(MathF.Round(headerRect.Left), divY, MathF.Round(headerRect.Right), divY, _tabBorderPaint);
                break;
            }

            case WindowPageTabDesignMode.Rounded:
            case WindowPageTabDesignMode.RoundedCompact:
            default:
                break;
        }
    }

    private void DrawTabBackground(SKCanvas canvas, SKRect rect, bool isSelected, bool isHovered,
        SKColor inactiveBackground, SKColor selectedBackground, SKColor hoverBackground,
        SKColor inactiveBorderColor, SKColor selectedBorderColor, float indicatorHeight)
    {
        var sf = ScaleFactor;
        var backgroundColor = isSelected ? selectedBackground : isHovered ? hoverBackground : inactiveBackground;
        var borderColor = isSelected ? selectedBorderColor : inactiveBorderColor;

        _tabBackgroundPaint.Color = backgroundColor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        switch (TabDesignMode)
        {
            case WindowPageTabDesignMode.Rectangle:
            {
                // Tailwind underline tabs: ghost hover tint + primary bottom indicator
                if (isHovered && !isSelected)
                {
                    var ghostRect = new SKRect(
                        MathF.Round(rect.Left + 2f * sf), MathF.Round(rect.Top + sf),
                        MathF.Round(rect.Right - 2f * sf), MathF.Round(rect.Bottom - sf));
                    var ghostRadius = MathF.Round(6f * sf);
                    canvas.DrawRoundRect(ghostRect, ghostRadius, ghostRadius, _tabBackgroundPaint);
                }
                if (isSelected)
                {
                    // Full-width 2px primary indicator at the bottom edge
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(2.5f * sf));
                    canvas.DrawRect(MathF.Round(rect.Left), MathF.Round(rect.Bottom) - indH, rect.Width, indH, _tabIndicatorPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Rounded:
            {
                // Segmented control: fills against the container background
                if (isSelected || isHovered)
                {
                    var vIn = MathF.Round(2.5f * sf);
                    var hIn = MathF.Round(2f * sf);
                    var pillRect = new SKRect(
                        MathF.Round(rect.Left + hIn), MathF.Round(rect.Top + vIn),
                        MathF.Round(rect.Right - hIn), MathF.Round(rect.Bottom - vIn));
                    var radius = MathF.Round(8f * sf);
                    canvas.DrawRoundRect(pillRect, radius, radius, _tabBackgroundPaint);
                    if (isSelected)
                        canvas.DrawRoundRect(pillRect, radius, radius, _tabBorderPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.RoundedCompact:
            {
                // shadcn/ui TabsTrigger: transparent until selected (card lift)
                if (isSelected || isHovered)
                {
                    var vIn = MathF.Round(3f * sf);
                    var hIn = MathF.Round(3f * sf);
                    var cardRect = new SKRect(
                        MathF.Round(rect.Left + hIn), MathF.Round(rect.Top + vIn + 3f * sf),
                        MathF.Round(rect.Right - hIn), MathF.Round(rect.Bottom - vIn - 3f * sf));
                    var radius = MathF.Round(6f * sf);
                    canvas.DrawRoundRect(cardRect, radius, radius, _tabBackgroundPaint);
                    if (isSelected)
                        canvas.DrawRoundRect(cardRect, radius, radius, _tabBorderPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Pill:
            {
                // Full-height pill: selected gets opaque Primary fill, hover gets faint tint
                if (isSelected || isHovered)
                {
                    var vIn = MathF.Round(3.5f * sf);
                    var hIn = MathF.Round(3f * sf);
                    var pillRect = new SKRect(
                        MathF.Round(rect.Left + hIn),  MathF.Round(rect.Top + vIn),
                        MathF.Round(rect.Right - hIn), MathF.Round(rect.Bottom - vIn));
                    var radius = pillRect.Height / 2f;
                    canvas.DrawRoundRect(pillRect, radius, radius, _tabBackgroundPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Outlined:
            {
                // Classic HTML tab: 3-sided border (left/top/right), open bottom merges with content
                var sw   = _tabBorderPaint.StrokeWidth;
                var half = sw * 0.5f;
                var offTop = MathF.Round(4f * sf);
                if (isSelected)
                {
                    var fillRect = new SKRect(
                        MathF.Round(rect.Left), MathF.Round(rect.Top + offTop),
                        MathF.Round(rect.Right), MathF.Round(rect.Bottom + sw));
                    canvas.DrawRect(fillRect, _tabBackgroundPaint);
                    var l = MathF.Round(rect.Left) + half;
                    var t = MathF.Round(rect.Top + offTop) + half;
                    var r = MathF.Round(rect.Right) - half;
                    var b = MathF.Round(rect.Bottom) + sw;
                    canvas.DrawLine(l, b, l, t, _tabBorderPaint);
                    canvas.DrawLine(l, t, r, t, _tabBorderPaint);
                    canvas.DrawLine(r, t, r, b, _tabBorderPaint);
                }
                else if (isHovered)
                {
                    var ghostRect = new SKRect(
                        MathF.Round(rect.Left + 2f * sf), MathF.Round(rect.Top + offTop + 2f * sf),
                        MathF.Round(rect.Right - 2f * sf), MathF.Round(rect.Bottom - sf));
                    canvas.DrawRect(ghostRect, _tabBackgroundPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Minimal:
            {
                // Linear/Raycast: subtle full-height tint fill + 3px Primary left-edge accent bar
                if (isSelected)
                {
                    canvas.DrawRect(
                        new SKRect(MathF.Round(rect.Left), MathF.Round(rect.Top), MathF.Round(rect.Right), MathF.Round(rect.Bottom)),
                        _tabBackgroundPaint);
                    var indW   = MathF.Max(2f, MathF.Round(3f * sf));
                    var indTop = MathF.Round(rect.Top + 8f * sf);
                    var indBot = MathF.Round(rect.Bottom - 8f * sf);
                    _tabIndicatorPaint.Color = _tabBorderPaint.Color;
                    canvas.DrawRoundRect(
                        SKRect.Create(MathF.Round(rect.Left), indTop, indW, MathF.Max(0f, indBot - indTop)),
                        indW / 2f, indW / 2f, _tabIndicatorPaint);
                }
                else if (isHovered)
                {
                    canvas.DrawRect(
                        new SKRect(MathF.Round(rect.Left), MathF.Round(rect.Top), MathF.Round(rect.Right), MathF.Round(rect.Bottom)),
                        _tabBackgroundPaint);
                }
                break;
            }

            case WindowPageTabDesignMode.Chromed:
            default:
            {
                if (isSelected || isHovered)
                {
                    var chrTop = isSelected ? MathF.Round(rect.Top + 4f * sf) : MathF.Round(rect.Top + 7f * sf);
                    var chrBot = isSelected ? MathF.Round(rect.Bottom + sf) : MathF.Round(rect.Bottom - 3f * sf);
                    var chromedRect = new SKRect(MathF.Round(rect.Left), chrTop, MathF.Round(rect.Right), chrBot);
                    WindowPageTabGeometry.BuildTopRoundedTabPath(_tabChromePath, chromedRect, MathF.Round(10f * sf));
                    canvas.DrawPath(_tabChromePath, _tabBackgroundPaint);
                    if (isSelected)
                        canvas.DrawPath(_tabChromePath, _tabBorderPaint);
                }
                break;
            }
        }
    }

    private void DrawTabIndicator(SKCanvas canvas, SKRect rect, float indicatorHeight)
    {
        if (indicatorHeight <= 0f)
            return;

        var indicatorInset = 10f * ScaleFactor;
        var indicatorRect = SKRect.Create(
            rect.Left + indicatorInset,
            rect.Bottom - indicatorHeight,
            Math.Max(0f, rect.Width - indicatorInset * 2f),
            indicatorHeight);

        if (indicatorRect.Width <= 0)
            return;

        _tabIndicatorPaint.Color = ColorScheme.Primary;
        canvas.DrawRoundRect(indicatorRect, indicatorHeight / 2f, indicatorHeight / 2f, _tabIndicatorPaint);
    }

    private void DrawTabCloseButton(SKCanvas canvas, SKRect buttonRect, bool isHovered, SKColor foreground)
    {
        var sf = ScaleFactor;
        var midX = MathF.Round(buttonRect.MidX);
        var midY = MathF.Round(buttonRect.MidY);
        var circleR = MathF.Round(buttonRect.Width * 0.44f);

        if (isHovered)
        {
            _tabBackgroundPaint.Color = foreground.WithAlpha(ColorScheme.IsDarkMode ? (byte)28 : (byte)22);
            canvas.DrawCircle(midX, midY, circleR, _tabBackgroundPaint);
        }

        using var xPaint = new SKPaint
        {
            Color = foreground.WithAlpha(isHovered ? (byte)220 : (byte)150),
            StrokeWidth = MathF.Max(1f, MathF.Round(1.5f * sf)),
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            IsStroke = true
        };
        var size = MathF.Round(3f * sf);
        canvas.DrawLine(midX - size, midY - size, midX + size, midY + size, xPaint);
        canvas.DrawLine(midX + size, midY - size, midX - size, midY + size, xPaint);
    }

    private void DrawNewTabButton(SKCanvas canvas, SKRect buttonRect, bool isHovered, SKColor foreground)
    {
        var sf = ScaleFactor;
        var midX = MathF.Round(buttonRect.MidX);
        var midY = MathF.Round(buttonRect.MidY);
        var circleR = MathF.Round(buttonRect.Width * 0.48f);

        _tabBackgroundPaint.Color = isHovered
            ? foreground.WithAlpha(ColorScheme.IsDarkMode ? (byte)22 : (byte)16)
            : foreground.WithAlpha(ColorScheme.IsDarkMode ? (byte)10 : (byte)7);
        canvas.DrawCircle(midX, midY, circleR, _tabBackgroundPaint);

        using var plusPaint = new SKPaint
        {
            Color = foreground.WithAlpha(isHovered ? (byte)210 : (byte)140),
            StrokeWidth = MathF.Max(1f, MathF.Round(1.5f * sf)),
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
            IsStroke = true
        };
        var size = MathF.Round(4f * sf);
        canvas.DrawLine(midX - size, midY, midX + size, midY, plusPaint);
        canvas.DrawLine(midX, midY - size, midX, midY + size, plusPaint);
    }

    private SKRect GetTabHeaderRect()
    {
        if (!ShouldDrawTabStrip)
            return SKRect.Empty;

        var rect = base.DisplayRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return SKRect.Empty;

        var headerHeight = Math.Min(rect.Height, GetTabHeaderHeight());
        return new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + headerHeight);
    }

    private float GetTabHeaderHeight()
    {
        return TabStripHeight * ScaleFactor;
    }

    private void PrepareTabFont()
    {
        PrepareTabFont(TabFontSize.Topx(this));
    }

    private void PrepareTabFont(float size)
    {
        var baseFont = Font;
        _tabFont.Typeface = baseFont.Typeface ?? SKTypeface.Default;
        _tabFont.Size = Math.Max(1f, size);
        _tabFont.Subpixel = true;
        _tabFont.Edging = SKFontEdging.SubpixelAntialias;
        _tabFont.Hinting = SKFontHinting.Full;
        _tabFont.Embolden = baseFont.Embolden;
        _tabFont.ScaleX = baseFont.ScaleX;
        _tabFont.SkewX = baseFont.SkewX;
        _tabFont.LinearMetrics = baseFont.LinearMetrics;
    }

    private bool TryGetTabIndexAtPoint(SKPoint point, out int tabIndex)
    {
        tabIndex = -1;

        if (!ShouldDrawTabStrip)
            return false;

        UpdateTabRects();

        for (var i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(point))
            {
                tabIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool TryGetTabCloseButtonIndexAtPoint(SKPoint point, out int tabIndex)
    {
        tabIndex = -1;

        if (!ShouldDrawTabCloseButtons)
            return false;

        UpdateTabRects();

        for (var i = 0; i < _tabCloseButtonRects.Count; i++)
        {
            if (_tabCloseButtonRects[i].Contains(point))
            {
                tabIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool IsPointOverNewTabButton(SKPoint point)
    {
        if (!ShouldDrawNewTabButton)
            return false;

        UpdateTabRects();
        return _newTabButtonRect.Contains(point);
    }

    private void UpdateTabRects()
    {
        _tabCloseButtonRects.Clear();
        _newTabButtonRect = SKRect.Empty;
        _tabRects.Clear();
        _tabWidthBuffer.Clear();

        var pageCount = Count;
        if (pageCount <= 0)
            return;

        var headerRect = GetTabHeaderRect();
        if (headerRect.Width <= 0 || headerRect.Height <= 0)
            return;

        PrepareTabFont();

        var gap = ResolvedTabGap * ScaleFactor;
        var horizontalPadding = TabHorizontalPadding * ScaleFactor;
        var verticalInset = TabVerticalInset * ScaleFactor;
        var minWidth = TabMinWidth * ScaleFactor;
        var maxWidth = TabMaxWidth * ScaleFactor;
        var iconAllowance = (TabIconSize + TabIconSpacing) * ScaleFactor;
        var closeButtonSize = TabCloseButtonSize * ScaleFactor;
        var closeButtonSpacing = TabCloseButtonSpacing * ScaleFactor;
        var closeButtonAllowance = ShouldDrawTabCloseButtons ? closeButtonSize + closeButtonSpacing : 0f;
        var newTabButtonSize = NewTabButtonSize * ScaleFactor;
        var newTabReserve = ShouldDrawNewTabButton ? newTabButtonSize + gap : 0f;
        var contentWidth = Math.Max(0f, headerRect.Width - (horizontalPadding * 2f) - newTabReserve);

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var width = WindowPageTabGeometry.MeasureDesiredTabWidth(page, _tabFont, horizontalPadding,
                iconAllowance, closeButtonAllowance, minWidth, maxWidth,
                ShouldDrawTabIcons, ShouldDrawTabCloseButtons);
            _tabWidthBuffer.Add(width);
        }

        var startX = ComputeTabStartX(headerRect, horizontalPadding, newTabReserve, contentWidth, gap);

        WindowPageTabGeometry.LayoutTabs(_tabWidthBuffer,
            startX,
            headerRect.Top + verticalInset,
            Math.Max(0f, headerRect.Height - (verticalInset * 2f)),
            contentWidth,
            gap,
            maxWidth,
            false,
            _tabRects);

        var currentX = startX;
        if (_tabRects.Count > 0)
        {
            currentX = _tabRects[_tabRects.Count - 1].Right + gap;
        }

        for (var pageIndex = 0; pageIndex < _tabRects.Count; pageIndex++)
            _tabCloseButtonRects.Add(CreateTabCloseButtonRect(_tabRects[pageIndex], closeButtonSize, horizontalPadding));

        if (ShouldDrawNewTabButton)
        {
            var newButtonLeft = Math.Min(currentX, headerRect.Right - horizontalPadding - newTabButtonSize);
            _newTabButtonRect = SKRect.Create(
                newButtonLeft,
                headerRect.MidY - newTabButtonSize / 2f,
                newTabButtonSize,
                newTabButtonSize);
        }
    }

    private float ComputeTabStartX(SKRect headerRect, float horizontalPadding, float newTabReserve, float contentWidth, float gap)
    {
        if (_tabAlignment == WindowPageTabAlignment.Start)
            return headerRect.Left + horizontalPadding;

        var totalTabWidth = 0f;
        for (var i = 0; i < _tabWidthBuffer.Count; i++)
            totalTabWidth += _tabWidthBuffer[i];
        totalTabWidth += gap * MathF.Max(0f, _tabWidthBuffer.Count - 1);
        totalTabWidth = MathF.Min(totalTabWidth, contentWidth);

        return _tabAlignment switch
        {
            WindowPageTabAlignment.Center => headerRect.Left + horizontalPadding + (contentWidth - totalTabWidth) / 2f,
            WindowPageTabAlignment.End    => headerRect.Left + horizontalPadding + (contentWidth - totalTabWidth),
            _                             => headerRect.Left + horizontalPadding
        };
    }

    private SKRect CreateTabCloseButtonRect(SKRect tabRect, float preferredSize, float horizontalPadding)
    {
        if (!ShouldDrawTabCloseButtons)
            return SKRect.Empty;

        return WindowPageTabGeometry.CreateTrailingButtonRect(tabRect, preferredSize, horizontalPadding, 10f * ScaleFactor);
    }

    private void StartTabSelectionAnimation(int previousSelectedIndex, int nextSelectedIndex)
    {
        if (TabMode != WindowPageTabMode.Embedded || previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
        {
            _previousSelectedIndex = nextSelectedIndex;
            _tabSelectionAnimation.SetProgress(1);
            return;
        }

        _previousSelectedIndex = previousSelectedIndex;
        _tabSelectionAnimation.SetProgress(0);
        _tabSelectionAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void StartWindowChromeSelectionAnimation(int previousSelectedIndex, int nextSelectedIndex)
    {
        if (TabMode != WindowPageTabMode.WindowChrome || previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
        {
            _windowChromePreviousSelectedIndex = nextSelectedIndex;
            _windowChromeTabSelectionAnimation.SetProgress(1);
            return;
        }

        _windowChromePreviousSelectedIndex = previousSelectedIndex;
        _windowChromeTabSelectionAnimation.SetProgress(0);
        _windowChromeTabSelectionAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void ResetTabSelectionAnimation()
    {
        _previousSelectedIndex = _selectedIndex;
        _tabSelectionAnimation.SetProgress(1);
    }

    private void ResetWindowChromeState()
    {
        _windowChromePreviousSelectedIndex = _selectedIndex;
        _windowChromeTabSelectionAnimation.SetProgress(1);
        ResetWindowChromeHoverState();
        InvalidateWindowChromeLayout();
    }

    private void InvalidateTabChrome()
    {
        InvalidateWindowChromeLayout();
        InvalidateRenderTree();
        Invalidate();
    }

    private int GetDirectionalSign()
    {
        if (_transitionFromIndex < 0 || _transitionToIndex < 0)
            return 1;

        return _transitionToIndex >= _transitionFromIndex ? 1 : -1;
    }

    private SKRect GetTransitionViewport()
    {
        var selectedPage = GetPageAt(_selectedIndex) ?? GetPageAt(_transitionFromIndex) ?? GetPageAt(_transitionToIndex);
        if (selectedPage != null)
        {
            SyncPageBounds(selectedPage);
            return selectedPage.Bounds;
        }

        return DisplayRectangle;
    }

    private void SyncAllPageBounds()
    {
        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page != null)
                SyncPageBounds(page);
        }
    }

    private void SyncPageBounds(ElementBase page)
    {
        var viewport = DisplayRectangle;
        if (page.Bounds != viewport)
            page.Arrange(viewport);
    }

    private void ReleaseTransitionSnapshots()
    {
        lock (_transitionSnapshotSync)
        {
            _transitionFromSnapshot?.Dispose();
            _transitionFromSnapshot = null;

            _transitionToSnapshot?.Dispose();
            _transitionToSnapshot = null;
        }
    }

    private void FinalizeCompletedTransitionIfPending()
    {
        if (Interlocked.Exchange(ref _transitionFinalizationPending, 0) == 0)
            return;

        CommitSelectedPageVisibility();
        ReleaseTransitionSnapshots();
        _transitionFromIndex = -1;
        _transitionToIndex = -1;
        _isTransitionDirty = false;
        InvalidateRenderTree();
    }

    private static double ValidateIncrement(double value)
    {
        return value <= 0 ? 0.01 : value;
    }
}