# Controls

## Inheritance

`ElementBase` is the control. `Element` is an empty concrete. `Container` is a transparent host (`FlowLayout`, `Grid`). `WindowBase` is the HWND. `Window` adds title-bar chrome.

Paint order in `ElementBase.Render` is fixed: transform, shadows, background, background images, backdrop, motion, `OnPaint`, spinner, border, default text, children, focus adorner. Customize with `OnPaint`, visual styles, motion, or backdrop. Do not reimplement `Render`.

## New control

1. `public class MyControl : ElementBase` in `Orivy/Controls/MyControl.cs`, namespace `Orivy.Controls`.
2. Parameterless constructor so Studio can `Activator.CreateInstance` it. Set `AutoSize`, `TabStop`, `Padding`, `Size`, and call `ConfigureVisualStyles`.
3. Property setters: return if unchanged, then raise `OnXxxChanged`, then `Invalidate()` or `InvalidateMeasure()`. Layout properties (`Dock`, `Anchor`, `Margin`) go through the existing `CommonProperties` / `DefaultLayout` setters, not a parallel store.
4. Override `GetPreferredSize` when `AutoSize` depends on content. Override `ShouldRenderDefaultText` to `false` when `OnPaint` draws the text itself.
5. Input overrides: `OnMouseDown` / `OnMouseUp` / `OnMouseMove` / `OnKeyDown`. Set `MouseEventArgs.Handled` when the control consumes the event.
6. Drag: capture on the parent window and implement `IMouseCaptureLost`. Existing drags that still omit the interface (`TextBox`, `GridList`, `ScrollBar`, `TrackBar`, `SplitContainer`, `ColorPicker`, `SwitchButton`, `NumericUpDown`) are a gap. New drag code does not copy that gap.
7. `Dispose(bool)`: unsubscribe animation and extra events, dispose owned Skia objects, then `base.Dispose`. The base already unsubscribes `ColorScheme.ThemeChanged` and disposes render paints, visual styles, motion, remote images, and backdrop.
8. Public properties that Studio or the property grid should show get `[Category]`, `[Description]`, and `[DefaultValue]`. Hide infrastructure with `[Browsable(false)]`.

`WindowBase` is not a toolbox item. Do not give a control a constructor that requires arguments if it should be placeable.

## Visual styles

Call `ConfigureVisualStyles` from the constructor. The base stores the builder and replays it on theme change.

```csharp
ConfigureVisualStyles(styles =>
{
    styles
        .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
        .Base(baseStyle => baseStyle
            .Background(ColorScheme.Primary)
            .Foreground(SKColors.White)
            .Radius(8))
        .OnHover(rule => rule.Background(ColorScheme.Primary.Brightness(0.06f)))
        .OnPressed(rule => rule.Opacity(0.94f))
        .OnDisabled(rule => rule.Opacity(0.5f));
});
```

Checked appearance: override `GetVisualCheckedState()` and call `RefreshVisualStylesForStateChange()` when the flag changes. Pointer and pressed states are already hooked from mouse enter/leave/down/up.

Color helpers: `Brightness`, `WithAlpha`, `ColorScheme.Surface*`, `ForeColor`, `Outline`, `ShadowColor`.

## Layout (inside Orivy)

`LayoutTransaction` is `internal`. Use it from framework code, not from Studio or example apps.

- One-shot: `LayoutTransaction.DoLayout(parent, this, PropertyNames.Text)`.
- Wrap code that may nest layouts: `using (new LayoutTransaction(parent, this, PropertyNames.Bounds))`.
- Property name strings come from `PropertyNames`, not literals.
- `PerformLayout` is re-entrant-guarded (`IsPerformingLayout`) and bumps `s_globalLayoutPassId`, which drops the measure cache. `SuspendLayout` / `ResumeLayout` batch structural edits.
- The engine path is `DefaultLayout` (dock, then anchor, then autosize). `ElementBase.Measure` / `Arrange` are a second, rarely used pass. Fix the path the control actually runs.
- Dock and Anchor share one bitfield. Dock other than `None` replaces Anchor until Dock returns to `None`.
- `IArrangedElement.Children` allocates a new list on every get. Do not add more allocations in layout hot paths.

## Binding

```csharp
control.Link(c => c.Text).From(vm, v => v.Name).OneWay();
control.Link(c => c.Text).FromData<MyVm, string>(v => v.Name).TwoWay();
```

Expressions must be a direct member. `Link` bindings are tracked and disposed with the element. View models derive `ObservableObject` and use `SetProperty`. `InteractionExtensions.When` never unsubscribes; do not use it for lifetime-sensitive handlers.

## DPI

Logical layout sizes stay unscaled unless the existing control already multiplies by `ScaleFactor`. Fonts go through `CreateRenderFont` / `size.Topx(this)`. Override `OnDpiChanged` only to extend the base, which already scales `Size`, `Padding`, and `Margin`. Override `ShouldScaleSizeOnDpiChange` when `AutoSize` owns the size. Do not also apply the `WM_DPICHANGED` suggested rect (`_isHandlingDpiChange` exists to prevent that double scale).
