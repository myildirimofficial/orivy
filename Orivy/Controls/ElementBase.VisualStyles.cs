using Orivy.Animation;
using Orivy.Styling;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Orivy.Controls;

public abstract partial class ElementBase
{
    private readonly AnimationManager _visualStyleAnimation = new(true);
    private ElementVisualStyleSnapshot _styleAnimationFrom;
    private ElementVisualStyleSnapshot _styleAnimationTo;
    private ElementVisualTransitionDescriptor _styleAnimationTransition;
    private ElementVisualStyleSnapshot _styleBaseSnapshot;
    private ElementVisualStyleSnapshot _styleEffectiveSnapshot;
    private readonly List<VisualStyleConfiguration> _visualStyleConfigurations = new();
    private readonly List<ElementBase> _visualStyleStateDependents = new();
    private bool _isReplayingVisualStyleConfigurations;
    private int _visualStyleMutationDepth;
    private bool _hasVisualStyleBaseOverride;
    private bool _styleBaseOverridesWidth;
    private bool _styleBaseOverridesHeight;
    private bool _isPressed;
    protected bool _isPointerOver;
    private bool _visualStylesEnabled;
    private bool _visualStylesInitialized;

    [Browsable(false)]
    public bool IsPressed => _isPressed;

    [Browsable(false)]
    public bool IsPointerOver => _isPointerOver;

    [Browsable(false)]
    internal bool UseDefaultPointerVisualStates { get; set; } = true;

    [Browsable(false)]
    public ElementVisualStateContext VisualState => CreateVisualStateContext();

    [Browsable(false)]
    public ElementVisualStyleCollection VisualStyles { get; }

    [Browsable(false)]
    public ElementVisualTransition VisualTransition { get; } = new();

    [Browsable(false)]
    public bool VisualStylesEnabled => _visualStylesEnabled;

