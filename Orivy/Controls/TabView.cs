using Orivy.Animation;
using Orivy;
using Orivy.Extensions;
using Orivy.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Orivy.Controls;

public partial class TabView : ElementBase
{
    private const long DefaultMaxTransitionSnapshotBytes = 32L * 1024 * 1024;
    private const float DefaultTabGap = 0f;
    private const float TabHorizontalPadding = 14f;
    private const float TabVerticalInset = 4f;
    private const float TabIndicatorHeight = 3f;
    private const float TabIconSize = 24f;
    private const float TabIconSpacing = 4f;
    private const float TabCloseButtonSize = 18f;
    private const float TabCloseButtonSpacing = 8f;
    private const float TabMinWidth = 30f;
    private const float TabMaxWidth = 240f;
    private const float VerticalTabMinWidth = 160f;
    private const float VerticalTabMaxWidth = 360f;
    private const float VerticalTabMinHeight = 32f;
    private const float VerticalTabMaxHeight = 74f;
    private const float VerticalTabStripResizeMaxWidth = 420f;
    private const float TabStripResizerThickness = 14f;
    private const float TabStripResizerVisualThickness = 2f;
    private const float TabStripResizerVisualLength = 28f;
    private const float TabStripResizerAnimationSpeed = 0.14f;
    private const float NewTabButtonSize = 22f;
    private const float TabSelectionAnimationSpeed = 0.14f;
    private const float TabHoverAnimationStep = 0.18f;
    private const float TabDragThreshold = 6f;
    private const float TitleBarTabHorizontalPadding = 8f;
    private const float TitleBarTabIconSize = 16f;
    private const float TitleBarTabIconSpacing = 4f;
    private const float TitleBarTabCloseButtonSize = 20f;
    private const float TitleBarTabCloseButtonInset = 4.5f;
    private const float TitleBarTabSelectionAnimationSpeed = 0.10f;
    private const float TitleBarHoverAnimationSpeed = 0.10f;
    private const float TabFontSize = 9.5f;
    private const float TitleBarTabFontSize = 8.5f;
    private const float TitleBarTabFontSizeWithIcon = 9.25f;

    private readonly AnimationManager _transitionAnimation;
    private readonly AnimationManager _tabSelectionAnimation;
    private readonly AnimationManager _tabStripResizerAnimation;
    private readonly AnimationManager _titleBarTabSelectionAnimation;
    private readonly AnimationManager _titleBarTabCloseHoverAnimation;
    private readonly AnimationManager _titleBarNewTabHoverAnimation;
    private readonly object _transitionSnapshotSync = new();
    private readonly List<SKRect> _tabCloseButtonRects = new();
    private readonly List<SKRect> _tabRects = new();
    private readonly List<float> _tabWidthBuffer = new();
    private readonly List<SKRect> _titleBarTabRects = new();
    private readonly List<float> _titleBarTabWidthBuffer = new();
    private readonly SKPaint _tabBackgroundPaint;
    private readonly SKPaint _tabBorderPaint;
    private readonly SKPaint _tabGlyphPaint;
    private readonly SKPaint _tabIndicatorPaint;
    private readonly SKPaint _tabTextPaint;
    private readonly SKPath _tabPath;
    private readonly SKFont _tabFont;
    private EventHandler? _onNewTabButtonClick;
    private EventHandler<int>? _onSelectedIndexChanged;
    private EventHandler<int>? _onTabCloseButtonClick;
    private bool _drawTabIcons;
    private SKRect _newTabButtonRect = SKRect.Empty;
    private int _previousSelectedIndex = -1;
    private int _hoveredTabCloseIndex = -1;
    private bool _hoveredNewTabButton;
    private SKRect _titleBarCloseButtonRect = SKRect.Empty;
    private SKRect _titleBarNewTabButtonRect = SKRect.Empty;
    private int _titleBarPreviousSelectedIndex = -1;
    private int _hoveredTitleBarTabIndex = -1;
    private bool _hoveredTitleBarCloseButton;
    private bool _hoveredTitleBarNewTabButton;
    private TabViewTitleBarLayoutContext _lastTitleBarLayoutContext;
    private bool _hasTitleBarLayoutContext;
    private int _titleBarLayoutPageCount = -1;
    private float _tabStripHeight = 44f;
    private float _verticalTabStripWidth = 44f;
    private float _verticalTabScrollOffset;
    private float _verticalTabScrollableExtent;
    private bool _showTabStripResizer;
    private int _hoveredTabIndex = -1;
    private bool _hoveredTabStripResizer;
    private bool _isTransitionDirty;
    private bool _isResizingTabStrip;
    private bool _newTabButton;
    private int _selectedIndex = -1;
    private bool _tabCloseButton;
    private float _tabStripResizeOrigin;
    private float _tabStripResizeStartHeight;
    private float _tabGap = DefaultTabGap;
    private TabViewDesignMode _tabDesignMode = TabViewDesignMode.Rectangle;
    private TabViewAlignment _tabAlignment = TabViewAlignment.Start;
    private TabViewLayoutMode _tabLayoutMode = TabViewLayoutMode.Top;
    private TabViewMode _tabMode = TabViewMode.TitleBar;
    private SKColor _tabStripBackground = SKColors.Transparent;
    private TabViewStyle? _customTabStyle;
    private bool _allowTabDrag = true;
    private List<ElementBase>? _pageOrder;
    private int _dragTabSourceIndex = -1;
    private bool _isDraggingTab;
    private float _dragTabGrabX;
    private float _dragTabCurrentX;
    private int _dragTabInsertIndex = -1;
    private float[] _tabDodgeAnimOffsets = Array.Empty<float>();
    private float[] _tabHoverProgress = Array.Empty<float>();
    private float[] _tabHoverTargets = Array.Empty<float>();
    private float[] _titleBarTabHoverProgress = Array.Empty<float>();
    private float[] _titleBarTabHoverTargets = Array.Empty<float>();
    private readonly object _tabHoverSync = new();
    private readonly System.Timers.Timer _tabHoverTimer;
    private int _transitionFinalizationPending;
    private int _transitionFromIndex = -1;
    private int _transitionToIndex = -1;
    private SKImage? _transitionFromSnapshot;
    private SKImage? _transitionToSnapshot;
    private SKRect _transitionViewport = SKRect.Empty;
    private readonly SKPaint _transitionPaint;
    internal event EventHandler? TabModeChanged;

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

            var headerThickness = Math.Min(
                UsesVerticalTabLayout ? rect.Width : rect.Height,
                GetTabHeaderThickness());

