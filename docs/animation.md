
Animation system — guide and patterns
====================================

Orivy's animation subsystem provides a simple yet extensible building block for transitioning UI state. It is based on a small set of types in `Orivy.Animation`:

- `AnimationManager` — high-level controller for start/stop/interrupt animation sequences.
- `ValueProvider<T>` — generic transition interpolator over a time interval.
- `ValueFactory<T>` and `ValueFactories` — numeric and geometry factories that compute intermediate values from progress.
- `EasingMethod` / `EasingMethods` — easing functions (quadratic/cubic/quartic, linear, chain, invert, etc.).

Key APIs
- `AnimationManager.StartNewAnimation(AnimationDirection direction)`
- `AnimationManager.Stop()`
- `AnimationManager.GetProgress()`
- `AnimationManager.GetSource()` (optional vector source override)
- `AnimationManager.GetData()` (custom data payload)
- `AnimationManager.Running` / `AnimationManager.IsAnimating()`
- Events: `OnAnimationProgress` and `OnAnimationFinished`

### Implementation details

`AnimationManager` is in `Orivy/Animation/AnimationManager.cs`.

- Uses `ValueProvider<double>` to store the current animated value (`0..1`).
- Timer is lazily created in `EnsureTimer()` when animation starts (~60 FPS, 16ms interval).
- `StartNewAnimation` sets `target` from direction (`In` => 1, `Out` => 0) and computes duration from `Increment`.
- `InterruptAnimation` controls whether a new call interrupts an ongoing animation.
- `Singular` mode means only one animation is tracked (`GetAnimationCount()` returns 1 when running).


### ValueProvider

`ValueProvider<T>` is a reusable time-based interpolator in `Orivy/Animation/ValueProvider.cs`.

- `StartTransition(startValue, targetValue, duration)` configures start/target values and time.
- `CurrentValue` returns `ValueFactory(StartValue, TargetValue, EasingMethod(CurrentProgress))`.
- `CurrentProgress` is computed from a `Stopwatch` timestamp and duration.
- `CancelTransition()` stops the current animation by forcing `StartValue` and `TargetValue` to current value and zero duration.

### Value factories

`ValueFactories` (`Orivy/Animation/ValueFactories.cs`) support many types:
- numeric types (`byte`, `int`, `double`, ...)
- geometry (`SKPoint`, `SKSize`, `SKRect`)
- `SKColor` interpolation (RGBA channels)
- `bool`, `decimal`, `TimeSpan`

### Easing functions

`EasingMethods` (`Orivy/Animation/EasingMethods.cs`) includes:
- `Chain`, `Invert`, `UpsideDown`, `Reverse` helpers
- default easing use `EasingMethods.DefaultEase` (QuadraticInOut by default in `AnimationManager`)

## Detailed example: fade-in/out panel

```csharp
public class FadePanel : ElementBase
{
    private readonly AnimationManager _fade = new AnimationManager();
    public bool IsVisibleAnimated { get; private set; }

    public FadePanel()
    {
        _fade.OnAnimationProgress += (_) => Invalidate();
        _fade.OnAnimationFinished += _ => IsVisibleAnimated = _fade.GetProgress() > 0.5;
    }

    public void AnimateIn() => _fade.StartNewAnimation(AnimationDirection.In);
    public void AnimateOut() => _fade.StartNewAnimation(AnimationDirection.Out);

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var opacity = (float)_fade.GetProgress();
        using var paint = new SKPaint { Color = ColorScheme.Surface.WithAlpha((byte)(opacity * 255f)) };
        canvas.DrawRect(ClientRectangle, paint);

        // You can treat _fade.GetSource() and _fade.GetData() as optional animation metadata.
    }
}
```

## Advanced scenario: custom easing and multiple animations

```csharp
var manager = new AnimationManager { AnimationType = AnimationType.EaseInOut, Increment = 0.08 };
manager.OnAnimationProgress += (_) => control.Invalidate();
manager.StartNewAnimation(AnimationDirection.In);

// Inline custom easing via ValueProvider
var valueProvider = new ValueProvider<double>(0.0, ValueFactories.DoubleFactory, EasingMethods.CubicEaseIn);
valueProvider.StartTransition(0.0, 1.0, TimeSpan.FromMilliseconds(400));

// In rendering loop:
var t = valueProvider.CurrentValue; // 0..1
control.Opacity = (float)t;
```

## Performance and correctness

- Keep the per-frame callback small: only compute progress and call `Invalidate()`;
  expensive operations (layout, allocations, GC pressure) belong in paint logic or precomputed lookup tables.
- Avoid recreating `AnimationManager` inside `OnPaint`; reuse one instance per control.
- `AnimationManager` uses `Timer` and event handlers; call `Dispose()` when control is disposed to avoid timer leaks.
- `ValueProvider` is not thread-safe; use on UI thread only.

## Troubleshooting

- If `OnAnimationProgress` stops firing, verify `AnimationManager.Running` and that `Timer` is created (`EnsureTimer`).
- For instant changes, use `SetProgress(0 or 1)` and `Stop()` to avoid the tick loop.
- `AnimationType` change while running takes effect only on next `StartNewAnimation` call (`UpdateEasingMethod`).

## Links and source

- `Orivy/Animation/AnimationManager.cs`
- `Orivy/Animation/ValueProvider.cs`
- `Orivy/Animation/ValueFactories.cs`
- `Orivy/Animation/EasingMethod.cs`
- `Orivy/Animation/EasingMethods.cs`

