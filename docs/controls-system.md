# Orivy Controls System

Deep reference for Orivy's control model, lifecycle, input & rendering model, common controls, and guidance for implementing new controls.

## Overview

Orivy's UI is built from lightweight UI elements that derive from `ElementBase`. Controls are retained objects that participate in a two-phase layout/paint pipeline. Controls expose explicit lifecycle hooks for measuring, arranging, painting, and input handling.

Goals of this document:
- Explain the responsibilities of `ElementBase` and how layout/paint/input are dispatched.
- Describe the visual-style and motion-effect subsystems.
- Provide concrete examples and recommended patterns for new controls.

Primary source files referenced in this document:
- `Orivy/Controls/ElementBase.cs`
- `Orivy/Controls/ElementBase.VisualStyles.cs`
- `Orivy/Controls/ElementBase.MotionEffects.cs`
- `Orivy/Controls/Button.cs`
- `Orivy/Controls/GridList.cs`
- `Orivy/Controls/ComboBox.cs`
- `Orivy/Controls/WindowBase.cs`

## Element model and lifecycle

Elements implement `IArrangedElement` and are composed into parent/child trees via the `Controls` collection.

- File: `Orivy/Layout/IArrangedElement.cs` — layout contract used by engines.

Key lifecycle stages:
- Construction: create visual-style and motion-effect systems (done in `ElementBase` constructor).
- Load: `OnLoad` / `EnsureLoadedRecursively` fired when element becomes part of a loaded window.
- Measure: `GetPreferredSize(SKSize proposedSize)` — return a preferred size given constraints.
- Arrange: `SetBounds(SKRect bounds, BoundsSpecified specified)` — framework will call this during arrange; implementors should call `CommonProperties.SetSpecifiedBounds` when appropriate.
- Paint: `OnPaint(SKCanvas canvas)` — render content; `Paint` event is raised by the base implementation.
- Input: `OnMouseMove/OnMouseDown/OnMouseUp/OnMouseClick`, keyboard events `OnKeyDown/OnKeyUp/OnKeyPress`.
- Unload / Dispose: `OnUnload` / `Dispose` to release resources.

Example responsibilities of `ElementBase` (summary):
- property storage via `PropertyStore`
- layout helpers and `PerformLayout`/`SuspendLayout`/`ResumeLayout`
- visual style snapshot and animations (`ElementBase.VisualStyles`)
- motion effects timer and rendering (`ElementBase.MotionEffects`)
- event routing and focus management

See `Orivy/Controls/ElementBase.cs` for the canonical implementation.

## Layout and sizing

Controls expose common layout properties:
- `Size`, `MinimumSize`, `MaximumSize`, `Padding`, `Margin`, `Anchor`, `Dock`, `AutoSize`, `AutoSizeMode`.

Measure guidance:
- Implement `GetPreferredSize(SKSize proposedSize)` to return a sensible preferred size. The layout engine treats this as a hint; the final `Size` may be different.
- Avoid heavy allocations or creating fonts/text layouts on every measure pass. Use caching (for example `LayoutUtils.MeasureTextCache`).

Arrange guidance:
- Call `CommonProperties.SetSpecifiedBounds` (or let `SetBounds` internal implementation record which sides were explicitly set) when you implement `SetBounds` behavior in custom elements.

Batching layout changes:
- Use `SuspendLayout`/`ResumeLayout` on an element or `LayoutTransaction` when making multiple changes that affect layout to avoid repeated passes.

## Painting and visual styles

`ElementBase` provides a composable visual-style system with animated transitions. Visual styles are defined using the `ElementVisualStyleCollection` API and applied as snapshots to the control.

- Visual styles live in `ElementBase.VisualStyles` and are resolved/animated by `ElementBase.VisualStyles.cs`.
- To react to state changes (hover, pressed, focused), use the provided `VisualState` context or configure rules via `ConfigureVisualStyles`.

Motion effects are a separate, lightweight system driven by a timer to render decorative animations (e.g., accent or background particles). Configure via `MotionEffects` or `ConfigureMotionEffects`.

Painting best practices:
- Use `OnPaint(SKCanvas canvas)` and raise the `Paint` event only for external hooks. Keep painting deterministic and avoid heap allocations on the hot path.
- Re-use `SKPaint`, `SKPath`, and `SKShader` objects when possible.

## Input model and event routing