    public ElementBase ConfigureVisualStyles(Action<ElementVisualStyleBuilder> configure, bool clearExisting = false)
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!_isReplayingVisualStyleConfigurations)
        {
            if (clearExisting)
                _visualStyleConfigurations.Clear();

            _visualStyleConfigurations.Add(new VisualStyleConfiguration(configure, clearExisting));
        }

        // Batch rule Clear/Add without per-item RefreshVisualStyles — InsertItem used to
        // re-enter ResolveVisualStyleSnapshot while the collection was still being rebuilt
        // (ThemeChanged → Replay → Configure → OnHover Add → NRE on a half-built list).
        _visualStyleMutationDepth++;
        try
        {
            var builder = new ElementVisualStyleBuilder(this);
            if (clearExisting)
                builder.ClearRules();

            configure(builder);
        }
        finally
        {
            _visualStyleMutationDepth--;
        }

        RefreshVisualStyles();
        return this;
    }

    public void ClearVisualStyles()
    {
        if (!_isReplayingVisualStyleConfigurations)
            _visualStyleConfigurations.Clear();

        VisualStyles.Clear();
        _hasVisualStyleBaseOverride = false;
        _styleBaseOverridesWidth = false;
        _styleBaseOverridesHeight = false;
        UpdateVisualStylesEnabledState();
        if (!_visualStylesEnabled)
            return;

        RefreshVisualStyles(forceImmediate: true);
    }

    private void InitializeVisualStyleSystem()
    {
        _styleBaseSnapshot = CaptureCurrentSnapshot();
        _styleEffectiveSnapshot = _styleBaseSnapshot;
        _styleAnimationFrom = _styleBaseSnapshot;
        _styleAnimationTo = _styleBaseSnapshot;
        _styleAnimationTransition = VisualTransition.ToDescriptor();

        _visualStyleAnimation.AnimationType = VisualTransition.AnimationType;
        _visualStyleAnimation.Increment = VisualTransition.GetIncrement();
        _visualStyleAnimation.SecondaryIncrement = _visualStyleAnimation.Increment;
        _visualStyleAnimation.InterruptAnimation = true;
        _visualStyleAnimation.OnAnimationProgress += HandleVisualStyleAnimationProgress;
        _visualStyleAnimation.OnAnimationFinished += HandleVisualStyleAnimationFinished;

        _visualStylesInitialized = true;
    }

    internal void OnVisualStyleDefinitionsChanged()
    {
        UpdateVisualStylesEnabledState();
        if (!_visualStylesEnabled)
            return;

        // Defer refresh until the outer ConfigureVisualStyles / Replay batch finishes.
        if (_visualStyleMutationDepth > 0 || _isReplayingVisualStyleConfigurations)
            return;

        RefreshVisualStyles();
    }

    public void ReevaluateVisualStyles()
    {
        if (!_visualStylesEnabled)
            return;

        RefreshVisualStyles();
    }

    internal void RegisterVisualStyleStateDependent(ElementBase dependent)
    {
        if (dependent == null || ReferenceEquals(dependent, this) || _visualStyleStateDependents.Contains(dependent))
            return;

        _visualStyleStateDependents.Add(dependent);
    }

    internal void RefreshVisualStylesForThemeChange()
    {
        if (!_visualStylesInitialized)
            return;

        _isPressed = false;
        ReplayVisualStyleConfigurations();

        if (!_visualStylesEnabled)
            return;

        // ColorScheme already lerps Surface for ~220 ms and raises ThemeChanged every tick.
        // Restarting each control's 140 ms ease on every tick nests interpolations and flashes.
        // Snap styles to the current palette so they track the shared lerp.
        RefreshVisualStyles(forceImmediate: true);
    }

    public void ApplyVisualStyleBase(ElementVisualStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        var snapshot = _styleBaseSnapshot;
        style.ApplyTo(ref snapshot);
        ApplyStickyVisualColors(ref snapshot, allowPointerTint: false);
        _styleBaseSnapshot = snapshot;
        _hasVisualStyleBaseOverride = true;
        _styleBaseOverridesWidth |= style.Width.HasValue;
        _styleBaseOverridesHeight |= style.Height.HasValue;
        UpdateVisualStylesEnabledState();

        RefreshVisualStyles(forceImmediate: true);
    }

    /// <summary>
    /// Colors assigned through property setters (designer / InitializeComponent) are sticky across
    /// theme replay. Style-driven writes go through <see cref="ApplyEffectiveVisualStyle"/> and do
    /// not mark stickiness, so CheckBox/Radio/Button theme bases keep updating until the user
    /// assigns a color explicitly.
    /// </summary>
    private void MarkStickyBackColor(SKColor color)
    {
        if (_isReplayingVisualStyleConfigurations)
            return;

        _stickyBackColor = true;
        _stickyBackColorValue = color;
    }

    private void MarkStickyForeColor(SKColor color)
    {
        if (_isReplayingVisualStyleConfigurations)
            return;

        _stickyForeColor = true;
        _stickyForeColorValue = color;
    }

    private void MarkStickyBorderColor(SKColor color)
    {
        if (_isReplayingVisualStyleConfigurations)
            return;

        _stickyBorderColor = true;
        _stickyBorderColorValue = color;
    }

    private void ApplyStickyVisualColors(ref ElementVisualStyleSnapshot snapshot, bool allowPointerTint)
    {
        if (_stickyBackColor)
        {
            var fill = _stickyBackColorValue;
            if (allowPointerTint && Enabled && fill.Alpha > 0)
            {
                if (_isPressed)
                    fill = fill.Brightness(-0.08f);
                else if (_isPointerOver)
                    fill = fill.Brightness(0.06f);
            }

            snapshot = snapshot.WithBackColor(fill);
        }

        if (_stickyForeColor)
            snapshot = snapshot.WithForeColor(_stickyForeColorValue);

        if (_stickyBorderColor)
            snapshot = snapshot.WithBorderColor(_stickyBorderColorValue);
    }

    private void RefreshVisualStyles(bool forceImmediate = false)
    {
        if (!_visualStylesInitialized || !_visualStylesEnabled)
            return;

        var targetTransition = VisualTransition;
        var targetSnapshot = ResolveVisualStyleSnapshot(ref targetTransition);
        ApplyOrStartVisualStyleTransition(targetSnapshot, targetTransition.ToDescriptor(), forceImmediate);
    }

    private ElementVisualStyleSnapshot ResolveVisualStyleSnapshot(ref ElementVisualTransition transition)
    {
        var snapshot = _styleBaseSnapshot;
        // Keep layout size in the snapshot so focus transitions do not interpolate Width/Height.
        if (!_styleBaseOverridesWidth && !_styleBaseOverridesHeight)
            snapshot = snapshot.WithSize(_size);

        var state = CreateVisualStateContext();

        for (var i = 0; i < VisualStyles.Count; i++)
        {
            var rule = VisualStyles[i];
            if (rule?.Style == null)
                continue;

            if (!rule.Matches(this, state))
                continue;

            rule.Style.ApplyTo(ref snapshot);
            if (rule.Transition != null)
                transition = rule.Transition;
        }

        ApplyStickyVisualColors(ref snapshot, allowPointerTint: true);
        return snapshot;
    }

    private ElementVisualStateContext CreateVisualStateContext()
    {
        var states = ElementVisualStates.None;

        if (_isPointerOver)
            states |= ElementVisualStates.PointerOver;

        if (_isPressed)
            states |= ElementVisualStates.Pressed;

        if (Focused)
            states |= ElementVisualStates.Focused;

        if (!Enabled)
            states |= ElementVisualStates.Disabled;

        if (!Visible)
            states |= ElementVisualStates.Hidden;

        if (HasValidationError)
            states |= ElementVisualStates.Invalid;

        if (GetVisualCheckedState())
            states |= ElementVisualStates.Checked;

        return new ElementVisualStateContext(this, states);
    }

    protected virtual bool GetVisualCheckedState() => false;

    private void HandleVisualStyleAnimationProgress(object _)
    {
        var snapshot = ElementVisualStyleInterpolator.Interpolate(
            _styleAnimationFrom,
            _styleAnimationTo,
            (float)_visualStyleAnimation.GetProgress());
        ApplyEffectiveVisualStyle(snapshot);
    }

    private void HandleVisualStyleAnimationFinished(object _)
    {
        ApplyEffectiveVisualStyle(_styleAnimationTo);
        OnVisualStyleTransitionCompleted();
    }

    public virtual void  OnVisualStyleTransitionCompleted() { }

    private void ApplyOrStartVisualStyleTransition(
        in ElementVisualStyleSnapshot targetSnapshot,
        ElementVisualTransitionDescriptor targetTransition,
        bool forceImmediate)
    {
        var transitionChanged = _styleAnimationTransition != targetTransition;
        var allowReplayOnReevaluate = targetTransition.Mode == ElementVisualTransitionMode.ReplayOnReevaluate;
        var targetUnchanged = _styleAnimationTo.Equals(targetSnapshot);

        if (!transitionChanged && targetUnchanged && !_visualStyleAnimation.IsAnimating())
        {
            if (!allowReplayOnReevaluate || _styleEffectiveSnapshot.Equals(targetSnapshot))
                return;
        }

        _styleAnimationTo = targetSnapshot;
        _styleAnimationTransition = targetTransition;

        if (forceImmediate || !targetTransition.Enabled || targetTransition.Duration <= TimeSpan.Zero)
        {
            _visualStyleAnimation.SetProgress(1);
            ApplyEffectiveVisualStyle(targetSnapshot);
            return;
        }

        _styleAnimationFrom = _styleEffectiveSnapshot;
        if (_styleAnimationFrom.Equals(targetSnapshot))
            return;

        _visualStyleAnimation.AnimationType = targetTransition.AnimationType;
        _visualStyleAnimation.Increment = targetTransition.GetIncrement();
        _visualStyleAnimation.SecondaryIncrement = _visualStyleAnimation.Increment;
        _visualStyleAnimation.SetProgress(0);
        _visualStyleAnimation.StartNewAnimation(AnimationDirection.In);
    }

    private void ApplyEffectiveVisualStyle(in ElementVisualStyleSnapshot snapshot)
    {
        var previousSize = _size;
        var previousBackColor = _backColor;
        var previousForeColor = _foreColor;
        var previousBorder = _border;
        var previousBorderColor = _borderColor;
        var previousRadius = _radius;
        var previousShadows = _shadows;
        var previousOpacity = _opacity;
        var previousTranslateX = _renderTranslateX;
        var previousTranslateY = _renderTranslateY;
        var previousScaleX = _renderScaleX;
        var previousScaleY = _renderScaleY;

        // Layout owns Size unless a style explicitly sets Width/Height. Applying interpolated
        // snapshot sizes made TextBox/ComboBox animate wider on focus (slide/grow to the right).
        if (_styleBaseOverridesWidth || _styleBaseOverridesHeight)
            _size = snapshot.Size;
        else
            _size = previousSize;

        _backColor = snapshot.BackColor;
        _foreColor = snapshot.ForeColor;
        _border = snapshot.Border;
        _borderColor = snapshot.BorderColor;
        _radius = snapshot.Radius;
        _shadows = ElementVisualStyleInterpolator.CloneShadows(snapshot.Shadows);
        _opacity = snapshot.Opacity;
        _renderTranslateX = snapshot.TranslateX;
        _renderTranslateY = snapshot.TranslateY;
        _renderScaleX = snapshot.ScaleX;
        _renderScaleY = snapshot.ScaleY;
        _styleEffectiveSnapshot = new ElementVisualStyleSnapshot(
            _size,
            _backColor,
            _foreColor,
            _border,
            _borderColor,
            _radius,
            _shadows,
            _opacity,
            _renderTranslateX,
            _renderTranslateY,
            _renderScaleX,
            _renderScaleY);

        var sizeChanged = previousSize != _size;
        var visualsChanged = previousBackColor != _backColor ||
                             previousForeColor != _foreColor ||
                             previousBorder != _border ||
                             previousBorderColor != _borderColor ||
                             previousRadius != _radius ||
                             Math.Abs(previousOpacity - _opacity) > 0.0001f ||
                             Math.Abs(previousTranslateX - _renderTranslateX) > 0.01f ||
                             Math.Abs(previousTranslateY - _renderTranslateY) > 0.01f ||
                             Math.Abs(previousScaleX - _renderScaleX) > 0.001f ||
                             Math.Abs(previousScaleY - _renderScaleY) > 0.001f ||
                             !ElementVisualStyleInterpolator.AreShadowsEqual(previousShadows, _shadows);

        if (!CanReceivePointerInput())
            ClearPointerStateRecursive();

        if (sizeChanged)
            HandleEffectiveSizeChanged();
        else if (visualsChanged)
            Invalidate();
    }

    private bool CanReceivePointerInput()
    {
        return Visible && Enabled && Width > 0 && Height > 0 && Opacity > 0.01f;
    }

    private void ClearPointerStateRecursive()
    {
        var stateChanged = false;

        if (_isPointerOver)
        {
            _isPointerOver = false;
            stateChanged = true;
        }

        if (_isPressed)
        {
            _isPressed = false;
            stateChanged = true;
        }

        _lastHoveredElement = null!;

        for (var i = 0; i < Controls.Count; i++)
        {
            if (Controls[i] is ElementBase child)
                child.ClearPointerStateRecursive();
        }

        if (!stateChanged)
            return;

        if (_visualStylesEnabled)
            RefreshVisualStyles();

        NotifyVisualStyleStateDependents();
    }

    private void HandleEffectiveSizeChanged()
    {
        if (Parent is ElementBase parent && !parent.IsPerformingLayout)
            parent.PerformLayout();
        else if (!IsPerformingLayout && Controls.Count > 0)
            PerformLayout();

        Invalidate();
    }

    private ElementVisualStyleSnapshot CaptureCurrentSnapshot()
    {
        return new ElementVisualStyleSnapshot(
            _size,
            _backColor,
            _foreColor,
            _border,
            _borderColor,
            _radius,
            ElementVisualStyleInterpolator.CloneShadows(_shadows),
            _opacity,
            _renderTranslateX,
            _renderTranslateY,
            _renderScaleX,
            _renderScaleY);
    }

    private void ReplayVisualStyleConfigurations()
    {
        if (_visualStyleConfigurations.Count == 0)
            return;

        // Nested ThemeChanged during an in-flight replay must not Clear+rebuild again.
        if (_isReplayingVisualStyleConfigurations)
            return;

        var configurations = _visualStyleConfigurations.ToArray();

        _isReplayingVisualStyleConfigurations = true;
        _visualStyleMutationDepth++;
        try
        {
            VisualStyles.Clear();
            _hasVisualStyleBaseOverride = false;
            _styleBaseOverridesWidth = false;
            _styleBaseOverridesHeight = false;
            UpdateVisualStylesEnabledState();

            for (var i = 0; i < configurations.Length; i++)
            {
                var configuration = configurations[i];
                ConfigureVisualStyles(configuration.Configure, configuration.ClearExisting);
            }
        }
        finally
        {
            _visualStyleMutationDepth--;
            _isReplayingVisualStyleConfigurations = false;
        }
    }

    internal void OffsetEffectiveTranslateY(float delta)
    {
        if (Math.Abs(delta) < 0.5f)
            return;

        _renderTranslateY += delta;
        _styleEffectiveSnapshot = _styleEffectiveSnapshot.WithTranslateY(_styleEffectiveSnapshot.TranslateY + delta);
        Invalidate();
    }

    internal void SetEffectiveTranslateY(float value)
    {
        if (Math.Abs(_renderTranslateY - value) < 0.5f)
            return;

        _renderTranslateY = value;
        _styleEffectiveSnapshot = _styleEffectiveSnapshot.WithTranslateY(value);
        Invalidate();
    }

    private void SetStyleBaseSize(SKSize size, bool preserveOverriddenDimensions)
    {
        if (preserveOverriddenDimensions)
        {
            size = new SKSize(
                _styleBaseOverridesWidth ? _styleBaseSnapshot.Size.Width : size.Width,
                _styleBaseOverridesHeight ? _styleBaseSnapshot.Size.Height : size.Height);
        }

        _styleBaseSnapshot = _styleBaseSnapshot.WithSize(size);
    }

    private void SetStyleBaseBackColor(SKColor color)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithBackColor(color);
    }

    private void SetStyleBaseForeColor(SKColor color)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithForeColor(color);
    }

    private void SetStyleBaseBorder(Thickness border)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithBorder(border);
    }

    private void SetStyleBaseBorderColor(SKColor color)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithBorderColor(color);
    }

    private void SetStyleBaseRadius(Radius radius)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithRadius(radius);
    }

    private void SetStyleBaseShadows(BoxShadow[] shadows)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithShadows(ElementVisualStyleInterpolator.CloneShadows(shadows));
    }

    private void SetStyleBaseOpacity(float opacity)
    {
        _styleBaseSnapshot = _styleBaseSnapshot.WithOpacity(opacity);
    }

    private void UpdatePointerOverState(bool isPointerOver)
    {
        if (!UseDefaultPointerVisualStates)
            isPointerOver = false;

        if (_isPointerOver == isPointerOver)
            return;

        _isPointerOver = isPointerOver;
        if (_visualStylesEnabled)
            RefreshVisualStyles();

        NotifyVisualStyleStateDependents();
    }

    protected void UpdatePressedState(bool isPressed)
    {
        if (!UseDefaultPointerVisualStates)
            isPressed = false;

        if (_isPressed == isPressed)
            return;

        _isPressed = isPressed;
        if (_visualStylesEnabled)
            RefreshVisualStyles();

        NotifyVisualStyleStateDependents();
    }

    protected void RefreshVisualStylesForStateChange()
    {
        if (_visualStylesEnabled)
            RefreshVisualStyles();

        NotifyVisualStyleStateDependents();
    }

    private void UpdateVisualStylesEnabledState()
    {
        var wasEnabled = _visualStylesEnabled;
        _visualStylesEnabled = _hasVisualStyleBaseOverride || VisualStyles.Count > 0;

        if (wasEnabled && !_visualStylesEnabled)
        {
            _visualStyleAnimation.SetProgress(1);
            _styleAnimationFrom = _styleBaseSnapshot;
            _styleAnimationTo = _styleBaseSnapshot;
            ApplyEffectiveVisualStyle(_styleBaseSnapshot);
        }
    }

    private void DisposeVisualStyleSystem()
    {
        _visualStyleAnimation.OnAnimationProgress -= HandleVisualStyleAnimationProgress;
        _visualStyleAnimation.OnAnimationFinished -= HandleVisualStyleAnimationFinished;
        _visualStyleAnimation.Dispose();
    }

    private void NotifyVisualStyleStateDependents()
    {
        for (var i = _visualStyleStateDependents.Count - 1; i >= 0; i--)
        {
            var dependent = _visualStyleStateDependents[i];
            if (dependent == null || dependent.IsDisposed || dependent.Disposing)
            {
                _visualStyleStateDependents.RemoveAt(i);
                continue;
            }

            dependent.ReevaluateVisualStyles();
        }
    }

    private readonly record struct VisualStyleConfiguration(
        Action<ElementVisualStyleBuilder> Configure,
        bool ClearExisting);
}
