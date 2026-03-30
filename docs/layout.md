# Orivy Layout System

This document is a deep, implementation-aware reference for Orivy's layout system. It mirrors the level of detail in `docs/binding-system.md` and explains the core concepts, contracts, behaviors, examples, and implementation mapping to source files.

## Overview

Orivy implements a retained-mode, two-phase layout pipeline inspired by WinForms semantics: a *Measure* (preferred-size) phase and an *Arrange* (final bounds) phase. Layout is explicit and deterministic — every element participates via the `IArrangedElement` contract and the active `LayoutEngine` implementation.

Goals:
- Predictable sizing and positioning
- Explicit measurement and arrangement passes
- Low allocations in hot paths (reuse caches and paints)
- Support for common layout idioms: Dock, Anchor, AutoSize

Key design principles:
- The layout engine queries `GetPreferredSize` with a proposed constraints size and then assigns final bounds with `SetBounds`.
- Controls should avoid heavy allocations during `GetPreferredSize` and reuse text/measure caches when possible.

## Core contract

The layout contract is defined by `IArrangedElement`.

- File: [Orivy/Layout/IArrangedElement.cs](Orivy/Layout/IArrangedElement.cs)

Important members (summary):

```csharp
public interface IArrangedElement
{
    SKRect Bounds { get; }
    void SetBounds(SKRect bounds, BoundsSpecified specified);
    SKSize GetPreferredSize(SKSize proposedSize);
    SKRect DisplayRectangle { get; }
    bool ParticipatesInLayout { get; }
    PropertyStore Properties { get; }
    void PerformLayout();
    void SuspendLayout();
    void ResumeLayout(bool performLayout);
    IArrangedElement? Container { get; }
    ArrangedElementCollection Children { get; }
}
```

Notes:
- `GetPreferredSize(SKSize proposedSize)` is a hint. The engine may not be able to honor the returned size.
- `SetBounds(SKRect bounds, BoundsSpecified specified)` must call the framework helper to record which sides were specified; see `CommonProperties.SetSpecifiedBounds` usage patterns in control implementations.

## LayoutEngine

Layout engines implement the measurement/arrangement strategy. The default engine is `DefaultLayout`.

- File: [Orivy/Layout/LayoutEngine.cs](Orivy/Layout/LayoutEngine.cs)
- File: [Orivy/Layout/DefaultLayout.cs](Orivy/Layout/DefaultLayout.cs)

The engine is responsible for:
- Iterating children in z-order
- Performing dock passes (slicing regions for docked children)
- Performing anchor passes (computing anchored bounds)
- Respecting `AutoSize` semantics
- Caching preferred sizes and staged bounds across measurement phases

## DefaultLayout semantics

`DefaultLayout` implements the familiar Dock/Anchor/AutoSize behaviors. Main points:

- Dock order follows children z-order (Controls.Add order). Docked controls are laid out first, slicing available region.
- Anchored controls are measured and positioned relative to the parent's client rectangle. The implementation contains an "Anchor V2" compatibility path for tighter behavior with mixed anchors.
- `AutoSize` controls contribute to preferred-size calculations. Growth mode (GrowOnly or GrowAndShrink) is honored when computing parent preferred size.
- The engine stages bounds changes in a cached-bounds store during measurement and applies them during arrange via `ApplyCachedBounds`.

See implementation for details: [Orivy/Layout/DefaultLayout.cs](Orivy/Layout/DefaultLayout.cs)

### Docking

- Docked children take up space by consuming the parent's remaining client region. Typical dock values: `Top`, `Bottom`, `Left`, `Right`, `Fill`.
- The layout calculates preferred size for docked controls when asked for parent's preferred size.

Example (conceptual):

```csharp
// Typical usage from control code (high-level):
header.Dock = DockStyle.Top;
footer.Dock = DockStyle.Bottom;
content.Dock = DockStyle.Fill;
```

### Anchoring (including V2)

- Anchors pin edges of the child to parent edges. Horizontal and vertical anchors can be combined.
- When a child is anchored Left+Right, it stretches horizontally.
- Anchor V2 is an improved computation path in `DefaultLayout` that provides better behavior for complex anchor combinations; the code uses `UpdateAnchorInfoV2` and `ComputeAnchoredBoundsV2` — consult the source for compatibility notes.

### AutoSize

- `AutoSize` controls can either grow-only or grow-and-shrink based on mode (see `AutoSizeMode`). The layout engine considers `AutoSize` when calculating parent preferred sizes.

## Preferred-size caching and Measure strategies

To keep layout performant, Orivy caches preferred-size results and uses light-weight MRU caches for text measurement.

- Preferred-size caches live in the `PropertyStore` attached to elements. See [Orivy/Layout/CommonProperties.cs](Orivy/Layout/CommonProperties.cs) and `PropertyStore` for implementation details.
- Text measurement uses `LayoutUtils.MeasureTextCache` to avoid repeated heavy text layout work; it keeps a small MRU ring buffer.

Files:
- [Orivy/Layout/CommonProperties.cs](Orivy/Layout/CommonProperties.cs)
- [Orivy/Layout/PropertyStore.cs](Orivy/Layout/PropertyStore.cs)
- [Orivy/Layout/LayoutUtils.MeasureTextCache.cs](Orivy/Layout/LayoutUtils.MeasureTextCache.cs)

