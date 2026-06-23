using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Orivy;

/// <summary>
/// Manages the application's color scheme, theme transitions, and derived UI colors.
/// </summary>
public static class ColorScheme
{
    /// <summary>
    /// Lookup table for pre-calculated relative luminance values (0-255).
    /// Avoids expensive Math.Pow calculations during runtime.
    /// </summary>
    private static readonly double[] LuminanceLut = new double[256];

    /// <summary>
    /// Static constructor to initialize the luminance lookup table and default cached colors.
    /// </summary>
    static ColorScheme()
    {
        // Pre-calculate Math.Pow values for the LUT
        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            LuminanceLut[i] = v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
        }

        // Initialize cached derived colors for the default theme (Light Mode) on startup
        RecalculateDerivedColors();
    }

    /// <summary>
    /// The starting background color for the current transition animation.
    /// </summary>
    private static SKColor _backgroundFrom = new(250, 250, 250);

    /// <summary>
    /// The target background color for the current transition animation.
    /// </summary>
    private static SKColor _backgroundTo = new(250, 250, 250);

    /// <summary>
    /// The currently active, interpolated background color.
    /// </summary>
    private static SKColor _currentBackground = new(250, 250, 250);

    /// <summary>
    /// The progress of the current background transition (0.0 to 1.0).
    /// </summary>
    private static double _progress = 1.0;

    /// <summary>
    /// Indicates whether the current theme is evaluated as dark mode.
    /// </summary>
    private static bool _isDark;

    /// <summary>
    /// Unique identifier to cancel overlapping transition animations.
    /// </summary>
    private static int _transitionId;

    /// <summary>
    /// Cached surface variant color.
    /// </summary>
    private static SKColor _surfaceVariant;

    /// <summary>
    /// Cached surface container color.
    /// </summary>
    private static SKColor _surfaceContainer;

    /// <summary>
    /// Cached high elevation surface container color.
    /// </summary>
    private static SKColor _surfaceContainerHigh;

    /// <summary>
    /// Cached low elevation surface container color.
    /// </summary>
    private static SKColor _surfaceContainerLow;

    /// <summary>
    /// Cached outline/border color.
    /// </summary>
    private static SKColor _outline;

    /// <summary>
    /// Cached error state color.
    /// </summary>
    private static SKColor _error;

    /// <summary>
    /// Cached success state color.
    /// </summary>
    private static SKColor _success;

    /// <summary>
    /// Cached warning state color.
    /// </summary>
    private static SKColor _warning;

    /// <summary>
    /// Cached shadow color.
    /// </summary>
    private static SKColor _shadowColor;

    /// <summary>
    /// The primary brand/seed color for the theme.
    /// </summary>
    private static SKColor _primary = new(33, 150, 243);

    /// <summary>
    /// Toggle for enabling or disabling the dashed focus path effect.
    /// </summary>
    private static bool _useFocusPathEffect = true;

    /// <summary>
    /// The base length of the dash in the focus path effect.
    /// </summary>
    private static float _focusPathEffectDashLength = 1f;

    /// <summary>
    /// The base length of the gap in the focus path effect.
    /// </summary>
    private static float _focusPathEffectGapLength = 2.5f;

    /// <summary>
    /// Cached SkiaSharp path effect instance to avoid reallocation.
    /// </summary>
    private static SKPathEffect? _cachedFocusPathEffect;

    /// <summary>
    /// The scale value used to generate the cached path effect.
    /// </summary>
    private static float _cachedFocusPathEffectScale;

    /// <summary>
    /// The dash length value used to generate the cached path effect.
    /// </summary>
    private static float _cachedFocusPathEffectDashLength;

    /// <summary>
    /// The gap length value used to generate the cached path effect.
    /// </summary>
    private static float _cachedFocusPathEffectGapLength;

    /// <summary>
    /// When true, renders debug borders around UI elements for layout troubleshooting.
    /// </summary>
    public static bool DrawDebugBorders;

    /// <summary>
    /// Raised whenever the theme, primary color, or background transition updates.
    /// UI elements should subscribe to this to trigger redraws.
    /// </summary>
    public static event EventHandler? ThemeChanged;

    /// <summary>
    /// Gets or sets whether the dashed focus path effect is enabled.
    /// Changing this value invalidates the path effect cache and raises <see cref="ThemeChanged"/>.
    /// </summary>
    public static bool UseFocusPathEffect
    {
        get => _useFocusPathEffect;
        set
        {
            if (_useFocusPathEffect == value) return;
            _useFocusPathEffect = value;
            InvalidateFocusPathEffectCache();
            RaiseThemeChanged();
        }
    }

    /// <summary>
    /// Gets or sets the base dash length for the focus path effect.
    /// Values below 0.5 are clamped. Raises <see cref="ThemeChanged"/> on change.
    /// </summary>
    public static float FocusPathEffectDashLength
    {
        get => _focusPathEffectDashLength;
        set
        {
            var next = Math.Max(0.5f, value);
            if (Math.Abs(_focusPathEffectDashLength - next) < 0.001f) return;
            _focusPathEffectDashLength = next;
            InvalidateFocusPathEffectCache();
            RaiseThemeChanged();
        }
    }

    /// <summary>
    /// Gets or sets the base gap length for the focus path effect.
    /// Values below 0.5 are clamped. Raises <see cref="ThemeChanged"/> on change.
    /// </summary>
    public static float FocusPathEffectGapLength
    {
        get => _focusPathEffectGapLength;
        set
        {
            var next = Math.Max(0.5f, value);
            if (Math.Abs(_focusPathEffectGapLength - next) < 0.001f) return;
            _focusPathEffectGapLength = next;
            InvalidateFocusPathEffectCache();
            RaiseThemeChanged();
        }
    }

    /// <summary>
    /// Gets or sets the dark mode state. 
    /// Setting this triggers an animated background transition to the default light/dark colors.
    /// </summary>
    public static bool IsDarkMode
    {
        get => _isDark;
        set
        {
            var target = value ? new SKColor(28, 28, 30) : new SKColor(250, 250, 250);
            if (_isDark == value && ColorsClose(_currentBackground, target)) return;
            StartBackgroundTransition(target);
        }
    }

    /// <summary>
    /// Gets or sets the primary theme color. 
    /// Setting this property updates the internal cache and raises the <see cref="ThemeChanged"/> event.
    /// </summary>
    public static SKColor Primary
    {
        get => _primary;
        set
        {
            if (_primary == value) return;
            _primary = value;
            RaiseThemeChanged();
        }
    }

    /// <summary>
    /// Gets the current active background/surface color.
    /// </summary>
    public static SKColor Surface => _currentBackground;
    
    /// <summary>
    /// Alias for <see cref="Surface"/>.
    /// </summary>
    public static SKColor BackColor => _currentBackground;
    
    /// <summary>
    /// Gets the foreground (text/icon) color, automatically determined for contrast against the surface.
    /// </summary>
    public static SKColor ForeColor => _currentBackground.Determine();

    /// <summary>
    /// Gets the cached surface variant color (slightly adjusted from the base surface).
    /// </summary>
    public static SKColor SurfaceVariant => _surfaceVariant;
    
    /// <summary>
    /// Gets the cached standard surface container color.
    /// </summary>
    public static SKColor SurfaceContainer => _surfaceContainer;
    
    /// <summary>
    /// Gets the cached high-elevation surface container color.
    /// </summary>
    public static SKColor SurfaceContainerHigh => _surfaceContainerHigh;
    
    /// <summary>
    /// Gets the cached low-elevation surface container color.
    /// </summary>
    public static SKColor SurfaceContainerLow => _surfaceContainerLow;

    /// <summary>
    /// Gets the cached outline color used for borders and dividers.
    /// </summary>
    public static SKColor Outline => _outline;
    
    /// <summary>
    /// Alias for <see cref="Outline"/>.
    /// </summary>
    public static SKColor BorderColor => _outline;

    /// <summary>
    /// Gets the cached error state color (red tones).
    /// </summary>
    public static SKColor Error => _error;
    
    /// <summary>
    /// Gets the cached success state color (green tones).
    /// </summary>
    public static SKColor Success => _success;
    
    /// <summary>
    /// Gets the cached warning state color (yellow/orange tones).
    /// </summary>
    public static SKColor Warning => _warning;
    
    /// <summary>
    /// Gets the cached shadow color (white with alpha in dark mode, black with alpha in light mode).
    /// </summary>
    public static SKColor ShadowColor => _shadowColor;

    // --- Public Methods ---

    /// <summary>
    /// Retrieves a cached <see cref="SKPathEffect"/> for dashed focus indicators.
    /// If the requested scale or dash/gap lengths differ from the cache, a new effect is created.
    /// </summary>
    /// <param name="scale">The scale multiplier for the dash and gap lengths. Minimum is 0.5.</param>
    /// <returns>A cached <see cref="SKPathEffect"/> or null if the effect is disabled.</returns>
    public static SKPathEffect? GetFocusPathEffect(float scale = 1f)
    {
        if (!UseFocusPathEffect) return null;

        var resolvedScale = Math.Max(0.5f, scale);
        
        // Return cached instance if parameters haven't changed
        if (_cachedFocusPathEffect != null
            && Math.Abs(_cachedFocusPathEffectScale - resolvedScale) < 0.001f
            && Math.Abs(_cachedFocusPathEffectDashLength - FocusPathEffectDashLength) < 0.001f
            && Math.Abs(_cachedFocusPathEffectGapLength - FocusPathEffectGapLength) < 0.001f)
            return _cachedFocusPathEffect;

        InvalidateFocusPathEffectCache();

        var dashLength = FocusPathEffectDashLength * resolvedScale;
        var gapLength = FocusPathEffectGapLength * resolvedScale;

        _cachedFocusPathEffectScale = resolvedScale;
        _cachedFocusPathEffectDashLength = FocusPathEffectDashLength;
        _cachedFocusPathEffectGapLength = FocusPathEffectGapLength;
        _cachedFocusPathEffect = SKPathEffect.CreateDash(new[] { dashLength, gapLength }, 0f);

        return _cachedFocusPathEffect;
    }

    /// <summary>
    /// Instantly applies the light or dark theme without any transition animation.
    /// </summary>
    /// <param name="dark">True to apply dark mode, false for light mode.</param>
    public static void SetThemeInstant(bool dark)
    {
        _isDark = dark;
        var bg = dark ? new SKColor(28, 28, 30) : new SKColor(250, 250, 250);

        _backgroundFrom = bg;
        _backgroundTo = bg;
        _currentBackground = bg;
        _progress = 1;

        RecalculateDerivedColors();
        RaiseThemeChanged();
    }

    /// <summary>
    /// Starts an animated transition to a specific target background color.
    /// Automatically determines if the target requires dark or light mode based on luminance.
    /// </summary>
    /// <param name="targetBackground">The target background color to transition to.</param>
    public static void StartThemeTransition(SKColor targetBackground)
    {
        _isDark = RelativeLuminance(targetBackground) < .45;
        var accent = targetBackground.Brightness(_isDark ? .35f : -.35f);
        
        // Update primary color via the property setter
        Primary = accent; 
        
        StartBackgroundTransition(targetBackground);
    }

    /// <summary>
    /// Calculates the tint color applied to elevated surfaces in dark mode.
    /// </summary>
    /// <param name="level">The elevation level (1-5+).</param>
    /// <returns>A white color with alpha based on the elevation level, or transparent in light mode.</returns>
    public static SKColor GetElevationTint(int level)
    {
        if (!_isDark) return SKColors.Transparent;
        return SKColors.White.WithAlpha((byte)(level * 4 + 4));
    }

    /// <summary>
    /// Gets the blur radius for a given elevation level.
    /// </summary>
    /// <param name="level">The elevation level.</param>
    /// <returns>The blur radius in pixels.</returns>
    public static float GetElevationBlur(int level) => level switch
    {
        1 => 2, 2 => 4, 3 => 8, 4 => 12, 5 => 16, _ => 20
    };

    /// <summary>
    /// Gets the Y-axis offset for a given elevation level.
    /// </summary>
    /// <param name="level">The elevation level.</param>
    /// <returns>The vertical offset in pixels.</returns>
    public static float GetElevationOffset(int level) => level * 2;

    /// <summary>
    /// Disposes of the current cached focus path effect and resets the cache tracking variables.
    /// </summary>
    private static void InvalidateFocusPathEffectCache()
    {
        _cachedFocusPathEffect?.Dispose();
        _cachedFocusPathEffect = null;
        _cachedFocusPathEffectScale = 0f;
        _cachedFocusPathEffectDashLength = 0f;
        _cachedFocusPathEffectGapLength = 0f;
    }

    /// <summary>
    /// Executes the background color transition animation asynchronously.
    /// Uses a high-precision Stopwatch for smooth interpolation and avoids GC-heavy Task.Delay loops.
    /// </summary>
    /// <param name="target">The target background color to animate towards.</param>
    private static async void StartBackgroundTransition(SKColor target)
    {
        var id = ++_transitionId;
        _backgroundFrom = _currentBackground;
        _backgroundTo = target;

        var sw = Stopwatch.StartNew();
        const long durationTicks = 220 * TimeSpan.TicksPerMillisecond;

        while (sw.ElapsedTicks < durationTicks)
        {
            if (id != _transitionId) return; // Cancelled by a newer transition

            double t = (double)sw.ElapsedTicks / durationTicks;
            t = t * t * (3 - 2 * t); // Smoothstep interpolation
            _progress = t;

            _currentBackground = Lerp(_backgroundFrom, _backgroundTo, t);

            bool newIsDark = RelativeLuminance(_currentBackground) < .45;
            if (_isDark != newIsDark)
            {
                _isDark = newIsDark;
            }

            RecalculateDerivedColors();
            RaiseThemeChanged();

            // ConfigureAwait(false) prevents context switching overhead
            await Task.Delay(16).ConfigureAwait(false);
        }

        // Ensure final state is exact
        _progress = 1;
        _currentBackground = target;
        _isDark = RelativeLuminance(target) < .45;
        
        RecalculateDerivedColors();
        RaiseThemeChanged();
    }

    /// <summary>
    /// Invokes the <see cref="ThemeChanged"/> event safely.
    /// Avoids GetInvocationList() to prevent array allocations and reduce GC pressure.
    /// </summary>
    private static void RaiseThemeChanged()
    {
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Recalculates and updates all cached derived colors (Surface variants, Outline, Status colors, etc.)
    /// based on the current <see cref="_currentBackground"/> and <see cref="_isDark"/> state.
    /// </summary>
    private static void RecalculateDerivedColors()
    {
        _surfaceVariant = SurfaceAdjust(_currentBackground, .10);
        _surfaceContainer = SurfaceAdjust(_currentBackground, .08);
        _surfaceContainerHigh = SurfaceAdjust(_currentBackground, .12);
        _surfaceContainerLow = SurfaceAdjust(_currentBackground, .04);
        _outline = SurfaceAdjust(_currentBackground, .22);

        _error = _isDark ? new SKColor(255, 100, 100) : new SKColor(220, 50, 50);
        _success = _isDark ? new SKColor(100, 255, 150) : new SKColor(50, 200, 100);
        _warning = _isDark ? new SKColor(255, 200, 100) : new SKColor(220, 150, 50);
        _shadowColor = _isDark ? new SKColor(255, 255, 255, 20) : new SKColor(0, 0, 0, 20);
    }

    /// <summary>
    /// Adjusts a base surface color by blending it with white (in dark mode) or black (in light mode).
    /// </summary>
    /// <param name="baseColor">The starting color.</param>
    /// <param name="amount">The blend amount (0.0 to 1.0).</param>
    /// <returns>The adjusted <see cref="SKColor"/>.</returns>
    private static SKColor SurfaceAdjust(SKColor baseColor, double amount)
        => _isDark ? Blend(baseColor, SKColors.White, amount) : Blend(baseColor, SKColors.Black, amount);

    /// <summary>
    /// Linearly interpolates between two colors. Alias for <see cref="Lerp"/>.
    /// </summary>
    /// <param name="a">The start color.</param>
    /// <param name="b">The end color.</param>
    /// <param name="t">The interpolation factor (0.0 to 1.0).</param>
    /// <returns>The blended <see cref="SKColor"/>.</returns>
    private static SKColor Blend(SKColor a, SKColor b, double t) => Lerp(a, b, t);

    /// <summary>
    /// Performs linear interpolation (LERP) between the RGBA channels of two colors.
    /// </summary>
    /// <param name="a">The start color.</param>
    /// <param name="b">The end color.</param>
    /// <param name="t">The interpolation factor (0.0 to 1.0).</param>
    /// <returns>The interpolated <see cref="SKColor"/>.</returns>
    private static SKColor Lerp(SKColor a, SKColor b, double t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t),
        (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t),
        (byte)(a.Alpha + (b.Alpha - a.Alpha) * t)
    );

    /// <summary>
    /// Calculates the relative luminance of a color using the pre-calculated LUT.
    /// Used to determine if a color is perceived as "dark" or "light".
    /// </summary>
    /// <param name="c">The color to evaluate.</param>
    /// <returns>The relative luminance value (0.0 to 1.0).</returns>
    private static double RelativeLuminance(SKColor c)
        => .2126 * LuminanceLut[c.Red] + .7152 * LuminanceLut[c.Green] + .0722 * LuminanceLut[c.Blue];

    /// <summary>
    /// Checks if two colors are visually similar within a small threshold.
    /// </summary>
    /// <param name="a">The first color.</param>
    /// <param name="b">The second color.</param>
    /// <returns>True if the RGB channels are within a threshold of 2.</returns>
    private static bool ColorsClose(SKColor a, SKColor b)
    {
        const int t = 2;
        return Math.Abs(a.Red - b.Red) <= t
            && Math.Abs(a.Green - b.Green) <= t
            && Math.Abs(a.Blue - b.Blue) <= t;
    }
}