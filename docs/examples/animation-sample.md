Animation example
==========================

Hover animation example using `AnimationManager`:

```csharp
public class AnimatedButton : Button
{
    private readonly AnimationManager _hoverAnim = new AnimationManager();

    public AnimatedButton()
    {
        _hoverAnim.OnAnimationProgress += (_) => Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hoverAnim.StartNewAnimation(AnimationDirection.In);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverAnim.StartNewAnimation(AnimationDirection.Out);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        var progress = (float)_hoverAnim.GetProgress();
        // Use progress to interpolate color, shadow or scale
    }
}
```

Tip: inside `OnAnimationProgress` only call `Invalidate()`; do not perform expensive calculations on the timer thread.