### MeasureTextCache (summary)

- `MeasureTextCache` stores an unconstrained preferred size (no wrapping) and an MRU of constrained sizes (max 6 entries). It exposes `GetTextSize(string? text, SKFont? font, SKSize proposedConstraints, TextRenderOptions options)`.
- `TextRequiresWordBreak()` decides whether word-wrapping is necessary using the unconstrained width.

Example usage in `GetPreferredSize`:

```csharp
private readonly LayoutUtils.MeasureTextCache _measureCache = new();

public override SKSize GetPreferredSize(SKSize proposedSize)
{
    var options = new TextRenderOptions { Wrap = TextWrap.WordBreak };
    SKSize textSize = _measureCache.GetTextSize(Text, Font, proposedSize, options);
    return new SKSize(textSize.Width + Padding.Left + Padding.Right,
                      textSize.Height + Padding.Top + Padding.Bottom);
}
```

Note: keep a single `MeasureTextCache` instance per control instance; avoid allocating it per measure call.

## Cached bounds

The layout engine stages bounds changes between measurement phases using an internal cached-bounds store. This allows `GetPreferredSize` to inspect a candidate layout without immediately committing bounds to the element.

Key operations in `DefaultLayout`:
- `SetCachedBounds` — store a candidate rect for a child.
- `ApplyCachedBounds` — apply staged bounds to children during arrange.
- `ClearCachedBounds` — reset staged changes between top-level layouts.

This approach avoids partial state exposure: `GetPreferredSize` can simulate layout without mutating control state until the final arrange.

## Layout transactions

To batch updates and avoid layout thrashing, use `LayoutTransaction`.

- File: [Orivy/Layout/LayoutTransaction.cs](Orivy/Layout/LayoutTransaction.cs)

`LayoutTransaction` clears preferred-size caches and suspends layout while you make multiple changes. Example pattern:

```csharp
using (new LayoutTransaction(parentElement, childElement, true))
{
    // Make multiple changes that affect layout
    childElement.SuspendLayout();
    childElement.SomeProperty = newValue;
    childElement.ResumeLayout(false);
}
// When the transaction disposes, caches are cleared and layout can be performed once.
```

Use transactions when updating multiple properties (size, padding, child collection) to avoid repeating expensive measure/arrange passes.

## Examples

1) Text label preferred-size (cache-aware)

```csharp
public class Label : ElementBase
{
    private readonly LayoutUtils.MeasureTextCache _textCache = new();
    public string Text { get; set; } = string.Empty;

    public override SKSize GetPreferredSize(SKSize proposedSize)
    {
        var options = new TextRenderOptions { Wrap = TextWrap.WordBreak };
        SKSize textSize = _textCache.GetTextSize(Text, Font, proposedSize, options);
        return new SKSize(textSize.Width + Padding.Left + Padding.Right,
                          textSize.Height + Padding.Top + Padding.Bottom);
    }
}
```

2) Simple Dock layout (header/content/footer)

```csharp
// High-level usage in a window/page initialization:
header.Dock = DockStyle.Top;
footer.Dock = DockStyle.Bottom;
content.Dock = DockStyle.Fill;

// DefaultLayout will measure header/footer for preferred height and assign
// the remaining region to content.
```

3) Anchoring for responsive controls

```csharp
// Make a button stay glued to bottom-right of its container
myButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

// On parent resize the engine will compute a new position so the distance to
// the bottom-right corner remains constant, or stretch if both sides anchored.
```

## Common pitfalls & recommendations

- Do not allocate new text layouts or fonts inside `GetPreferredSize` on every call — reuse `SKFont` and use `MeasureTextCache`.
- When changing multiple properties that affect layout (Dock, Anchor, Padding, Child collection), use `SuspendLayout`/`ResumeLayout` or `LayoutTransaction` to batch updates.
- Remember that `GetPreferredSize` is a hint; controls should behave sensibly if assigned smaller or larger actual sizes.
- Avoid heavy logic in `PerformLayout` callbacks; keep layout fast and deterministic.

## Edge cases & compatibility notes

- Anchor V2: `DefaultLayout` contains an improved anchor computation path (`ComputeAnchoredBoundsV2`) — this can change sizing in complex scenarios. If you depend on legacy anchor behavior, consult the compatibility code paths in [Orivy/Layout/DefaultLayout.cs](Orivy/Layout/DefaultLayout.cs).
- Dock ordering: dock uses children z-order. Reordering child insertion affects layout.

## Implementation reference (files)

- `Orivy/Layout/DefaultLayout.cs` — engine implementation, dock/anchor/autosize logic
- `Orivy/Layout/CommonProperties.cs` — property keys, preferred-size cache utilities
- `Orivy/Layout/PropertyStore.cs` — typed property store used by layout
- `Orivy/Layout/LayoutUtils.cs` — alignment/stretch helpers, constants
- `Orivy/Layout/LayoutUtils.MeasureTextCache.cs` — text measurement MRU cache
- `Orivy/Layout/LayoutTransaction.cs` — transaction helper to batch layout changes
- `Orivy/Layout/IArrangedElement.cs` — layout contract used by controls