Window-level input dispatch translates mouse/keyboard events into element-local handlers.

Key points:
- `OnMouseMove` finds the topmost input target with `FindTopmostInputTarget` and forwards adjusted coordinates to child elements.
- `OnMouseDown` updates pressed state and focuses the clicked element via the parent window's focus manager.
- `OnMouseClick` and `OnMouseDoubleClick` are invoked after up/down sequences and route to children as appropriate.
- Keyboard events are routed to the `FocusedElement` and also bubble through parent elements for handling of `Tab` key navigation.

See `Orivy/Controls/ElementBase.cs` for exact dispatch code (`OnMouseDown`, `OnMouseUp`, `OnMouseClick`, `OnKeyDown`, etc.).

## Common controls and behavior notes

- `Button` (`Orivy/Controls/Button.cs`)
  - Implements `GetPreferredSize` by measuring text with `TextRenderer.MeasureText` and adding padding/border.
  - Supports keyboard activation (Enter/Space) via `OnKeyDown`/`OnKeyUp` overrides and `PerformClick()`.
  - Configures default visual styles in the constructor and optionally enables accent motion effects.

- `GridList` (`Orivy/Controls/GridList.cs` + `GridList.Models.cs`)
  - Data-driven list with virtualization-friendly patterns. Consult `GridList` source for model binding and cell measurement points.

- `ComboBox`, `ScrollBar`, `MenuStrip`, `ContextMenuStrip` — controls follow the same `ElementBase` patterns (measure, arrange, paint, input). See each file for behavior specifics.

## Implementing a new control (step-by-step)

Follow these steps to implement a performant, well-behaved control:

1. Derive from `ElementBase`.
2. Override `GetPreferredSize(SKSize proposedSize)` and return a conservative preferred size.
   - Use `LayoutUtils.MeasureTextCache` or `TextRenderer.MeasureText` for text measurement, caching results per-instance.
3. Override `OnPaint(SKCanvas canvas)` to render your control. Raise the `Paint` event if you want external subscribers.
4. Override input handlers (`OnMouseDown`, `OnMouseUp`, `OnMouseMove`, `OnKeyDown`, etc.) only when needed.
5. Call `InvalidateMeasure()` and/or `Invalidate()` when properties change that affect size or appearance.
6. Provide `ConfigureVisualStyles` or default visual styles in the constructor to expose themeable appearance.

Minimal example:

```csharp
public class SimpleLabel : ElementBase
{
    public string LabelText { get; set; } = string.Empty;

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var options = new TextRenderOptions { Wrap = TextWrap.None };
        var textSize = TextRenderer.MeasureText(LabelText, Font, proposedSize, options);
        return new SKSize(textSize.Width + Padding.Left + Padding.Right, textSize.Height + Padding.Top + Padding.Bottom);
    }

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);
        using var paint = new SKPaint { Color = ForeColor, IsAntialias = true };
        canvas.DrawText(LabelText, Padding.Left, Padding.Top + Font.Metrics.Ascent, paint);
    }
}
```

## Performance guidance

- Avoid allocating `SKPaint`, `SKPath`, fonts, or arrays inside `GetPreferredSize` or in tight paint loops. Cache and reuse.
- Use `LayoutUtils.MeasureTextCache` for multi-line or wrapped text measurement.
- Batch property changes using `SuspendLayout`/`ResumeLayout` or `LayoutTransaction` to avoid repeated layout.
- Keep `OnPaint` idempotent and minimize branching that could cause large GC churn.

## Debugging and troubleshooting

- If layout looks wrong after changing anchors/dock, verify that `DefaultLayout` anchor V2 behavior is compatible with expectations (`Orivy/Layout/DefaultLayout.cs`).
- If controls fail to update visuals when theme changes, ensure `RefreshVisualStyles(forceImmediate: true)` is invoked when base style properties are mutated.

## File reference

- `Orivy/Controls/ElementBase.cs`
- `Orivy/Controls/ElementBase.VisualStyles.cs`
- `Orivy/Controls/ElementBase.MotionEffects.cs`
- `Orivy/Controls/Button.cs`
- `Orivy/Controls/GridList.cs`
- `Orivy/Controls/ComboBox.cs`
- `Orivy/Controls/WindowBase.cs`

---

Next: I will mark the `Controls` deep-dive done and start the `Binding` deep-dive. I will update the todo list accordingly.