            return _tabLayoutMode switch
            {
                TabViewLayoutMode.Left => new SKRect(
                    Math.Min(rect.Right, rect.Left + headerThickness),
                    rect.Top,
                    rect.Right,
                    rect.Bottom),
                TabViewLayoutMode.Right => new SKRect(
                    rect.Left,
                    rect.Top,
                    Math.Max(rect.Left, rect.Right - headerThickness),
                    rect.Bottom),
                TabViewLayoutMode.Bottom => new SKRect(
                    rect.Left,
                    rect.Top,
                    rect.Right,
                    Math.Max(rect.Top, rect.Bottom - headerThickness)),
                _ => new SKRect(
                    rect.Left,
                    Math.Min(rect.Bottom, rect.Top + headerThickness),
                    rect.Right,
                    rect.Bottom)
            };
        }
    }

    public TabView()
    {
        MouseWheel += HandleMouseWheelRouting;
        ImageAlign = ContentAlignment.MiddleLeft;

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
        _tabGlyphPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round
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
        _tabPath = new SKPath();
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

        _tabHoverTimer = new System.Timers.Timer(16)
        {
            AutoReset = true,
            Enabled = false
        };
        _tabHoverTimer.Elapsed += HandleTabHoverTimerElapsed;

        _tabStripResizerAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = TabStripResizerAnimationSpeed,
            SecondaryIncrement = TabStripResizerAnimationSpeed,
            AnimationType = AnimationType.CubicEaseOut
        };
        _tabStripResizerAnimation.OnAnimationProgress += HandleTabStripResizerProgress;

        _titleBarTabSelectionAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = TitleBarTabSelectionAnimationSpeed,
            SecondaryIncrement = TitleBarTabSelectionAnimationSpeed,
            AnimationType = AnimationType.CubicEaseOut
        };
        _titleBarTabSelectionAnimation.OnAnimationProgress += HandleTitleBarSelectionProgress;
        _titleBarTabSelectionAnimation.OnAnimationFinished += HandleTitleBarSelectionFinished;

        _titleBarTabCloseHoverAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = TitleBarHoverAnimationSpeed,
            SecondaryIncrement = TitleBarHoverAnimationSpeed,
            AnimationType = AnimationType.EaseInOut
        };
        _titleBarTabCloseHoverAnimation.OnAnimationProgress += HandleTitleBarHoverProgress;

        _titleBarNewTabHoverAnimation = new AnimationManager
        {
            Singular = true,
            InterruptAnimation = true,
            Increment = TitleBarHoverAnimationSpeed,
            SecondaryIncrement = TitleBarHoverAnimationSpeed,
            AnimationType = AnimationType.EaseInOut
        };
        _titleBarNewTabHoverAnimation.OnAnimationProgress += HandleTitleBarHoverProgress;

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
    [DefaultValue(TabViewMode.TitleBar)]
    public TabViewMode TabMode
    {
        get => _tabMode;
        set
        {
            if (_tabMode == value)
                return;

            _tabMode = value;
            ResetTabStripResizerInteraction();
            _hoveredTabIndex = -1;
            ResetTabSelectionAnimation();
            ResetTitleBarState();

            CancelTransitionPreservingSelection();
            PerformLayout();
            InvalidateRenderTree();
            Invalidate();
            TabModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Category("Layout")]
    [DefaultValue(TabViewLayoutMode.Top)]
    [Description("Controls which edge hosts the embedded tab strip. TitleBar mode always renders tabs on the top edge.")]
    public TabViewLayoutMode TabLayoutMode
    {
        get => _tabLayoutMode;
        set
        {
            if (_tabLayoutMode == value)
                return;

            _tabLayoutMode = value;
            ResetTabStripResizerInteraction();
            if (_tabLayoutMode is not TabViewLayoutMode.Left and not TabViewLayoutMode.Right)
                ResetVerticalTabScroll();
            else
                EnsureSelectedVerticalTabVisible();
            ResetTabSelectionAnimation();
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
        get => UsesVerticalTabLayout ? _verticalTabStripWidth : _tabStripHeight;
        set
        {
            var clamped = Math.Max(32f, value);
            if (Math.Abs(_tabStripHeight - clamped) < 0.001f && Math.Abs(_verticalTabStripWidth - clamped) < 0.001f)
                return;

            _tabStripHeight = clamped;
            _verticalTabStripWidth = clamped;
            ApplyTabStripThicknessChange();
        }
    }

    private void ApplyTabStripThicknessChange()
    {
        EnsureSelectedVerticalTabVisible();
        CancelTransitionPreservingSelection();
        PerformLayout();
        InvalidateRenderTree();
        Invalidate();
    }

    private void SetVerticalTabStripWidth(float value)
    {
        var clamped = Math.Max(VerticalTabMinWidth, value);
        if (Math.Abs(_verticalTabStripWidth - clamped) < 0.001f)
            return;

        _verticalTabStripWidth = clamped;
        ApplyTabStripThicknessChange();
    }

    [Category("Layout")]
    [DefaultValue(false)]
    [Description("Shows a draggable splitter between a vertical embedded tab strip and the page content.")]
    public bool ShowTabStripResizer
    {
        get => _showTabStripResizer;
        set
        {
            if (_showTabStripResizer == value)
                return;

            _showTabStripResizer = value;
            if (!_showTabStripResizer)
                ResetTabStripResizerInteraction();

            PerformLayout();
            InvalidateRenderTree();
            Invalidate();
        }
    }

    [Browsable(false)]
    public bool UsesTabStrip => TabMode == TabViewMode.Embedded;

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
    [DefaultValue(TabViewDesignMode.Rectangle)]
    public TabViewDesignMode TabDesignMode
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TabViewStyle? CustomTabStyle
    {
        get => _customTabStyle;
        set
        {
            _customTabStyle = value;
            InvalidateTabChrome();
        }
    }

    public TabView ConfigureTabStyle(Action<TabViewStyleBuilder> configure, bool clearExisting = false)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new TabViewStyleBuilder(clearExisting ? default : _customTabStyle ?? default);
        configure(builder);
        CustomTabStyle = builder.Build();
        return this;
    }

    public void ClearCustomTabStyle()
    {
        CustomTabStyle = null;
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
    [DefaultValue(TabViewAlignment.Start)]
    [Description("Controls alignment of tabs along the embedded tab strip's primary axis.")]
    public TabViewAlignment TabAlignment
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
    public TabViewDesignMode TitleBarTabDesignMode
    {
        get => TabDesignMode;
        set => TabDesignMode = value;
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool EnableTransitions { get; set; } = true;

    [Category("Behavior")]
    [DefaultValue(DefaultMaxTransitionSnapshotBytes)]
    [Description("Upper bound for retained page transition snapshots in bytes. Set to 0 to disable the limit.")]
    public long MaxTransitionSnapshotBytes { get; set; } = DefaultMaxTransitionSnapshotBytes;

    [Category("Behavior")]
    [DefaultValue(TabViewTransitionEffect.SlideHorizontal)]
    public TabViewTransitionEffect TransitionEffect { get; set; } = TabViewTransitionEffect.SlideHorizontal;

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
    internal float ResolvedTabGap => Math.Max(0f, _customTabStyle?.Metrics.Gap ?? _tabGap);

    private bool ShouldDrawTabStrip => TabMode == TabViewMode.Embedded && Count > 0;
    private bool ShouldDrawTabStripResizer => _showTabStripResizer && SupportsTabStripResizer;
    private bool ShouldDrawTabIcons => ShouldDrawTabStrip && DrawTabIcons;
    private bool ShouldDrawTabCloseButtons => ShouldDrawTabStrip && TabCloseButton;
    private bool ShouldDrawNewTabButton => ShouldDrawTabStrip && NewTabButton;
    private bool SupportsTabStripResizer => ShouldDrawTabStrip && _tabLayoutMode is TabViewLayoutMode.Left or TabViewLayoutMode.Right;
    private bool UsesVerticalTabLayout => ShouldDrawTabStrip && _tabLayoutMode is TabViewLayoutMode.Left or TabViewLayoutMode.Right;
    private bool UsesNonTopEmbeddedTabLayout => ShouldDrawTabStrip && _tabLayoutMode != TabViewLayoutMode.Top;

    private bool IsTabViewPage(ElementBase element)
    {
        return element is Container;
    }

    private int GetPageCount()
    {
        var count = 0;
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is ElementBase element && IsTabViewPage(element))
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
            if (Controls[i] is not ElementBase element || !IsTabViewPage(element))
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
            if (ParentWindow is Window window)
                window.CloseFloatingOverlays();
            EnsureSelectedVerticalTabVisible();
            StartTabSelectionAnimation(previousSelectedIndex, _selectedIndex);
            StartTitleBarSelectionAnimation(previousSelectedIndex, _selectedIndex);

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
        _transitionViewport = SKRect.Empty;

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

        if (e.Element is not ElementBase element || !IsTabViewPage(element))
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

        EnsureSelectedVerticalTabVisible();
        ResetTabSelectionAnimation();

        CancelTransitionPreservingSelection();
    }

    internal override void OnControlRemoved(ElementEventArgs e)
    {
        base.OnControlRemoved(e);

        if (e.Element is not ElementBase element || !IsTabViewPage(element))
            return;

        if (_pageOrder != null)
            _pageOrder.Remove(element);

        if (Count == 0)
            _selectedIndex = -1;
        else if (_selectedIndex >= Count)
            SelectedIndex = Count - 1;

        EnsureSelectedVerticalTabVisible();
        ResetTabSelectionAnimation();

        CancelTransitionPreservingSelection();
    }

    internal override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible && IsTransitioning)
            StopTransition();

        if (!Visible)
            ResetTitleBarHoverState();
    }

    internal override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        SyncAllPageBounds();
        EnsureSelectedVerticalTabVisible();
        InvalidateTitleBarLayout();

        if (IsTransitioning)
            _isTransitionDirty = true;
    }

    protected override bool ShouldIncludeHitTestElement(ElementBase element, bool requireEnabled)
    {
        if (!base.ShouldIncludeHitTestElement(element, requireEnabled))
            return false;

        if (!IsTabViewPage(element) || !IsTransitioning || !LockInputDuringTransition)
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
        if (e.Button == MouseButtons.Left && TryBeginTabStripResize(e.Location))
        {
            base.OnMouseDown(e);
            return;
        }

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
                    _dragTabGrabX = tabIndex < _tabRects.Count
                        ? GetTabPrimaryCoordinate(e.Location) - GetTabPrimaryStart(_tabRects[tabIndex])
                        : 0f;
                    _dragTabCurrentX = GetTabPrimaryCoordinate(e.Location);
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
        if (_isResizingTabStrip)
        {
            UpdateTabStripResize(e.Location);
            return;
        }

        base.OnMouseMove(e);

        if (!ShouldDrawTabStrip)
        {
            if (_hoveredTabIndex >= 0 || _hoveredTabCloseIndex >= 0 || _hoveredNewTabButton || _hoveredTabStripResizer)
            {
                _hoveredTabIndex = -1;
                _hoveredTabCloseIndex = -1;
                _hoveredNewTabButton = false;
                if (_hoveredTabStripResizer)
                    _tabStripResizerAnimation.StartNewAnimation(AnimationDirection.Out);
                _hoveredTabStripResizer = false;
                Cursor = Cursors.Default;
                Invalidate();
            }

            return;
        }

        var hoveredTabStripResizer = IsPointOverTabStripResizer(e.Location);
        var resizerStateChanged = _hoveredTabStripResizer != hoveredTabStripResizer;
        if (resizerStateChanged)
        {
            _hoveredTabStripResizer = hoveredTabStripResizer;
            Cursor = hoveredTabStripResizer ? GetTabStripResizerCursor() : Cursors.Default;
            _tabStripResizerAnimation.StartNewAnimation(hoveredTabStripResizer ? AnimationDirection.In : AnimationDirection.Out);
        }

        if (hoveredTabStripResizer || _isResizingTabStrip)
            GetParentWindow()?.UpdateCursor(this);

        if (_dragTabSourceIndex >= 0)
        {
            var pointerPrimary = GetTabPrimaryCoordinate(e.Location);
            var grabOriginX = _dragTabSourceIndex < _tabRects.Count
                ? GetTabPrimaryStart(_tabRects[_dragTabSourceIndex]) + _dragTabGrabX
                : _dragTabCurrentX;

            if (!_isDraggingTab && Math.Abs(pointerPrimary - grabOriginX) > TabDragThreshold * ScaleFactor)
                _isDraggingTab = true;

            if (_isDraggingTab)
            {
                _dragTabCurrentX = pointerPrimary;
                _dragTabInsertIndex = ComputeDragInsertIndex(pointerPrimary);
                Invalidate();
                return;
            }
        }

        var hoveredTabIndex = TryGetTabIndexAtPoint(e.Location, out var tabIndex) ? tabIndex : -1;
        var hoveredCloseTabIndex = TryGetTabCloseButtonIndexAtPoint(e.Location, out var closeTabIndex) ? closeTabIndex : -1;
        var hoveredNewTabButton = IsPointOverNewTabButton(e.Location);
        if (_hoveredTabIndex == hoveredTabIndex &&
            _hoveredTabCloseIndex == hoveredCloseTabIndex &&
            _hoveredNewTabButton == hoveredNewTabButton &&
            !resizerStateChanged)
            return;

        if (_hoveredTabIndex != hoveredTabIndex)
        {
            _hoveredTabIndex = hoveredTabIndex;
            SetTabHoverTarget(_tabRects.Count, hoveredTabIndex);
        }
        _hoveredTabCloseIndex = hoveredCloseTabIndex;
        _hoveredNewTabButton = hoveredNewTabButton;
        Invalidate();
    }

    internal override void OnMouseWheel(MouseEventArgs e)
    {
        if (UsesVerticalTabLayout && ShouldDrawTabStrip && !e.IsHorizontalWheel)
        {
            var headerRect = GetTabHeaderRect();
            if (headerRect.Width > 0f && headerRect.Height > 0f && headerRect.Contains(e.Location))
            {
                UpdateTabRects();

                if (_verticalTabScrollableExtent > 0f)
                {
                    var step = GetVerticalTabScrollStep();
                    SetVerticalTabScrollOffset(_verticalTabScrollOffset - ((e.Delta / 120f) * step));
                    return;
                }
            }
        }

        base.OnMouseWheel(e);
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

        if (_hoveredTabIndex >= 0 || _hoveredTabCloseIndex >= 0 || _hoveredNewTabButton || _hoveredTabStripResizer)
        {
            _hoveredTabIndex = -1;
            SetTabHoverTarget(_tabRects.Count, -1);
            _hoveredTabCloseIndex = -1;
            _hoveredNewTabButton = false;
            if (_hoveredTabStripResizer)
                _tabStripResizerAnimation.StartNewAnimation(AnimationDirection.Out);
            _hoveredTabStripResizer = false;
            if (!_isResizingTabStrip)
                Cursor = Cursors.Default;
            invalidate = true;
        }

        if (invalidate)
            Invalidate();
    }

    protected override bool TryRenderChildContent(SKCanvas canvas)
    {
        FinalizeCompletedTransitionIfPending();

        if (!IsTransitioning)
        {
            var selectedPage = GetPageAt(_selectedIndex);

            for (var i = 0; i < Controls.Count; i++)
            {
                if (Controls[i] is not ElementBase child || !child.Visible || child.Width <= 0f || child.Height <= 0f)
                    continue;

                if (IsTabViewPage(child))
                {
                    if (!ReferenceEquals(child, selectedPage))
                        continue;

                    SyncPageBounds(child);
                }

                if (NeedsFullChildRedraw)
                    child.InvalidateRenderTree();

                child.Render(canvas);
            }

            NeedsFullChildRedraw = false;
            return true;
        }

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

    protected override void OnImageAlignChanged(EventArgs e)
    {
        base.OnImageAlignChanged(e);
        InvalidateTabChrome();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleBarTabSelectionAnimation.OnAnimationProgress -= HandleTitleBarSelectionProgress;
            _titleBarTabSelectionAnimation.OnAnimationFinished -= HandleTitleBarSelectionFinished;
            _titleBarTabSelectionAnimation.Dispose();
            _titleBarTabCloseHoverAnimation.OnAnimationProgress -= HandleTitleBarHoverProgress;
            _titleBarTabCloseHoverAnimation.Dispose();
            _titleBarNewTabHoverAnimation.OnAnimationProgress -= HandleTitleBarHoverProgress;
            _titleBarNewTabHoverAnimation.Dispose();
            _tabSelectionAnimation.OnAnimationProgress -= HandleTabSelectionProgress;
            _tabSelectionAnimation.OnAnimationFinished -= HandleTabSelectionFinished;
            _tabSelectionAnimation.Dispose();
            _tabHoverTimer.Elapsed -= HandleTabHoverTimerElapsed;
            _tabHoverTimer.Stop();
            _tabHoverTimer.Dispose();
            _tabStripResizerAnimation.OnAnimationProgress -= HandleTabStripResizerProgress;
            _tabStripResizerAnimation.Dispose();
            _transitionAnimation.OnAnimationProgress -= HandleTransitionProgress;
            _transitionAnimation.OnAnimationFinished -= HandleTransitionFinished;
            _transitionAnimation.Dispose();
            _tabBackgroundPaint.Dispose();
            _tabBorderPaint.Dispose();
            _tabGlyphPaint.Dispose();
            _tabIndicatorPaint.Dispose();
            _tabTextPaint.Dispose();
            _tabPath.Dispose();
            _tabFont.Dispose();
            _transitionPaint.Dispose();
            ReleaseTransitionSnapshots();
        }

        base.Dispose(disposing);
    }

    private void HandleMouseWheelRouting(object? sender, MouseEventArgs e)
    {
    }

    private void HandleTransitionProgress(object _)
    {
        Invalidate();
    }

    private void HandleTabSelectionProgress(object _)
    {
        Invalidate();
    }

    private void HandleTabHoverTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateTabHoverAnimationFrame();
    }

    private void HandleTabStripResizerProgress(object _)
    {
        Invalidate();
    }

    private void HandleTitleBarSelectionProgress(object _)
    {
        Invalidate();
    }

    private void HandleTitleBarSelectionFinished(object _)
    {
        _titleBarPreviousSelectedIndex = _selectedIndex;
        UpdateTitleBarAuxiliaryRects();
        Invalidate();
    }

    private void HandleTitleBarHoverProgress(object _)
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

        if (!ShouldAnimateTransition(previousSelectedIndex, nextSelectedIndex))
            return false;

        var carryForwardSnapshot = IsTransitioning ? CaptureActiveTransitionSnapshot() : null;

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
        if (!EnableTransitions || TransitionEffect == TabViewTransitionEffect.None)
            return false;

        if (previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
            return false;

        var fromPage = GetPageAt(previousSelectedIndex);
        var toPage = GetPageAt(nextSelectedIndex);
        if (fromPage == null || toPage == null)
            return false;

        var viewport = GetTransitionViewport();
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;

        if (MaxTransitionSnapshotBytes <= 0)
            return true;

        return EstimateTransitionSnapshotBytes(viewport) <= MaxTransitionSnapshotBytes;
    }

    private static long EstimateTransitionSnapshotBytes(SKRect viewport)
    {
        try
        {
            var width = Math.Max(1L, (long)Math.Ceiling(viewport.Width));
            var height = Math.Max(1L, (long)Math.Ceiling(viewport.Height));
            return checked(width * height * 4L * 2L);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private void CommitSelectedPageVisibility()
    {
        var selectedPage = GetPageAt(_selectedIndex);
        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is not ElementBase element || !IsTabViewPage(element))
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
        _transitionViewport = SKRect.Empty;
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

        var viewport = DisplayRectangle;
        if (viewport.Width <= 0f || viewport.Height <= 0f)
            return false;

        _transitionViewport = viewport;

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
            case TabViewTransitionEffect.Fade:
                DrawFade(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.SlideHorizontal:
                DrawSlideHorizontal(canvas, fromSnapshot, toSnapshot, viewport, progress, pushExistingPage: false);
                break;
            case TabViewTransitionEffect.SlideVertical:
                DrawSlideVertical(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.ScaleFade:
                DrawScaleFade(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Push:
                DrawSlideHorizontal(canvas, fromSnapshot, toSnapshot, viewport, progress, pushExistingPage: true, drawJunctionShadow: true);
                break;
            case TabViewTransitionEffect.Cover:
                DrawCover(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Reveal:
                DrawReveal(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Uncover:
                DrawUncover(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Flip:
                DrawFlip(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Iris:
                DrawIris(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Morph:
                DrawMorph(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Zoom:
                DrawZoom(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.CrossZoom:
                DrawCrossZoom(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Split:
                DrawSplit(canvas, fromSnapshot, toSnapshot, viewport, progress);
                break;
            case TabViewTransitionEffect.Wipe:
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
        // 0›0.5: from squashes from full width to zero
        // 0.5›1: to expands from zero to full width
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
        // from shrinks 1.0 › 0.96, to grows 1.04 › 1.0, giving a soft dissolve-morph feel.
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
        //   0.0 › 0.55 : FROM shrinks (1.0 › 0.6) and fades completely out.
        //   0.45 › 1.0 : TO grows (0.6 › 1.0) and fades fully in.
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
        if (_isResizingTabStrip)
        {
            base.OnMouseUp(e);
            _isResizingTabStrip = false;
            _hoveredTabStripResizer = IsPointOverTabStripResizer(e.Location);
            _tabStripResizerAnimation.StartNewAnimation(_hoveredTabStripResizer ? AnimationDirection.In : AnimationDirection.Out);
            Cursor = _hoveredTabStripResizer ? GetTabStripResizerCursor() : Cursors.Default;
            Invalidate();
            return;
        }

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

            if (mouseX < GetTabPrimaryMidpoint(_tabRects[i]))
                return i;
        }

        return _tabRects.Count;
    }

    private float GetTabPrimaryCoordinate(SKPoint point)
    {
        return UsesVerticalTabLayout ? point.Y : point.X;
    }

    private float GetTabPrimaryStart(SKRect rect)
    {
        return UsesVerticalTabLayout ? rect.Top : rect.Left;
    }

    private float GetTabPrimaryMidpoint(SKRect rect)
    {
        return UsesVerticalTabLayout ? rect.MidY : rect.MidX;
    }

    private float GetTabPrimaryLength(SKRect rect)
    {
        return UsesVerticalTabLayout ? rect.Height : rect.Width;
    }

    private float GetHeaderPrimaryStart(SKRect rect)
    {
        return UsesVerticalTabLayout ? rect.Top : rect.Left;
    }

    private float GetHeaderPrimaryEnd(SKRect rect)
    {
        return UsesVerticalTabLayout ? rect.Bottom : rect.Right;
    }

    private SKRect OffsetTabRectAlongPrimaryAxis(SKRect rect, float delta)
    {
        return UsesVerticalTabLayout
            ? SKRect.Create(rect.Left, rect.Top + delta, rect.Width, rect.Height)
            : SKRect.Create(rect.Left + delta, rect.Top, rect.Width, rect.Height);
    }

    private SKRect CreatePrimaryAxisRect(SKRect sourceRect, float primaryStart)
    {
        return UsesVerticalTabLayout
            ? SKRect.Create(sourceRect.Left, primaryStart, sourceRect.Width, sourceRect.Height)
            : SKRect.Create(primaryStart, sourceRect.Top, sourceRect.Width, sourceRect.Height);
    }

    private void EnsurePageOrder()
    {
        if (_pageOrder != null)
            return;

        _pageOrder = new List<ElementBase>();
        for (var i = 0; i < Controls.Count; i++)
            if (Controls[i] is ElementBase el && IsTabViewPage(el))
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
        var horizontalPadding = GetTabHorizontalContentPadding();
        var closeButtonSpacing = TabCloseButtonSpacing * ScaleFactor;
        var isDark = ColorScheme.IsDarkMode;

        // --- Unified, clean color palette per design mode ---
        SKColor headerBackground, headerBorderColor,
                inactiveBackground, hoverBackground, selectedBackground,
                inactiveBorderColor, selectedBorderColor,
                activeTextColor, inactiveTextColor;

        switch (TabDesignMode)
        {
            case TabViewDesignMode.Rectangle:
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

            case TabViewDesignMode.Rounded:
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

            case TabViewDesignMode.RoundedCompact:
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

            case TabViewDesignMode.Pill:
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

            case TabViewDesignMode.Outlined:
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

            case TabViewDesignMode.Minimal:
                // Linear/Raycast sidebar: minimal surface, Primary left-accent bar on selected
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

            case TabViewDesignMode.Fluent:
                headerBackground    = (isDark ? ColorScheme.SurfaceContainerHigh : ColorScheme.SurfaceContainer).WithAlpha(isDark ? (byte)184 : (byte)218);
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)44 : (byte)34);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = ColorScheme.Primary.WithAlpha(isDark ? (byte)22 : (byte)14);
                selectedBackground  = (isDark ? ColorScheme.SurfaceContainerHigh : ColorScheme.Surface).WithAlpha(isDark ? (byte)232 : (byte)242);
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = SKColors.White.WithAlpha(isDark ? (byte)36 : (byte)144);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)166 : (byte)150) : ForeColor.WithAlpha(110);
                break;

            case TabViewDesignMode.MacOS:
                headerBackground    = ColorScheme.SurfaceContainer.WithAlpha(isDark ? (byte)150 : (byte)178);
                headerBorderColor   = ColorScheme.Outline.WithAlpha(isDark ? (byte)54 : (byte)42);
                inactiveBackground  = SKColors.Transparent;
                hoverBackground     = SKColors.White.WithAlpha(isDark ? (byte)16 : (byte)70);
                selectedBackground  = SKColors.White.WithAlpha(isDark ? (byte)34 : (byte)232);
                inactiveBorderColor = SKColors.Transparent;
                selectedBorderColor = ColorScheme.Outline.WithAlpha(isDark ? (byte)70 : (byte)46);
                activeTextColor     = Enabled ? ForeColor : ForeColor.WithAlpha(170);
                inactiveTextColor   = Enabled ? ForeColor.WithAlpha(isDark ? (byte)164 : (byte)146) : ForeColor.WithAlpha(110);
                break;

            case TabViewDesignMode.Chromed:
            default:
                // Browser-style tabs: surface strip, elevated selected card, divider line
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

        ApplyCustomTabPalette(
            ref headerBackground,
            ref headerBorderColor,
            ref inactiveBackground,
            ref hoverBackground,
            ref selectedBackground,
            ref inactiveBorderColor,
            ref selectedBorderColor,
            ref activeTextColor,
            ref inactiveTextColor);

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
                animatedSelectionRect = TabViewTabGeometry.InterpolateRect(
                    _tabRects[_previousSelectedIndex],
                    _tabRects[_selectedIndex],
                    Math.Clamp((float)_tabSelectionAnimation.GetProgress(), 0f, 1f));
            }
        }

        var activeTabsRect = SKRect.Empty;
        for (var i = 0; i < _tabRects.Count; i++)
        {
            if (activeTabsRect.IsEmpty) activeTabsRect = _tabRects[i];
            else activeTabsRect.Union(_tabRects[i]);
        }

        DrawTabHeaderSurface(canvas, headerRect,
            _tabStripBackground == SKColors.Transparent ? headerBackground : _tabStripBackground,
            headerBorderColor, activeTabsRect);
        DrawTabStripResizer(canvas);

        var clippedTabContent = UsesVerticalTabLayout;
        var clippedTabContentSave = 0;
        if (clippedTabContent)
        {
            clippedTabContentSave = canvas.Save();
            canvas.ClipRect(headerRect);
        }

        float ComputeDragTabTarget(int tIdx)
        {
            var srcSlotWidth = GetTabPrimaryLength(_tabRects[_dragTabSourceIndex]) + tabGap;
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

        EnsureTabHoverState(_tabRects.Count);

        for (var tabIndex = 0; tabIndex < _tabRects.Count; tabIndex++)
        {
            if (_isDraggingTab && tabIndex == _dragTabSourceIndex)
                continue;

            var rect = _tabRects[tabIndex];
            if (_isDraggingTab && tabIndex < _tabDodgeAnimOffsets.Length)
                rect = OffsetTabRectAlongPrimaryAxis(rect, _tabDodgeAnimOffsets[tabIndex]);

            var isSelected = tabIndex == _selectedIndex && !shouldAnimateSelection;
            var isHovered = tabIndex == _hoveredTabIndex;
            var hoverProgress = GetTabHoverProgress(tabIndex, isSelected);
            DrawTabBackground(canvas, rect, isSelected, isHovered,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight, hoverProgress);
        }

        if (animatedSelectionRect.Width > 0f && shouldAnimateSelection)
        {
            DrawTabBackground(canvas, animatedSelectionRect, true, false,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight, 0f);
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
            var hoverProgress = GetTabHoverProgress(tabIndex, isSelected);
            var iconRect = SKRect.Empty;
            var closeButtonRect = tabIndex < _tabCloseButtonRects.Count ? _tabCloseButtonRects[tabIndex] : SKRect.Empty;
            if (_isDraggingTab && tabIndex < _tabDodgeAnimOffsets.Length)
            {
                var shift = _tabDodgeAnimOffsets[tabIndex];
                rect = OffsetTabRectAlongPrimaryAxis(rect, shift);
                if (closeButtonRect.Width > 0)
                    closeButtonRect = OffsetTabRectAlongPrimaryAxis(closeButtonRect, shift);
            }

            if (closeButtonRect.Width > 0)
                DrawTabCloseButton(canvas, closeButtonRect, tabIndex == _hoveredTabCloseIndex, activeTextColor);

            var hasTabIcon = ShouldDrawTabIcons && page.HasImage;
            var tabTrailingReserve = closeButtonRect.Width > 0f ? closeButtonRect.Width + closeButtonSpacing : 0f;
            var innerVertPad = UsesVerticalTabLayout
                ? GetTabVerticalContentPadding() * 2f
                : GetTabVerticalContentPadding();
            SKRect textRect;
            (iconRect, textRect) = ComputeTabContentRects(rect, page.Text, hasTabIcon,
                horizontalPadding, innerVertPad, iconSize, iconSpacing, tabTrailingReserve);

            if (hasTabIcon)
                page.RenderImageSlot(canvas, iconRect);

            _tabTextPaint.Color = ResolveCustomTabTextColor(isSelected, hoverProgress,
                activeTextColor, inactiveTextColor, inactiveBackground, hoverBackground, selectedBackground);
            TextRenderer.DrawText(canvas, page.Text ?? string.Empty, textRect, _tabTextPaint, _tabFont, TextAlign, true, false);
        }

        if (_newTabButtonRect.Width > 0)
            DrawNewTabButton(canvas, _newTabButtonRect, _hoveredNewTabButton, activeTextColor);

        if (_isDraggingTab && _dragTabSourceIndex >= 0 && _dragTabSourceIndex < _tabRects.Count)
        {
            var srcRect  = _tabRects[_dragTabSourceIndex];
            var ghostLeft = Math.Clamp(
                _dragTabCurrentX - _dragTabGrabX,
                GetHeaderPrimaryStart(headerRect),
                GetHeaderPrimaryEnd(headerRect) - GetTabPrimaryLength(srcRect));
            var ghostRect = CreatePrimaryAxisRect(srcRect, ghostLeft);

            using var ghostLayerPaint = new SKPaint { Color = new SKColor(255, 255, 255, 210) };
            var layerSaved = canvas.SaveLayer(ghostLayerPaint);

            DrawTabBackground(canvas, ghostRect, true, false,
                inactiveBackground, selectedBackground, hoverBackground,
                inactiveBorderColor, selectedBorderColor, indicatorHeight, 0f);

            var ghostPage = GetPageAt(_dragTabSourceIndex);
            if (ghostPage != null)
            {
                var hasGhostIcon = ShouldDrawTabIcons && ghostPage.HasImage;
                (var ghostIconRect, var ghostTextRect) = ComputeTabContentRects(
                    ghostRect, ghostPage.Text, hasGhostIcon,
                    horizontalPadding, GetTabVerticalContentPadding(), iconSize, iconSpacing, 0f);

                if (hasGhostIcon)
                    ghostPage.RenderImageSlot(canvas, ghostIconRect);

                _tabTextPaint.Color = activeTextColor;
                TextRenderer.DrawText(canvas, ghostPage.Text ?? string.Empty, ghostTextRect, _tabTextPaint, _tabFont, TextAlign, true, false);
            }

            canvas.RestoreToCount(layerSaved);
        }

        if (clippedTabContent)
            canvas.RestoreToCount(clippedTabContentSave);
    }

    internal void HandleTitleBarSelectionChanged(int previousSelectedIndex)
    {
        StartTitleBarSelectionAnimation(previousSelectedIndex, _selectedIndex);
    }

    internal void InvalidateTitleBarLayout()
    {
        _hasTitleBarLayoutContext = false;
        _titleBarLayoutPageCount = -1;
        _titleBarTabRects.Clear();
        _titleBarTabWidthBuffer.Clear();
        _titleBarCloseButtonRect = SKRect.Empty;
        _titleBarNewTabButtonRect = SKRect.Empty;
    }

    internal void DrawTitleBarTabs(SKCanvas canvas, TabViewTitleBarLayoutContext context, SKColor foreColor, SKColor hoverColor, SKColor titleColor)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return;

        UpdateTitleBarLayout(context);
        UpdateTitleBarAuxiliaryRects();

        DrawTitleBarTabDividers(canvas, context, titleColor);

        var tabGap = ResolvedTabGap * ScaleFactor;

        float ComputeDragTabTarget(int tIdx)
        {
            var srcSlotWidth = GetTabPrimaryLength(_titleBarTabRects[_dragTabSourceIndex]) + tabGap;
            var adjIns = _dragTabInsertIndex > _dragTabSourceIndex ? _dragTabInsertIndex - 1 : _dragTabInsertIndex;
            var j = tIdx < _dragTabSourceIndex ? tIdx : tIdx - 1;
            return (tIdx > _dragTabSourceIndex ? -srcSlotWidth : 0f) + (j >= adjIns ? srcSlotWidth : 0f);
        }

        if (_isDraggingTab && _dragTabSourceIndex >= 0)
        {
            if (_tabDodgeAnimOffsets.Length != _titleBarTabRects.Count)
            {
                var prev = _tabDodgeAnimOffsets;
                _tabDodgeAnimOffsets = new float[_titleBarTabRects.Count];
                for (var k = 0; k < Math.Min(prev.Length, _tabDodgeAnimOffsets.Length); k++)
                    _tabDodgeAnimOffsets[k] = prev[k];
            }

            const float LerpFactor = 0.3f;
            const float SettleTolerance = 0.5f;
            var needsMoreFrames = false;

            for (var k = 0; k < _tabDodgeAnimOffsets.Length; k++)
            {
                if (k == _dragTabSourceIndex) { _tabDodgeAnimOffsets[k] = 0f; continue; }
                var target = _dragTabInsertIndex >= 0 ? ComputeDragTabTarget(k) : 0f;
                var delta = target - _tabDodgeAnimOffsets[k];
                if (MathF.Abs(delta) > SettleTolerance)
                {
                    _tabDodgeAnimOffsets[k] += delta * LerpFactor;
                    needsMoreFrames = true;
                }
                else _tabDodgeAnimOffsets[k] = target;
            }
            if (needsMoreFrames) Invalidate();
        }
        else
        {
            _tabDodgeAnimOffsets = Array.Empty<float>();
        }

        if (_selectedIndex < 0 || _selectedIndex >= _titleBarTabRects.Count)
            return;

        var effectiveHoverColor = titleColor != SKColor.Empty && !titleColor.IsDark()
            ? foreColor.WithAlpha(60)
            : hoverColor;
        EnsureTitleBarTabHoverState(_titleBarTabRects.Count);
        var animOffset = _isDraggingTab && _selectedIndex < _tabDodgeAnimOffsets.Length ? _tabDodgeAnimOffsets[_selectedIndex] : 0f;
        var selectedRect = OffsetTabRectAlongPrimaryAxis(GetTitleBarSelectedVisualRect(), animOffset);

        for (var hoverIndex = 0; hoverIndex < _titleBarTabRects.Count; hoverIndex++)
        {
            if (hoverIndex == _selectedIndex || (_isDraggingTab && hoverIndex == _dragTabSourceIndex))
                continue;

            var hoverProgress = GetTitleBarTabHoverProgress(hoverIndex, false);
            if (hoverProgress <= 0.001f)
                continue;

            var hoveredAnimOffset = _isDraggingTab && hoverIndex < _tabDodgeAnimOffsets.Length ? _tabDodgeAnimOffsets[hoverIndex] : 0f;
            DrawTitleBarTabSurface(canvas, OffsetTabRectAlongPrimaryAxis(_titleBarTabRects[hoverIndex], hoveredAnimOffset),
                false, true, hoverProgress, effectiveHoverColor, foreColor, titleColor);
        }

        if (!_isDraggingTab || _selectedIndex != _dragTabSourceIndex)
            DrawTitleBarTabSurface(canvas, selectedRect, true, false, 0f, effectiveHoverColor, foreColor, titleColor);
        PrepareTabFont((DrawTabIcons ? TitleBarTabFontSizeWithIcon : TitleBarTabFontSize).Topx(this));

        for (var pageIndex = 0; pageIndex < _titleBarTabRects.Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            if (_isDraggingTab && pageIndex == _dragTabSourceIndex)
                continue;

            var rect = _titleBarTabRects[pageIndex];
            if (_isDraggingTab && pageIndex < _tabDodgeAnimOffsets.Length)
                rect = OffsetTabRectAlongPrimaryAxis(rect, _tabDodgeAnimOffsets[pageIndex]);
            var iconRect = SKRect.Empty;
            var isSelected = pageIndex == _selectedIndex;
            var isHovered = pageIndex == _hoveredTitleBarTabIndex;
            var hoverProgress = GetTitleBarTabHoverProgress(pageIndex, isSelected);

            _tabTextPaint.Color = GetTitleBarTextColor(isSelected, isHovered, hoverProgress, foreColor);

            var titleBarPadding     = GetTitleBarTabHorizontalContentPadding();
            var titleBarIcon    = TitleBarTabIconSize * ScaleFactor;
            var titleBarIconSpacing = TitleBarTabIconSpacing * ScaleFactor;
            var hasTitleBarIcon = DrawTabIcons && page.HasImage;
            var titleBarTrailingReserve = pageIndex == _selectedIndex && _titleBarCloseButtonRect.Width > 0f
                ? _titleBarCloseButtonRect.Width + titleBarIconSpacing : 0f;

            var titleBarVerticalPadding = GetTitleBarTabVerticalContentPadding();
            (iconRect, var textRect) = ComputeTabContentRects(rect, page.Text, hasTitleBarIcon,
                titleBarPadding, titleBarVerticalPadding, titleBarIcon, titleBarIconSpacing, titleBarTrailingReserve);

            if (hasTitleBarIcon)
                page.RenderImageSlot(canvas, iconRect);

            TextRenderer.DrawText(canvas, page.Text ?? string.Empty, textRect, _tabTextPaint, _tabFont,
                TextAlign, true, false);
        }

        if (_isDraggingTab && _dragTabSourceIndex >= 0 && _dragTabSourceIndex < _titleBarTabRects.Count)
        {
            var srcRect  = _titleBarTabRects[_dragTabSourceIndex];
            var ghostLeft = Math.Clamp(
                _dragTabCurrentX - _dragTabGrabX,
                context.StartX,
                context.StartX + context.AvailableWidth - GetTabPrimaryLength(srcRect));
            var ghostRect = CreatePrimaryAxisRect(srcRect, ghostLeft);

            using var ghostLayerPaint = new SKPaint { Color = new SKColor(255, 255, 255, 210) };
            var layerSaved = canvas.SaveLayer(ghostLayerPaint);

            DrawTitleBarTabSurface(canvas, ghostRect, true, false, 0f, effectiveHoverColor, foreColor, titleColor);
            
            var ghostPage = GetPageAt(_dragTabSourceIndex);
            if (ghostPage != null) {
                var hasGhostIcon = DrawTabIcons && ghostPage.HasImage;
                var titleBarPadding = GetTitleBarTabHorizontalContentPadding();
                (var ghostIconRect, var ghostTextRect) = ComputeTabContentRects(
                    ghostRect, ghostPage.Text, hasGhostIcon, titleBarPadding, GetTitleBarTabVerticalContentPadding(), TitleBarTabIconSize * ScaleFactor, TitleBarTabIconSpacing * ScaleFactor, 0f);
                if (hasGhostIcon) ghostPage.RenderImageSlot(canvas, ghostIconRect);
                TextRenderer.DrawText(canvas, ghostPage.Text ?? string.Empty, ghostTextRect, _tabTextPaint, _tabFont, TextAlign, true, false);
            }
            canvas.RestoreToCount(layerSaved);
        }

        if (_titleBarCloseButtonRect.Width > 0)
            DrawTitleBarCloseButton(canvas, _titleBarCloseButtonRect, _hoveredTitleBarCloseButton, foreColor, effectiveHoverColor);

        if (_titleBarNewTabButtonRect.Width > 0)
            DrawTitleBarNewTabButton(canvas, _titleBarNewTabButtonRect, _hoveredTitleBarNewTabButton, foreColor, effectiveHoverColor);
    }

    internal bool TryGetTitleBarTabIndexAtPoint(SKPoint point, TabViewTitleBarLayoutContext context, out int tabIndex)
    {
        tabIndex = -1;

        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return false;

        UpdateTitleBarLayout(context);
        for (var i = 0; i < _titleBarTabRects.Count; i++)
        {
            if (_titleBarTabRects[i].Contains(point))
            {
                tabIndex = i;
                return true;
            }
        }

        return false;
    }

    internal bool IsPointOverTitleBarCloseButton(SKPoint point, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || !TabCloseButton)
            return false;

        UpdateTitleBarLayout(context);
        UpdateTitleBarAuxiliaryRects();
        return _titleBarCloseButtonRect.Contains(point);
    }

    internal bool IsPointOverTitleBarNewTabButton(SKPoint point, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || !NewTabButton)
            return false;

        UpdateTitleBarLayout(context);
        UpdateTitleBarAuxiliaryRects();
        return _titleBarNewTabButtonRect.Contains(point);
    }

    internal bool UpdateTitleBarHoverState(SKPoint point, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return ResetTitleBarHoverState();

        UpdateTitleBarLayout(context);
        UpdateTitleBarAuxiliaryRects();

        var hoveredTabIndex = TryGetTitleBarTabIndexAtPoint(point, context, out var tabIndex) ? tabIndex : -1;
        var hoveredCloseButton = _titleBarCloseButtonRect.Contains(point);
        var hoveredNewTabButton = _titleBarNewTabButtonRect.Contains(point);

        if (_hoveredTitleBarTabIndex == hoveredTabIndex &&
            _hoveredTitleBarCloseButton == hoveredCloseButton &&
            _hoveredTitleBarNewTabButton == hoveredNewTabButton)
            return false;

        if (_hoveredTitleBarTabIndex != hoveredTabIndex)
        {
            _hoveredTitleBarTabIndex = hoveredTabIndex;
            SetTitleBarTabHoverTarget(_titleBarTabRects.Count, hoveredTabIndex);
        }

        if (_hoveredTitleBarCloseButton != hoveredCloseButton)
        {
            _hoveredTitleBarCloseButton = hoveredCloseButton;
            _titleBarTabCloseHoverAnimation.StartNewAnimation(hoveredCloseButton ? AnimationDirection.In : AnimationDirection.Out);
        }

        if (_hoveredTitleBarNewTabButton != hoveredNewTabButton)
        {
            _hoveredTitleBarNewTabButton = hoveredNewTabButton;
            _titleBarNewTabHoverAnimation.StartNewAnimation(hoveredNewTabButton ? AnimationDirection.In : AnimationDirection.Out);
        }

        Invalidate();
        return true;
    }

    internal bool ProcessTitleBarMouseDown(MouseEventArgs e, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return false;

        UpdateTitleBarLayout(context);

        if (e.Button == MouseButtons.Left)
        {
            if (_titleBarNewTabButtonRect.Contains(e.Location))
            {
                RaiseNewTabButtonClick();
                return true;
            }

            if (_titleBarCloseButtonRect.Contains(e.Location))
            {
                return true; // Handle in mouse up
            }

            if (TryGetTitleBarTabIndexAtPoint(e.Location, context, out var tabIndex))
            {
                SelectedIndex = tabIndex;
                if (_allowTabDrag)
                {
                    _dragTabSourceIndex = tabIndex;
                    _dragTabGrabX = tabIndex < _titleBarTabRects.Count
                        ? GetTabPrimaryCoordinate(e.Location) - GetTabPrimaryStart(_titleBarTabRects[tabIndex])
                        : 0f;
                    _dragTabCurrentX = GetTabPrimaryCoordinate(e.Location);
                }
                return true;
            }
        }
        return false;
    }

    internal bool ProcessTitleBarMouseMove(MouseEventArgs e, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return false;

        if (_dragTabSourceIndex >= 0)
        {
            var pointerPrimary = GetTabPrimaryCoordinate(e.Location);
            var grabOriginX = _dragTabSourceIndex < _titleBarTabRects.Count
                ? GetTabPrimaryStart(_titleBarTabRects[_dragTabSourceIndex]) + _dragTabGrabX
                : _dragTabCurrentX;

            if (!_isDraggingTab && Math.Abs(pointerPrimary - grabOriginX) > TabDragThreshold * ScaleFactor)
                _isDraggingTab = true;

            if (_isDraggingTab)
            {
                _dragTabCurrentX = pointerPrimary;
                _dragTabInsertIndex = ComputeTitleBarDragInsertIndex(pointerPrimary);
                Invalidate();
            }
            return true;
        }

        return false;
    }

    private int ComputeTitleBarDragInsertIndex(float pointerPrimary)
    {
        for (var i = 0; i < _titleBarTabRects.Count; i++)
        {
            if (i == _dragTabSourceIndex) continue;
            var tabMid = GetTabPrimaryMidpoint(_titleBarTabRects[i]);
            if (pointerPrimary < tabMid)
                return i;
        }
        return Count;
    }

    internal bool ProcessTitleBarMouseUp(MouseEventArgs e, TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return false;

        if (_isDraggingTab && _dragTabSourceIndex >= 0 && _dragTabInsertIndex >= 0)
            CommitTabDrag(_dragTabSourceIndex, _dragTabInsertIndex);

        var handled = _dragTabSourceIndex >= 0 || _isDraggingTab;

        _isDraggingTab = false;
        _dragTabSourceIndex = -1;
        _dragTabInsertIndex = -1;
        _tabDodgeAnimOffsets = Array.Empty<float>();
        Invalidate();

        if (!handled && e.Button == MouseButtons.Left && _titleBarCloseButtonRect.Contains(e.Location) && _selectedIndex >= 0)
        {
            RaiseTabCloseButtonClick(_selectedIndex);
            handled = true;
        }

        return handled;
    }

    internal bool ResetTitleBarHoverState()
    {
        if (_hoveredTitleBarTabIndex < 0 && !_hoveredTitleBarCloseButton && !_hoveredTitleBarNewTabButton)
            return false;

        _hoveredTitleBarTabIndex = -1;
        SetTitleBarTabHoverTarget(_titleBarTabRects.Count, -1);

        if (_hoveredTitleBarCloseButton)
        {
            _hoveredTitleBarCloseButton = false;
            _titleBarTabCloseHoverAnimation.StartNewAnimation(AnimationDirection.Out);
        }

        if (_hoveredTitleBarNewTabButton)
        {
            _hoveredTitleBarNewTabButton = false;
            _titleBarNewTabHoverAnimation.StartNewAnimation(AnimationDirection.Out);
        }

        Invalidate();
        return true;
    }

    private void UpdateTitleBarLayout(TabViewTitleBarLayoutContext context)
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
        {
            InvalidateTitleBarLayout();
            return;
        }

        if (_hasTitleBarLayoutContext && _lastTitleBarLayoutContext == context && _titleBarLayoutPageCount == Count)
            return;

        _titleBarTabWidthBuffer.Clear();
        PrepareTabFont((DrawTabIcons ? TitleBarTabFontSizeWithIcon : TitleBarTabFontSize).Topx(this));

        var horizontalPadding = GetTitleBarTabHorizontalContentPadding();
        var titleBarIconSize    = TitleBarTabIconSize    * ScaleFactor;
        var titleBarIconSpacing = TitleBarTabIconSpacing * ScaleFactor;
        var closeButtonAllowance = TabCloseButton ? (TitleBarTabCloseButtonSize + TitleBarTabIconSpacing) * ScaleFactor : 0f;
        var newTabButtonSize = 24f * ScaleFactor;
        var newTabButtonGap = Math.Max(ResolvedTabGap * ScaleFactor, newTabButtonSize / 2f);
        var availableWidth = Math.Max(0f, context.AvailableWidth - (NewTabButton ? newTabButtonSize + newTabButtonGap : 0f));
        var customMetrics = _customTabStyle.HasValue ? _customTabStyle.Value.Metrics : default;
        var maxTabWidth = customMetrics.MaxWidth.HasValue
            ? Math.Max(0f, customMetrics.MaxWidth.Value * ScaleFactor)
            : Math.Max(0f, context.MaxTabWidth);
        var minTabWidth = Math.Max(0f, (customMetrics.MinWidth ?? 0f) * ScaleFactor);

        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var desiredWidth = TabViewTabGeometry.MeasureDesiredTabWidth(
                page,
                _tabFont,
                horizontalPadding,
                titleBarIconSize,
                titleBarIconSpacing,
                closeButtonAllowance,
                minTabWidth,
                maxTabWidth,
                DrawTabIcons,
                TabCloseButton,
                ImageAlign);

            _titleBarTabWidthBuffer.Add(desiredWidth);
        }

        var totalDesiredWidth = 0f;
        var gap = ResolvedTabGap * ScaleFactor;
        for (var i = 0; i < _titleBarTabWidthBuffer.Count; i++)
            totalDesiredWidth += _titleBarTabWidthBuffer[i];
        totalDesiredWidth += gap * MathF.Max(0f, _titleBarTabWidthBuffer.Count - 1);
        
        var clampedTotalWidth = MathF.Min(totalDesiredWidth, availableWidth);
        var startX = context.StartX;
        
        if (TabAlignment == TabViewAlignment.Center)
            startX += (availableWidth - clampedTotalWidth) / 2f;
        else if (TabAlignment == TabViewAlignment.End)
            startX += (availableWidth - clampedTotalWidth);

        TabViewTabGeometry.LayoutTabs(
            _titleBarTabWidthBuffer,
            startX,
            context.Top,
            context.Height,
            availableWidth,
            gap,
            maxTabWidth,
            false,
            _titleBarTabRects);

        _lastTitleBarLayoutContext = context;
        _hasTitleBarLayoutContext = true;
        _titleBarLayoutPageCount = Count;
    }

    internal float MeasureTitleBarRequiredHeight()
    {
        if (TabMode != TabViewMode.TitleBar || Count <= 0)
            return 0f;

        PrepareTabFont((DrawTabIcons ? TitleBarTabFontSizeWithIcon : TitleBarTabFontSize).Topx(this));
        var closeButtonSize = TabCloseButton ? TitleBarTabCloseButtonSize * ScaleFactor : 0f;
        var verticalPadding = GetTitleBarTabVerticalContentPadding();
        var iconSize = TitleBarTabIconSize * ScaleFactor;
        var iconSpacing = TitleBarTabIconSpacing * ScaleFactor;
        var customMetrics = _customTabStyle.HasValue ? _customTabStyle.Value.Metrics : default;
        var minHeight = (customMetrics.MinHeight ?? 0f) * ScaleFactor;
        var maxHeight = (customMetrics.MaxHeight ?? float.MaxValue) * ScaleFactor;
        var neededHeight = 0f;

        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            neededHeight = Math.Max(
                neededHeight,
                MeasureDesiredTabHeight(
                    page,
                    DrawTabIcons,
                    verticalPadding,
                    iconSize,
                    iconSpacing,
                    closeButtonSize,
                    minHeight,
                    maxHeight));
        }

        return MathF.Ceiling(neededHeight);
    }

    private void UpdateTitleBarAuxiliaryRects()
    {
        _titleBarCloseButtonRect = SKRect.Empty;
        _titleBarNewTabButtonRect = SKRect.Empty;

        if (_selectedIndex >= 0 && _selectedIndex < _titleBarTabRects.Count && TabCloseButton)
        {
            _titleBarCloseButtonRect = TabViewTabGeometry.CreateTrailingButtonRect(
                GetTitleBarSelectedVisualRect(),
                TitleBarTabCloseButtonSize * ScaleFactor,
                TitleBarTabCloseButtonInset * ScaleFactor,
                0f,
                1f);
        }

        if (NewTabButton && _titleBarTabRects.Count > 0)
        {
            var size = 24f * ScaleFactor;
            var gap = Math.Max(ResolvedTabGap * ScaleFactor, size / 2f);
            var lastTabRect = _titleBarTabRects[_titleBarTabRects.Count - 1];
            _titleBarNewTabButtonRect = SKRect.Create(
                lastTabRect.Right + gap,
                _lastTitleBarLayoutContext.CenterY - size / 2f,
                size,
                size);
        }
    }

    private SKRect GetTitleBarSelectedVisualRect()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _titleBarTabRects.Count)
            return SKRect.Empty;

        var activeRect = _titleBarTabRects[_selectedIndex];
        if (!_titleBarTabSelectionAnimation.IsAnimating() ||
            _titleBarPreviousSelectedIndex < 0 ||
            _titleBarPreviousSelectedIndex >= _titleBarTabRects.Count ||
            _titleBarPreviousSelectedIndex == _selectedIndex)
            return activeRect;

        return TabViewTabGeometry.InterpolateRect(
            _titleBarTabRects[_titleBarPreviousSelectedIndex],
            activeRect,
            Math.Clamp((float)_titleBarTabSelectionAnimation.GetProgress(), 0f, 1f));
    }

    private void DrawTitleBarDivider(SKCanvas canvas, TabViewTitleBarLayoutContext context, SKColor titleColor)
    {
        var dividerColor = titleColor != SKColor.Empty
            ? titleColor.Determine().WithAlpha(30)
            : ColorScheme.BorderColor;

        _tabBorderPaint.Color = dividerColor;
        _tabBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        var y = context.Bottom - (_tabBorderPaint.StrokeWidth / 2f);
        canvas.DrawLine(context.StartX, y, context.StartX + context.AvailableWidth, y, _tabBorderPaint);
    }

    private void DrawTitleBarTabDividers(SKCanvas canvas, TabViewTitleBarLayoutContext context, SKColor titleColor)
    {
        if (_titleBarTabRects.Count < 2 || ResolvedTabGap * ScaleFactor > 0.5f ||
            TabDesignMode is TabViewDesignMode.RoundedCompact or TabViewDesignMode.Pill or TabViewDesignMode.Minimal)
            return;

        var dividerColor = titleColor != SKColor.Empty
            ? titleColor.Determine().WithAlpha(42)
            : ColorScheme.Outline.WithAlpha(ColorScheme.IsDarkMode ? (byte)78 : (byte)62);

        _tabBorderPaint.Color = dividerColor;
        _tabBorderPaint.StrokeWidth = Math.Max(1f, ScaleFactor);

        var top = context.Top + 9f * ScaleFactor;
        var bottom = context.Bottom - 8f * ScaleFactor;

        for (var index = 0; index < _titleBarTabRects.Count - 1; index++)
        {
            if (index == _selectedIndex || index + 1 == _selectedIndex ||
                index == _hoveredTitleBarTabIndex || index + 1 == _hoveredTitleBarTabIndex)
                continue;

            var x = (_titleBarTabRects[index].Right + _titleBarTabRects[index + 1].Left) * 0.5f;
            canvas.DrawLine(x, top, x, bottom, _tabBorderPaint);
        }
    }

    private SKColor GetTitleBarTextColor(bool isSelected, bool isHovered, float hoverProgress, SKColor foreColor)
    {
        if (!Enabled)
            return foreColor.WithAlpha(156);

        if (_customTabStyle.HasValue)
        {
            var style = _customTabStyle.Value;
            if (isSelected)
                return ResolveCustomForeground(style.Selected.ForegroundColor, style.Selected.BackgroundColor, foreColor);

            var normalText = ResolveCustomForeground(style.Normal.ForegroundColor, style.Normal.BackgroundColor, foreColor.WithAlpha(194));
            if (style.Hover.ForegroundColor == SKColors.Empty)
                return normalText;

            var hoverText = ResolveCustomForeground(style.Hover.ForegroundColor, style.Hover.BackgroundColor, foreColor.WithAlpha(232));
            return normalText.InterpolateColor(hoverText, Math.Clamp(hoverProgress, 0f, 1f));
        }

        if (isSelected && TabDesignMode == TabViewDesignMode.Pill)
            return ColorScheme.Primary.Determine();

        if (isSelected && TabDesignMode == TabViewDesignMode.Minimal)
            return ColorScheme.Primary;

        if (isSelected)
            return foreColor;

        if (isHovered)
            return foreColor.WithAlpha(232);

        return foreColor.WithAlpha(194);
    }

    private void DrawTitleBarTabSurface(SKCanvas canvas, SKRect rect, bool isSelected, bool isHovered, float hoverProgress, SKColor hoverColor, SKColor foreColor, SKColor titleColor)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var isDark = ColorScheme.IsDarkMode;
        var isLightTitle = titleColor != SKColor.Empty && !titleColor.IsDark();
        var sf = ScaleFactor;

        var selectedBg = (isDark ? ColorScheme.SurfaceContainerHigh : ColorScheme.Surface)
            .WithAlpha(titleColor == SKColor.Empty ? (byte)255 : (byte)150);
        var hoverBg = foreColor.WithAlpha(isLightTitle ? (byte)14 : (byte)18);
        var backgroundColor = isSelected
            ? selectedBg
            : SKColors.Transparent.InterpolateColor(hoverBg, hoverProgress);
        var borderColor = isSelected
            ? ColorScheme.Outline.WithAlpha(isDark ? (byte)90 : (byte)68)
            : SKColors.Transparent.InterpolateColor(ColorScheme.Outline.WithAlpha(isDark ? (byte)36 : (byte)28), hoverProgress);

        _tabBackgroundPaint.Color = backgroundColor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        if (TryDrawCustomTabBackground(canvas, rect, isSelected, isHovered, hoverProgress,
            SKColors.Transparent, hoverBg, selectedBg,
            SKColors.Transparent, ColorScheme.Outline.WithAlpha(isDark ? (byte)36 : (byte)28),
            ColorScheme.Outline.WithAlpha(isDark ? (byte)90 : (byte)68),
            MathF.Max(2f, MathF.Round(3f * sf))))
            return;

        switch (TabDesignMode)
        {
            case TabViewDesignMode.Rectangle:
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

            case TabViewDesignMode.Rounded:
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

            case TabViewDesignMode.RoundedCompact:
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

            case TabViewDesignMode.Outlined:
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

            case TabViewDesignMode.Pill:
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

            case TabViewDesignMode.Minimal:
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
                    : backgroundColor;
                canvas.DrawRect(minRect, _tabBackgroundPaint);
                if (isSelected)
                {
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    var indH = MathF.Max(2f, MathF.Round(3f * sf));
                    canvas.DrawRect(minRect.Left, minRect.Bottom - indH, minRect.Width, indH, _tabIndicatorPaint);
                }
                break;
            }

            case TabViewDesignMode.Fluent:
            {
                if (!isSelected && !isHovered)
                    break;
                var fluentRect = new SKRect(
                    MathF.Round(rect.Left + 4f * sf),
                    MathF.Round(rect.Top + 6f * sf),
                    MathF.Round(rect.Right - 4f * sf),
                    MathF.Round(rect.Bottom - 4f * sf));
                _tabBackgroundPaint.Color = isSelected ? selectedBg : backgroundColor;
                canvas.DrawRoundRect(fluentRect, 4f * sf, 4f * sf, _tabBackgroundPaint);
                if (isSelected)
                {
                    _tabIndicatorPaint.Color = ColorScheme.Primary;
                    canvas.DrawRect(fluentRect.Left + 8f * sf, fluentRect.Bottom - 2f * sf, fluentRect.Width - 16f * sf, 2f * sf, _tabIndicatorPaint);
                }
                break;
            }

            case TabViewDesignMode.MacOS:
            {
                if (!isSelected && !isHovered)
                    break;
                var macRect = new SKRect(
                    MathF.Round(rect.Left + 2f * sf),
                    MathF.Round(rect.Top + 4f * sf),
                    MathF.Round(rect.Right - 2f * sf),
                    MathF.Round(rect.Bottom - 4f * sf));
                _tabBackgroundPaint.Color = isSelected ? selectedBg : backgroundColor;
                canvas.DrawRoundRect(macRect, macRect.Height / 2f, macRect.Height / 2f, _tabBackgroundPaint);
                if (isSelected)
                {
                    _tabBorderPaint.Color = ColorScheme.BorderColor;
                    canvas.DrawRoundRect(macRect, macRect.Height / 2f, macRect.Height / 2f, _tabBorderPaint);
                }
                break;
            }

            case TabViewDesignMode.Chromed:
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
                TabViewTabGeometry.BuildTopRoundedTabPath(_tabPath, chromedRect, MathF.Round(12f * sf));
                canvas.DrawPath(_tabPath, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawPath(_tabPath, _tabBorderPaint);
                break;
            }
        }
    }

    private void DrawTitleBarCloseButton(SKCanvas canvas, SKRect rect, bool isHovered, SKColor foreColor, SKColor hoverColor)
    {
        var sf = ScaleFactor;
        var progress = Math.Clamp((float)_titleBarTabCloseHoverAnimation.GetProgress(), 0f, 1f);
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
        var linePaint = PrepareTabGlyphPaint(foreColor.WithAlpha(isHovered ? (byte)255 : (byte)222), stroke, isAntialias: true);

        var size = MathF.Round(3.5f * sf);
        canvas.DrawLine(midX - size, midY - size, midX + size, midY + size, linePaint);
        canvas.DrawLine(midX - size, midY + size, midX + size, midY - size, linePaint);
    }

    private void DrawTitleBarNewTabButton(SKCanvas canvas, SKRect rect, bool isHovered, SKColor foreColor, SKColor hoverColor)
    {
        var sf = ScaleFactor;
        var progress = Math.Clamp((float)_titleBarNewTabHoverAnimation.GetProgress(), 0f, 1f);
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
        var linePaint = PrepareTabGlyphPaint(foreColor.WithAlpha(isHovered ? (byte)255 : (byte)228), stroke, isAntialias: false);

        var size = MathF.Round(5f * sf);
        var midX = MathF.Round(roundedRect.MidX) + crispOffset;
        var midY = MathF.Round(roundedRect.MidY) + crispOffset;
        
        canvas.DrawLine(midX - size, midY, midX + size, midY, linePaint);
        canvas.DrawLine(midX, midY - size, midX, midY + size, linePaint);
    }

    private SKPaint PrepareTabGlyphPaint(SKColor color, float strokeWidth, bool isAntialias)
    {
        _tabGlyphPaint.Shader = null;
        _tabGlyphPaint.ColorFilter = null;
        _tabGlyphPaint.PathEffect = null;
        _tabGlyphPaint.MaskFilter = null;
        _tabGlyphPaint.ImageFilter = null;
        _tabGlyphPaint.BlendMode = SKBlendMode.SrcOver;
        _tabGlyphPaint.IsAntialias = isAntialias;
        _tabGlyphPaint.Style = SKPaintStyle.Stroke;
        _tabGlyphPaint.StrokeCap = SKStrokeCap.Round;
        _tabGlyphPaint.Color = color;
        _tabGlyphPaint.StrokeWidth = strokeWidth;
        return _tabGlyphPaint;
    }

    private void DrawTabHeaderSurface(SKCanvas canvas, SKRect headerRect, SKColor backgroundColor, SKColor borderColor, SKRect activeTabsRect)
    {
        var sf = ScaleFactor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        if (UsesNonTopEmbeddedTabLayout)
        {
            if (backgroundColor != SKColors.Transparent)
            {
                _tabBackgroundPaint.Color = backgroundColor;
                switch (TabDesignMode)
                {
                    case TabViewDesignMode.Rounded:
                    case TabViewDesignMode.RoundedCompact:
                    case TabViewDesignMode.Pill:
                    case TabViewDesignMode.MacOS:
                    {
                        if (activeTabsRect.Width > 0 && activeTabsRect.Height > 0)
                        {
                            var pad = MathF.Round((TabDesignMode == TabViewDesignMode.MacOS ? 6f : 4f) * sf);
                            var wrapRect = new SKRect(
                                activeTabsRect.Left - pad,
                                activeTabsRect.Top - pad,
                                activeTabsRect.Right + pad,
                                activeTabsRect.Bottom + pad);
                            var wrapRadius = TabDesignMode == TabViewDesignMode.MacOS
                                ? MathF.Min(wrapRect.Width, wrapRect.Height) * 0.12f
                                : MathF.Round(10f * sf);
                            canvas.DrawRoundRect(wrapRect, wrapRadius, wrapRadius, _tabBackgroundPaint);
                        }

                        break;
                    }

                    case TabViewDesignMode.Fluent:
                    {
                        DrawFluentTabSurface(canvas, headerRect, MathF.Round(8f * sf), backgroundColor, false);
                        break;
                    }

                    default:
                    {
                        canvas.DrawRect(headerRect, _tabBackgroundPaint);
                        break;
                    }
                }
            }

            switch (_tabLayoutMode)
            {
                case TabViewLayoutMode.Left:
                {
                    var dividerX = MathF.Round(headerRect.Right) - _tabBorderPaint.StrokeWidth * 0.5f;
                    canvas.DrawLine(dividerX, MathF.Round(headerRect.Top), dividerX, MathF.Round(headerRect.Bottom), _tabBorderPaint);
                    break;
                }
                case TabViewLayoutMode.Right:
                {
                    var dividerX = MathF.Round(headerRect.Left) + _tabBorderPaint.StrokeWidth * 0.5f;
                    canvas.DrawLine(dividerX, MathF.Round(headerRect.Top), dividerX, MathF.Round(headerRect.Bottom), _tabBorderPaint);
                    break;
                }
                case TabViewLayoutMode.Bottom:
                {
                    var dividerY = MathF.Round(headerRect.Top) + _tabBorderPaint.StrokeWidth * 0.5f;
                    canvas.DrawLine(MathF.Round(headerRect.Left), dividerY, MathF.Round(headerRect.Right), dividerY, _tabBorderPaint);
                    break;
                }
            }

            return;
        }

        if (backgroundColor != SKColors.Transparent)
        {
            _tabBackgroundPaint.Color = backgroundColor;
            switch (TabDesignMode)
            {
                case TabViewDesignMode.Rounded:
                case TabViewDesignMode.RoundedCompact:
                case TabViewDesignMode.Pill:
                {
                    if (activeTabsRect.Width > 0 && activeTabsRect.Height > 0)
                    {
                        var padX = MathF.Round(4f * sf);
                        var padY = MathF.Round(4f * sf);
                        var wrapRect = new SKRect(
                            activeTabsRect.Left - padX,
                            activeTabsRect.Top - padY,
                            activeTabsRect.Right + padX,
                            activeTabsRect.Bottom + padY);

                        var wrapRadius = MathF.Round(10f * sf);
                        canvas.DrawRoundRect(wrapRect, wrapRadius, wrapRadius, _tabBackgroundPaint);
                    }
                    break;
                }
                
                default:
                {
                    canvas.DrawRect(headerRect, _tabBackgroundPaint);
                    break;
                }
            }
        }

        switch (TabDesignMode)
        {
            case TabViewDesignMode.Rectangle:
            case TabViewDesignMode.Chromed:
            case TabViewDesignMode.Outlined:
            case TabViewDesignMode.Minimal:
            {
                var divY = MathF.Round(headerRect.Bottom) - _tabBorderPaint.StrokeWidth * 0.5f;
                canvas.DrawLine(MathF.Round(headerRect.Left), divY, MathF.Round(headerRect.Right), divY, _tabBorderPaint);
                break;
            }

            case TabViewDesignMode.Rounded:
            case TabViewDesignMode.RoundedCompact:
            default:
                break;
        }
    }

    private void DrawTabStripResizer(SKCanvas canvas)
    {
        var resizerBounds = GetTabStripResizerRect();
        if (resizerBounds.Width <= 0f || resizerBounds.Height <= 0f)
            return;

        var active = _hoveredTabStripResizer || _isResizingTabStrip;
        var progress = _isResizingTabStrip
            ? 1f
            : Math.Clamp((float)_tabStripResizerAnimation.GetProgress(), 0f, 1f);
        if (!active && progress <= 0.001f)
            return;

        var visualWidth = MathF.Min(TabStripResizerVisualThickness * ScaleFactor, resizerBounds.Width);
        var maxLineHeight = MathF.Min(TabStripResizerVisualLength * ScaleFactor, resizerBounds.Height - 18f * ScaleFactor);
        if (visualWidth <= 0f || maxLineHeight <= 0f)
            return;

        var collapsedLineHeight = 8f * ScaleFactor;
        var lineHeight = collapsedLineHeight + ((maxLineHeight - collapsedLineHeight) * progress);
        var visualRect = SKRect.Create(
            resizerBounds.MidX - visualWidth / 2f,
            resizerBounds.MidY - lineHeight / 2f,
            visualWidth,
            lineHeight);

        var radius = visualRect.Width * 0.5f;

        _tabBackgroundPaint.Color = ColorScheme.Primary.WithAlpha((byte)Math.Clamp(MathF.Round(228f * progress), 0f, 255f));
        canvas.DrawRoundRect(visualRect, radius, radius, _tabBackgroundPaint);
    }

    private void DrawTabBackground(SKCanvas canvas, SKRect rect, bool isSelected, bool isHovered,
        SKColor inactiveBackground, SKColor selectedBackground, SKColor hoverBackground,
        SKColor inactiveBorderColor, SKColor selectedBorderColor, float indicatorHeight, float hoverProgress)
    {
        var sf = ScaleFactor;
        isHovered = isHovered || hoverProgress > 0.001f;
        var backgroundColor = isSelected
            ? selectedBackground
            : inactiveBackground.InterpolateColor(hoverBackground, hoverProgress);
        var borderColor = isSelected
            ? selectedBorderColor
            : inactiveBorderColor;

        _tabBackgroundPaint.Color = backgroundColor;
        _tabBorderPaint.Color = borderColor;
        _tabBorderPaint.StrokeWidth = MathF.Max(1f, MathF.Round(sf));

        if (TryDrawCustomTabBackground(canvas, rect, isSelected, isHovered, hoverProgress,
            inactiveBackground, hoverBackground, selectedBackground,
            inactiveBorderColor, inactiveBorderColor, selectedBorderColor,
            indicatorHeight))
            return;

        switch (TabDesignMode)
        {
            case TabViewDesignMode.Rectangle:
            {
                if (isHovered && !isSelected)
                {
                    var ghostRect = new SKRect(
                        MathF.Round(rect.Left + 2f * sf), MathF.Round(rect.Top + 2f * sf),
                        MathF.Round(rect.Right - 2f * sf), MathF.Round(rect.Bottom - 2f * sf));
                    var radius = MathF.Round(6f * sf);
                    canvas.DrawRoundRect(ghostRect, radius, radius, _tabBackgroundPaint);
                }

                if (isSelected)
                    DrawContentEdgeIndicator(canvas, rect, MathF.Max(2f, MathF.Round(2.5f * sf)), ColorScheme.Primary);

                break;
            }

            case TabViewDesignMode.Pill:
            {
                if (!isSelected && !isHovered)
                    break;

                var inset = MathF.Round(4f * sf);
                var pillRect = new SKRect(
                    MathF.Round(rect.Left + inset), MathF.Round(rect.Top + inset),
                    MathF.Round(rect.Right - inset), MathF.Round(rect.Bottom - inset));
                var radius = MathF.Min(pillRect.Width, pillRect.Height) * 0.5f;
                canvas.DrawRoundRect(pillRect, radius, radius, _tabBackgroundPaint);
                break;
            }

            case TabViewDesignMode.Minimal:
            {
                if (!isSelected && !isHovered)
                    break;

                canvas.DrawRect(rect, _tabBackgroundPaint);
                if (isSelected)
                    DrawContentEdgeIndicator(canvas, rect, MathF.Max(2f, MathF.Round(3f * sf)), borderColor == SKColors.Transparent ? ColorScheme.Primary : borderColor);

                break;
            }

            case TabViewDesignMode.Outlined:
            {
                if (!isSelected && !isHovered)
                    break;

                var outlineRect = new SKRect(
                    MathF.Round(rect.Left + sf), MathF.Round(rect.Top + sf),
                    MathF.Round(rect.Right - sf), MathF.Round(rect.Bottom - sf));
                var radius = MathF.Round(7f * sf);
                canvas.DrawRoundRect(outlineRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawRoundRect(outlineRect, radius, radius, _tabBorderPaint);
                break;
            }

            case TabViewDesignMode.Rounded:
            {
                if (!isSelected && !isHovered)
                    break;

                var vIn = MathF.Round(2.5f * sf);
                var hIn = MathF.Round(2f * sf);
                var pillRect = new SKRect(
                    MathF.Round(rect.Left + hIn), MathF.Round(rect.Top + vIn),
                    MathF.Round(rect.Right - hIn), MathF.Round(rect.Bottom - vIn));
                var radius = MathF.Round(8f * sf);
                canvas.DrawRoundRect(pillRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawRoundRect(pillRect, radius, radius, _tabBorderPaint);
                break;
            }

            case TabViewDesignMode.RoundedCompact:
            {
                if (!isSelected && !isHovered)
                    break;

                var vIn = MathF.Round(3f * sf);
                var hIn = MathF.Round(3f * sf);
                var cardRect = new SKRect(
                    MathF.Round(rect.Left + hIn), MathF.Round(rect.Top + vIn),
                    MathF.Round(rect.Right - hIn), MathF.Round(rect.Bottom - vIn));
                var radius = MathF.Round(6f * sf);
                canvas.DrawRoundRect(cardRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawRoundRect(cardRect, radius, radius, _tabBorderPaint);
                break;
            }

            case TabViewDesignMode.Fluent:
            {
                if (!isSelected && !isHovered)
                    break;

                var fluentRect = new SKRect(
                    MathF.Round(rect.Left + 4f * sf), MathF.Round(rect.Top + 4f * sf),
                    MathF.Round(rect.Right - 4f * sf), MathF.Round(rect.Bottom - 4f * sf));
                var radius = MathF.Round(6f * sf);
                DrawFluentTabSurface(canvas, fluentRect, radius, backgroundColor, isSelected);
                if (isSelected)
                    DrawContentEdgeIndicator(canvas, rect, MathF.Max(2f, MathF.Round(2.5f * sf)), ColorScheme.Primary);
                break;
            }

            case TabViewDesignMode.MacOS:
            {
                if (!isSelected && !isHovered)
                    break;

                var macRect = new SKRect(
                    MathF.Round(rect.Left + 2f * sf), MathF.Round(rect.Top + 4f * sf),
                    MathF.Round(rect.Right - 2f * sf), MathF.Round(rect.Bottom - 4f * sf));
                var radius = MathF.Min(macRect.Width, macRect.Height) * 0.5f;
                canvas.DrawRoundRect(macRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                    canvas.DrawRoundRect(macRect, radius, radius, _tabBorderPaint);
                break;
            }

            case TabViewDesignMode.Chromed:
            default:
            {
                if (!isSelected && !isHovered)
                    break;

                var inset = MathF.Round(isSelected ? 1f * sf : 4f * sf);
                var chromedRect = new SKRect(
                    MathF.Round(rect.Left + inset), MathF.Round(rect.Top + 3f * sf),
                    MathF.Round(rect.Right - inset), MathF.Round(rect.Bottom - 3f * sf));
                var radius = MathF.Round(10f * sf);
                canvas.DrawRoundRect(chromedRect, radius, radius, _tabBackgroundPaint);
                if (isSelected)
                {
                    canvas.DrawRoundRect(chromedRect, radius, radius, _tabBorderPaint);
                    DrawContentEdgeIndicator(canvas, rect, MathF.Max(1f, MathF.Round(1.5f * sf)), selectedBorderColor);
                }
                break;
            }
        }
    }

    private void DrawFluentTabSurface(SKCanvas canvas, SKRect rect, float radius, SKColor baseColor, bool elevated)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
            return;

        var surfaceAlpha = baseColor.Alpha;
        var topTint = ColorScheme.IsDarkMode
            ? baseColor.WithAlpha((byte)Math.Min(255, surfaceAlpha + (elevated ? 18 : 8)))
            : elevated
                ? SKColors.White.WithAlpha(210)
                : baseColor.WithAlpha((byte)Math.Min(255, surfaceAlpha + 10));
        var bottomTint = ColorScheme.IsDarkMode
            ? baseColor.WithAlpha((byte)Math.Max(0, surfaceAlpha - (elevated ? 10 : 4)))
            : baseColor;

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Bottom),
                new[] { topTint, bottomTint },
                new[] { 0f, 1f },
                SKShaderTileMode.Clamp)
        };

        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        var revealAlpha = elevated
            ? (ColorScheme.IsDarkMode ? (byte)42 : (byte)170)
            : (byte)Math.Clamp(MathF.Round(surfaceAlpha * (ColorScheme.IsDarkMode ? 0.8f : 0.55f)), 0f, ColorScheme.IsDarkMode ? 24f : 52f);
        using var revealPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(1f, MathF.Round(ScaleFactor)),
            Color = SKColors.White.WithAlpha(revealAlpha)
        };
        canvas.DrawRoundRect(rect, radius, radius, revealPaint);

        if (!elevated)
            return;

        var shineRect = new SKRect(
            rect.Left + MathF.Round(8f * ScaleFactor),
            rect.Top + MathF.Round(4f * ScaleFactor),
            rect.Right - MathF.Round(8f * ScaleFactor),
            rect.Top + MathF.Round(5f * ScaleFactor));
        if (shineRect.Width <= 0f || shineRect.Height <= 0f)
            return;

        using var shinePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White.WithAlpha(ColorScheme.IsDarkMode ? (byte)36 : (byte)120)
        };
        canvas.DrawRoundRect(shineRect, shineRect.Height * 0.5f, shineRect.Height * 0.5f, shinePaint);
    }

    private void DrawContentEdgeIndicator(SKCanvas canvas, SKRect rect, float thickness, SKColor color)
    {
        var inset = MathF.Round(8f * ScaleFactor);
        SKRect indicatorRect;

        switch (_tabLayoutMode)
        {
            case TabViewLayoutMode.Left:
                indicatorRect = SKRect.Create(rect.Right - thickness, rect.Top + inset, thickness, Math.Max(0f, rect.Height - inset * 2f));
                break;
            case TabViewLayoutMode.Right:
                indicatorRect = SKRect.Create(rect.Left, rect.Top + inset, thickness, Math.Max(0f, rect.Height - inset * 2f));
                break;
            case TabViewLayoutMode.Bottom:
                indicatorRect = SKRect.Create(rect.Left + inset, rect.Top, Math.Max(0f, rect.Width - inset * 2f), thickness);
                break;
            default:
                indicatorRect = SKRect.Create(rect.Left + inset, rect.Bottom - thickness, Math.Max(0f, rect.Width - inset * 2f), thickness);
                break;
        }

        if (indicatorRect.Width <= 0f || indicatorRect.Height <= 0f)
            return;

        _tabIndicatorPaint.Color = color;
        canvas.DrawRoundRect(indicatorRect, thickness * 0.5f, thickness * 0.5f, _tabIndicatorPaint);
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

        var xPaint = PrepareTabGlyphPaint(
            foreground.WithAlpha(isHovered ? (byte)220 : (byte)150),
            MathF.Max(1f, MathF.Round(1.5f * sf)),
            isAntialias: true);
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

        var headerThickness = Math.Min(
            UsesVerticalTabLayout ? rect.Width : rect.Height,
            GetTabHeaderThickness());

        return _tabLayoutMode switch
        {
            TabViewLayoutMode.Left => new SKRect(rect.Left, rect.Top, Math.Min(rect.Right, rect.Left + headerThickness), rect.Bottom),
            TabViewLayoutMode.Right => new SKRect(Math.Max(rect.Left, rect.Right - headerThickness), rect.Top, rect.Right, rect.Bottom),
            TabViewLayoutMode.Bottom => new SKRect(rect.Left, Math.Max(rect.Top, rect.Bottom - headerThickness), rect.Right, rect.Bottom),
            _ => new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + headerThickness)
        };
    }

    private SKRect GetTabStripResizerRect()
    {
        if (!ShouldDrawTabStripResizer)
            return SKRect.Empty;

        var headerRect = GetTabHeaderRect();
        var resizerThickness = GetTabStripResizerThickness();
        if (headerRect.Width <= 0f || headerRect.Height <= 0f || resizerThickness <= 0f)
            return SKRect.Empty;

        var halfThickness = resizerThickness / 2f;

        return _tabLayoutMode switch
        {
            TabViewLayoutMode.Left => new SKRect(
                headerRect.Right - halfThickness,
                headerRect.Top,
                headerRect.Right + halfThickness,
                headerRect.Bottom),
            TabViewLayoutMode.Right => new SKRect(
                headerRect.Left - halfThickness,
                headerRect.Top,
                headerRect.Left + halfThickness,
                headerRect.Bottom),
            _ => SKRect.Empty,
        };
    }

    private float GetTabHeaderThickness()
    {
        var minimumThickness = (UsesVerticalTabLayout ? _verticalTabStripWidth : _tabStripHeight) * ScaleFactor;

        if (!UsesVerticalTabLayout)
            return MeasureHorizontalTabHeaderThickness(minimumThickness);

        if (Count <= 0)
            return minimumThickness;

        return MeasureVerticalTabHeaderThickness(minimumThickness);
    }

    private float MeasureHorizontalTabHeaderThickness(float minimumThickness)
    {
        if (!ShouldDrawTabIcons)
            return minimumThickness;

        var isHorizontalIcon = ImageAlign is
            ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight;
        if (isHorizontalIcon)
            return minimumThickness;

        PrepareTabFont();
        var iconSize    = TabIconSize    * ScaleFactor;
        var iconSpacing = TabIconSpacing * ScaleFactor;
        var vertInset   = GetTabVerticalContentPadding();

        // blockH = iconSize + iconSpacing + textH via MeasureContentBlockSize (text=null › just font metrics)
        var (_, blockH) = TabViewTabGeometry.MeasureContentBlockSize(
            null, true, _tabFont, iconSize, iconSpacing, ContentAlignment.TopCenter);

        // Header height = blockH + 4*vertInset
        // (LayoutTabs will strip 2*vertInset at strip level › tab rect height = blockH + 2*vertInset)
        // (ComputeTabContentRects will strip 1*vertInset per side › available area = blockH) ?
        var needed = blockH + 4f * vertInset;
        return Math.Max(minimumThickness, MathF.Ceiling(needed));
    }

    private float GetTabStripResizerThickness()
    {
        return ShouldDrawTabStripResizer ? TabStripResizerThickness * ScaleFactor : 0f;
    }

    private float MeasureVerticalTabHeaderThickness(float minimumThickness)
    {
        PrepareTabFont();

        var axisPadding = Math.Max(4f * ScaleFactor, GetTabVerticalContentPadding());
        var horizontalPadding = GetTabHorizontalContentPadding();
        var minWidth = Math.Max(VerticalTabMinWidth * ScaleFactor, minimumThickness);
        var maxWidth = Math.Max(VerticalTabMaxWidth * ScaleFactor, minWidth);
        var iconSize    = TabIconSize    * ScaleFactor;
        var iconSpacing = TabIconSpacing  * ScaleFactor;
        var closeButtonSize = TabCloseButtonSize * ScaleFactor;
        var closeButtonSpacing = TabCloseButtonSpacing * ScaleFactor;
        var closeButtonAllowance = ShouldDrawTabCloseButtons ? closeButtonSize + closeButtonSpacing : 0f;
        var requiredThickness = minimumThickness;

        for (var pageIndex = 0; pageIndex < Count; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var desiredWidth = TabViewTabGeometry.MeasureDesiredTabWidth(
                page,
                _tabFont,
                horizontalPadding,
                iconSize,
                iconSpacing,
                closeButtonAllowance,
                minWidth,
                maxWidth,
                ShouldDrawTabIcons,
                ShouldDrawTabCloseButtons,
                ImageAlign);

            requiredThickness = Math.Max(requiredThickness, desiredWidth + axisPadding * 2f);
        }

        if (ShouldDrawNewTabButton)
            requiredThickness = Math.Max(requiredThickness, (NewTabButtonSize * ScaleFactor) + axisPadding * 2f);

        return requiredThickness;
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
        Application.ApplyPreferredFontRendering(_tabFont);
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
        var horizontalPadding = GetTabHorizontalContentPadding();
        var verticalInset = GetTabVerticalContentPadding();
        var customMetrics = _customTabStyle.HasValue ? _customTabStyle.Value.Metrics : default;
        var minWidth = (customMetrics.MinWidth ?? TabMinWidth) * ScaleFactor;
        var maxWidth = Math.Max(minWidth, (customMetrics.MaxWidth ?? TabMaxWidth) * ScaleFactor);
        var iconSize    = TabIconSize    * ScaleFactor;
        var iconSpacing = TabIconSpacing  * ScaleFactor;
        var closeButtonSize = TabCloseButtonSize * ScaleFactor;
        var closeButtonSpacing = TabCloseButtonSpacing * ScaleFactor;
        var closeButtonAllowance = ShouldDrawTabCloseButtons ? closeButtonSize + closeButtonSpacing : 0f;
        var newTabButtonSize = NewTabButtonSize * ScaleFactor;
        var newTabReserve = ShouldDrawNewTabButton ? newTabButtonSize + gap : 0f;

        if (UsesVerticalTabLayout)
        {
            var axisPadding = Math.Max(4f * ScaleFactor, verticalInset);
            var tabWidth = Math.Max(0f, headerRect.Width - (axisPadding * 2f));
            var contentHeight = Math.Max(0f, headerRect.Height - (axisPadding * 2f) - newTabReserve);
            var verticalPadding = verticalInset * 2f;
            var minHeight = (customMetrics.MinHeight ?? VerticalTabMinHeight) * ScaleFactor;
            var maxHeight = Math.Max(minHeight, (customMetrics.MaxHeight ?? VerticalTabMaxHeight) * ScaleFactor);

            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var page = GetPageAt(pageIndex);
                if (page == null)
                    continue;

                _tabWidthBuffer.Add(MeasureDesiredTabHeight(
                    page,
                    ShouldDrawTabIcons,
                    verticalPadding,
                    iconSize,
                    iconSpacing,
                    closeButtonSize,
                    minHeight,
                    maxHeight));
            }

            var totalTabHeight = 0f;
            for (var i = 0; i < _tabWidthBuffer.Count; i++)
                totalTabHeight += _tabWidthBuffer[i];
            totalTabHeight += gap * MathF.Max(0f, _tabWidthBuffer.Count - 1);

            UpdateVerticalTabScrollMetrics(totalTabHeight, contentHeight);

            var startY = _verticalTabScrollableExtent > 0.01f
                ? headerRect.Top + axisPadding - _verticalTabScrollOffset
                : ComputeTabStartY(headerRect, axisPadding, contentHeight, gap);

            _tabRects.Clear();
            var currentY = startY;
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var page = GetPageAt(pageIndex);
                if (page == null)
                    continue;

                var bufferIndex = _tabRects.Count;
                var slotHeight = _tabWidthBuffer[bufferIndex];
                var slotRect = SKRect.Create(headerRect.Left + axisPadding, currentY, tabWidth, slotHeight);
                _tabRects.Add(slotRect);
                currentY += slotHeight + gap;
            }

            if (_tabRects.Count > 0)
                currentY = _tabRects[_tabRects.Count - 1].Bottom + gap;

            for (var pageIndex = 0; pageIndex < _tabRects.Count; pageIndex++)
                _tabCloseButtonRects.Add(CreateTabCloseButtonRect(_tabRects[pageIndex], closeButtonSize, horizontalPadding));

            if (ShouldDrawNewTabButton)
            {
                var pinnedNewButtonTop = headerRect.Bottom - axisPadding - newTabButtonSize;
                var newButtonTop = _verticalTabScrollableExtent > 0.01f
                    ? pinnedNewButtonTop
                    : Math.Min(currentY, pinnedNewButtonTop);
                _newTabButtonRect = SKRect.Create(
                    headerRect.MidX - newTabButtonSize / 2f,
                    newButtonTop,
                    newTabButtonSize,
                    newTabButtonSize);
            }

            return;
        }

        var contentWidth = Math.Max(0f, headerRect.Width - (horizontalPadding * 2f) - newTabReserve);
    ResetVerticalTabScroll();

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var page = GetPageAt(pageIndex);
            if (page == null)
                continue;

            var width = TabViewTabGeometry.MeasureDesiredTabWidth(page, _tabFont, horizontalPadding,
                iconSize, iconSpacing, closeButtonAllowance, minWidth, maxWidth,
                ShouldDrawTabIcons, ShouldDrawTabCloseButtons, ImageAlign);
            _tabWidthBuffer.Add(width);
        }

        var startX = ComputeTabStartX(headerRect, horizontalPadding, newTabReserve, contentWidth, gap);

        TabViewTabGeometry.LayoutTabs(_tabWidthBuffer,
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

    private float GetVerticalTabHeight(float iconSize, float iconSpacing, float closeButtonAllowance)
    {
        var closeButtonW = Math.Max(0f, closeButtonAllowance - (TabCloseButtonSpacing * ScaleFactor));

        var (_, blockH) = TabViewTabGeometry.MeasureContentBlockSize(
            null, ShouldDrawTabIcons, _tabFont, iconSize, iconSpacing, ImageAlign);

        var contentH = Math.Max(blockH, closeButtonW);
        var customMinHeight = _customTabStyle.HasValue ? _customTabStyle.Value.Metrics.MinHeight : null;
        return Math.Max((customMinHeight ?? VerticalTabMinHeight) * ScaleFactor, MathF.Ceiling(contentH + GetTabVerticalContentPadding() * 4f));
    }

    private float MeasureDesiredTabHeight(ElementBase page, bool includeIcon, float verticalPadding,
        float iconSize, float iconSpacing, float trailingButtonSize, float minHeight, float maxHeight)
    {
        var hasIcon = includeIcon && page.HasImage;
        return TabViewTabGeometry.MeasureDesiredTabHeight(
            page.Text,
            hasIcon,
            _tabFont,
            verticalPadding,
            iconSize,
            iconSpacing,
            trailingButtonSize,
            minHeight,
            maxHeight,
            ImageAlign);
    }

    private bool TryBeginTabStripResize(SKPoint point)
    {
        if (!IsPointOverTabStripResizer(point))
            return false;

        _isResizingTabStrip = true;
        _hoveredTabStripResizer = true;
        _tabStripResizeOrigin = point.X;
        _tabStripResizeStartHeight = _verticalTabStripWidth;
        _tabStripResizerAnimation.SetProgress(1d);
        Cursor = GetTabStripResizerCursor();
        GetParentWindow()?.UpdateCursor(this);
        Invalidate();
        return true;
    }

    private void UpdateTabStripResize(SKPoint point)
    {
        if (!_isResizingTabStrip)
            return;

        var scale = Math.Max(ScaleFactor, 1f);
        var pixelDelta = _tabLayoutMode == TabViewLayoutMode.Right
            ? _tabStripResizeOrigin - point.X
            : point.X - _tabStripResizeOrigin;
        var logicalDelta = pixelDelta / scale;
        var nextThickness = Math.Clamp(
            _tabStripResizeStartHeight + logicalDelta,
            VerticalTabMinWidth,
            GetMaximumVerticalTabStripWidth());

        if (Math.Abs(nextThickness - _verticalTabStripWidth) >= 0.25f)
            SetVerticalTabStripWidth(nextThickness);

        Cursor = GetTabStripResizerCursor();
    }

    private void UpdateVerticalTabScrollMetrics(float totalTabHeight, float viewportHeight)
    {
        _verticalTabScrollableExtent = Math.Max(0f, totalTabHeight - viewportHeight);
        _verticalTabScrollOffset = _verticalTabScrollableExtent <= 0.01f
            ? 0f
            : Math.Clamp(_verticalTabScrollOffset, 0f, _verticalTabScrollableExtent);
    }

    private void EnsureSelectedVerticalTabVisible()
    {
        if (!ShouldDrawTabStrip || !UsesVerticalTabLayout || _selectedIndex < 0 || Count <= 0)
        {
            ResetVerticalTabScroll();
            return;
        }

        var headerRect = GetTabHeaderRect();
        if (headerRect.Width <= 0f || headerRect.Height <= 0f)
            return;

        var gap = ResolvedTabGap * ScaleFactor;
        var axisPadding = Math.Max(4f * ScaleFactor, TabVerticalInset * ScaleFactor);
        var newTabReserve = ShouldDrawNewTabButton ? (NewTabButtonSize * ScaleFactor) + gap : 0f;
        var viewportHeight = Math.Max(0f, headerRect.Height - (axisPadding * 2f) - newTabReserve);

        UpdateTabRects();
        if (_verticalTabScrollableExtent <= 0.01f)
            return;

        if (_selectedIndex >= _tabRects.Count)
            return;

        var viewportTop = headerRect.Top + axisPadding;
        var viewportBottom = viewportTop + viewportHeight;
        var selectedRect = _tabRects[_selectedIndex];
        var nextOffset = _verticalTabScrollOffset;

        if (selectedRect.Top < viewportTop)
            nextOffset -= viewportTop - selectedRect.Top;
        else if (selectedRect.Bottom > viewportBottom)
            nextOffset += selectedRect.Bottom - viewportBottom;

        _verticalTabScrollOffset = Math.Clamp(nextOffset, 0f, _verticalTabScrollableExtent);
    }

    private void SetVerticalTabScrollOffset(float value)
    {
        var clamped = Math.Clamp(value, 0f, _verticalTabScrollableExtent);
        if (Math.Abs(_verticalTabScrollOffset - clamped) < 0.01f)
            return;

        _verticalTabScrollOffset = clamped;
        Invalidate();
    }

    private float GetVerticalTabScrollStep()
    {
        var step = _tabRects.Count > 0
            ? _tabRects[0].Height + (ResolvedTabGap * ScaleFactor)
            : VerticalTabMinHeight * ScaleFactor;

        return Math.Max(12f * ScaleFactor, step);
    }

    private void ResetVerticalTabScroll()
    {
        _verticalTabScrollOffset = 0f;
        _verticalTabScrollableExtent = 0f;
    }

    private float GetMaximumVerticalTabStripWidth()
    {
        var scale = Math.Max(ScaleFactor, 1f);
        var availableWidth = Math.Max(VerticalTabMinWidth, base.DisplayRectangle.Width / scale);
        return Math.Max(VerticalTabMinWidth, Math.Min(VerticalTabStripResizeMaxWidth, availableWidth - 96f));
    }

    private bool IsPointOverTabStripResizer(SKPoint point)
    {
        var rect = GetTabStripResizerRect();
        return rect.Width > 0f && rect.Height > 0f && rect.Contains(point);
    }

    private Cursor GetTabStripResizerCursor()
    {
        return _tabLayoutMode switch
        {
            TabViewLayoutMode.Left or TabViewLayoutMode.Right => Cursors.SizeWE,
            TabViewLayoutMode.Bottom => Cursors.SizeNS,
            _ => Cursors.Default,
        };
    }

    private void ResetTabStripResizerInteraction()
    {
        _hoveredTabStripResizer = false;
        _isResizingTabStrip = false;
        _tabStripResizeOrigin = 0f;
        _tabStripResizeStartHeight = 0f;
        _tabStripResizerAnimation.SetProgress(0d);
        Cursor = Cursors.Default;
    }

    private (SKRect iconRect, SKRect textRect) ComputeTabContentRects(
        SKRect tabRect, string? text, bool hasIcon,
        float horizontalPadding, float verticalPadding,
        float iconSize, float iconSpacing, float trailingReserve)
    {
        var availLeft   = tabRect.Left   + horizontalPadding;
        var availRight  = Math.Max(tabRect.Left + horizontalPadding,
                                   tabRect.Right - horizontalPadding - trailingReserve);
        var availTop    = tabRect.Top    + verticalPadding;
        var availBottom = Math.Max(tabRect.Top + verticalPadding,
                                   tabRect.Bottom - verticalPadding);
        var hasText = !string.IsNullOrEmpty(text);
        var metrics = _tabFont.Metrics;
        var textH = Math.Max(1f, metrics.Descent - metrics.Ascent);

        if (!hasIcon)
        {
            if (!hasText)
                return (SKRect.Empty, SKRect.Empty);

            var textOnly = new SKRect(availLeft, availTop, availRight, availBottom);
            return (SKRect.Empty, TabViewTabGeometry.EnsureEllipsisTextRect(textOnly));
        }

        if (!hasText)
        {
            var iconOnly = CreateAlignedRect(ImageAlign, availLeft, availRight, availTop, availBottom, iconSize, iconSize);
            return (iconOnly, SKRect.Empty);
        }

        var imageHorizontalGroup = GetAlignmentHorizontalGroup(ImageAlign);
        var textHorizontalGroup = GetAlignmentHorizontalGroup(TextAlign);
        var imageVerticalGroup = GetAlignmentVerticalGroup(ImageAlign);
        var textVerticalGroup = GetAlignmentVerticalGroup(TextAlign);

        var splitVertically = imageVerticalGroup != textVerticalGroup
            ? true
            : imageHorizontalGroup != textHorizontalGroup
                ? false
                : ImageAlign is not (ContentAlignment.MiddleLeft or ContentAlignment.MiddleRight);

        return splitVertically
            ? ComputeVerticalTabContentRects(availLeft, availRight, availTop, availBottom, iconSize, iconSpacing,
                textH, imageHorizontalGroup, imageVerticalGroup, textVerticalGroup)
            : ComputeHorizontalTabContentRects(availLeft, availRight, availTop, availBottom, iconSize, iconSpacing,
                imageHorizontalGroup, textHorizontalGroup);
    }

    private (SKRect iconRect, SKRect textRect) ComputeHorizontalTabContentRects(
        float left, float right, float top, float bottom,
        float iconSize, float iconSpacing,
        int imageHorizontalGroup, int textHorizontalGroup)
    {
        var availableWidth = Math.Max(0f, right - left);
        var iconWidth = Math.Min(iconSize, availableWidth);
        var remainingWidth = Math.Max(0f, availableWidth - iconWidth);
        var spacing = ResolveInterItemSpacing(remainingWidth, iconSpacing);
        var textWidth = Math.Max(0f, remainingWidth - spacing);
        var iconFirst = ResolvePrimaryOrder(imageHorizontalGroup, textHorizontalGroup);

        float iconSlotLeft;
        float iconSlotRight;
        SKRect textRect;

        if (iconFirst)
        {
            iconSlotLeft = left;
            iconSlotRight = left + iconWidth;
            textRect = new SKRect(
                Math.Min(right, iconSlotRight + spacing),
                top,
                right,
                bottom);
        }
        else
        {
            iconSlotLeft = Math.Max(left, right - iconWidth);
            iconSlotRight = right;
            textRect = new SKRect(
                left,
                top,
                Math.Max(left, iconSlotLeft - spacing),
                bottom);
        }

        var iconRect = CreateAlignedRect(ImageAlign, iconSlotLeft, iconSlotRight, top, bottom, iconSize, iconSize);
        return (iconRect, TabViewTabGeometry.EnsureEllipsisTextRect(textRect));
    }

    private (SKRect iconRect, SKRect textRect) ComputeVerticalTabContentRects(
        float left, float right, float top, float bottom,
        float iconSize, float iconSpacing, float textHeight,
        int imageHorizontalGroup, int imageVerticalGroup, int textVerticalGroup)
    {
        var availableHeight = Math.Max(0f, bottom - top);
        var iconHeight = Math.Min(iconSize, availableHeight);
        var remainingHeight = Math.Max(0f, availableHeight - iconHeight);
        var spacing = ResolveInterItemSpacing(remainingHeight, iconSpacing);
        var iconFirst = ResolvePrimaryOrder(imageVerticalGroup, textVerticalGroup);

        float iconSlotTop;
        float iconSlotBottom;
        SKRect textRect;

        if (iconFirst)
        {
            iconSlotTop = top;
            iconSlotBottom = top + iconHeight;
            textRect = new SKRect(
                left,
                Math.Min(bottom, iconSlotBottom + spacing),
                right,
                bottom);
        }
        else
        {
            iconSlotTop = Math.Max(top, bottom - iconHeight);
            iconSlotBottom = bottom;
            textRect = new SKRect(
                left,
                top,
                right,
                Math.Max(top, iconSlotTop - spacing));
        }

        if (textRect.Height < textHeight && availableHeight > iconHeight)
        {
            var adjustedSpacing = Math.Max(0f, availableHeight - iconHeight - textHeight);
            if (iconFirst)
            {
                textRect = new SKRect(left, Math.Min(bottom, iconSlotBottom + adjustedSpacing), right, bottom);
            }
            else
            {
                textRect = new SKRect(left, top, right, Math.Max(top, iconSlotTop - adjustedSpacing));
            }
        }

        var iconAlign = imageHorizontalGroup switch
        {
            < 0 when imageVerticalGroup < 0 => ContentAlignment.TopLeft,
            < 0 when imageVerticalGroup > 0 => ContentAlignment.BottomLeft,
            > 0 when imageVerticalGroup < 0 => ContentAlignment.TopRight,
            > 0 when imageVerticalGroup > 0 => ContentAlignment.BottomRight,
            < 0 => ContentAlignment.MiddleLeft,
            > 0 => ContentAlignment.MiddleRight,
            _ when imageVerticalGroup < 0 => ContentAlignment.TopCenter,
            _ when imageVerticalGroup > 0 => ContentAlignment.BottomCenter,
            _ => ContentAlignment.MiddleCenter,
        };

        var iconRect = CreateAlignedRect(iconAlign, left, right, iconSlotTop, iconSlotBottom, iconSize, iconSize);
        return (iconRect, TabViewTabGeometry.EnsureEllipsisTextRect(textRect));
    }

    private static bool ResolvePrimaryOrder(int imageGroup, int textGroup)
    {
        if (imageGroup < textGroup)
            return true;

        if (imageGroup > textGroup)
            return false;

        return imageGroup != 1;
    }

    private static float ResolveInterItemSpacing(float remainingSpace, float desiredSpacing)
    {
        if (remainingSpace <= 1f)
            return 0f;

        return Math.Min(desiredSpacing, remainingSpace - 1f);
    }

    private static SKRect CreateAlignedRect(ContentAlignment align, float left, float right, float top, float bottom, float width, float height)
    {
        var availableWidth = Math.Max(0f, right - left);
        var availableHeight = Math.Max(0f, bottom - top);
        var clampedWidth = Math.Clamp(width, 0f, availableWidth);
        var clampedHeight = Math.Clamp(height, 0f, availableHeight);
        var alignedLeft = Math.Clamp(
            AlignH(align, left, right, clampedWidth),
            left,
            Math.Max(left, right - clampedWidth));
        var alignedTop = Math.Clamp(
            AlignV(align, top, bottom, clampedHeight),
            top,
            Math.Max(top, bottom - clampedHeight));
        return SKRect.Create(alignedLeft, alignedTop, clampedWidth, clampedHeight);
    }

    private static int GetAlignmentHorizontalGroup(ContentAlignment alignment)
    {
        return alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => -1,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => 1,
            _ => 0,
        };
    }

    private static int GetAlignmentVerticalGroup(ContentAlignment alignment)
    {
        return alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => -1,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => 1,
            _ => 0,
        };
    }


    private static float AlignH(ContentAlignment align, float left, float right, float contentW)
    {
        return align switch
        {
            ContentAlignment.TopLeft    or ContentAlignment.MiddleLeft    or ContentAlignment.BottomLeft    => left,
            ContentAlignment.TopRight   or ContentAlignment.MiddleRight   or ContentAlignment.BottomRight   => right - contentW,
            _ => left + (right - left - contentW) * 0.5f,
        };
    }

    private static float AlignV(ContentAlignment align, float top, float bottom, float contentH)
    {
        return align switch
        {
            ContentAlignment.TopLeft    or ContentAlignment.TopCenter    or ContentAlignment.TopRight    => top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => bottom - contentH,
            _ => top + (bottom - top - contentH) * 0.5f,
        };
    }

    private float ComputeTabStartX(SKRect headerRect, float horizontalPadding, float newTabReserve, float contentWidth, float gap)
    {
        if (_tabAlignment == TabViewAlignment.Start)
            return headerRect.Left + horizontalPadding;

        var totalTabWidth = 0f;
        for (var i = 0; i < _tabWidthBuffer.Count; i++)
            totalTabWidth += _tabWidthBuffer[i];
        totalTabWidth += gap * MathF.Max(0f, _tabWidthBuffer.Count - 1);
        totalTabWidth = MathF.Min(totalTabWidth, contentWidth);

        return _tabAlignment switch
        {
            TabViewAlignment.Center => headerRect.Left + horizontalPadding + (contentWidth - totalTabWidth) / 2f,
            TabViewAlignment.End    => headerRect.Left + horizontalPadding + (contentWidth - totalTabWidth),
            _                             => headerRect.Left + horizontalPadding
        };
    }

    private float ComputeTabStartY(SKRect headerRect, float verticalPadding, float contentHeight, float gap)
    {
        if (_tabAlignment == TabViewAlignment.Start)
            return headerRect.Top + verticalPadding;

        var totalTabHeight = 0f;
        for (var i = 0; i < _tabWidthBuffer.Count; i++)
            totalTabHeight += _tabWidthBuffer[i];
        totalTabHeight += gap * MathF.Max(0f, _tabWidthBuffer.Count - 1);
        totalTabHeight = MathF.Min(totalTabHeight, contentHeight);

        return _tabAlignment switch
        {
            TabViewAlignment.Center => headerRect.Top + verticalPadding + (contentHeight - totalTabHeight) / 2f,
            TabViewAlignment.End    => headerRect.Top + verticalPadding + (contentHeight - totalTabHeight),
            _                             => headerRect.Top + verticalPadding
        };
    }

    private SKRect CreateTabCloseButtonRect(SKRect tabRect, float preferredSize, float horizontalPadding)
    {
        if (!ShouldDrawTabCloseButtons)
            return SKRect.Empty;

        return TabViewTabGeometry.CreateTrailingButtonRect(tabRect, preferredSize, horizontalPadding, 10f * ScaleFactor);
    }

    private void StartTabSelectionAnimation(int previousSelectedIndex, int nextSelectedIndex)
    {
        if (TabMode != TabViewMode.Embedded || previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
        {
            _previousSelectedIndex = nextSelectedIndex;
            _tabSelectionAnimation.SetProgress(1);
            return;
        }

        _previousSelectedIndex = previousSelectedIndex;
        _tabSelectionAnimation.SetProgress(0);
        _tabSelectionAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void StartTitleBarSelectionAnimation(int previousSelectedIndex, int nextSelectedIndex)
    {
        if (TabMode != TabViewMode.TitleBar || previousSelectedIndex < 0 || nextSelectedIndex < 0 || previousSelectedIndex == nextSelectedIndex)
        {
            _titleBarPreviousSelectedIndex = nextSelectedIndex;
            _titleBarTabSelectionAnimation.SetProgress(1);
            return;
        }

        _titleBarPreviousSelectedIndex = previousSelectedIndex;
        _titleBarTabSelectionAnimation.SetProgress(0);
        _titleBarTabSelectionAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void ResetTabSelectionAnimation()
    {
        _previousSelectedIndex = _selectedIndex;
        _tabSelectionAnimation.SetProgress(1);
    }

    private void ResetTitleBarState()
    {
        _titleBarPreviousSelectedIndex = _selectedIndex;
        _titleBarTabSelectionAnimation.SetProgress(1);
        ResetTitleBarHoverState();
        InvalidateTitleBarLayout();
    }

    private void InvalidateTabChrome()
    {
        InvalidateTitleBarLayout();
        if (GetParentWindow() is Window hostWindow)
            hostWindow.RefreshTitleBarTabsHostLayout();
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
        if (_transitionViewport.Width > 0f && _transitionViewport.Height > 0f)
            return _transitionViewport;

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

        _transitionViewport = SKRect.Empty;
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
        _transitionViewport = SKRect.Empty;
        InvalidateRenderTree();
    }

    private static double ValidateIncrement(double value)
    {
        return value <= 0 ? 0.01 : value;
    }
}
