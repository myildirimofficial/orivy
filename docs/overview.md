
Overview
========

Orivy is a retained-mode UI framework built on SkiaSharp. It provides a lightweight, high-performance rendering pipeline with an explicit layout system (Measure/Arrange), a visual styling system, an animation subsystem, and a small set of ready-to-use controls.

Design goals
- Deterministic rendering through Skia (`SKCanvas`) — no native OS controls are used for rendering.
- Explicit layout: a two-phase Measure/Arrange layout pipeline inspired by WinForms/WPF but implemented in a custom, lightweight layout engine.
- Composable, testable controls: `ElementBase` is the base class and defines lifecycle, properties, and events.
- Performance-friendly APIs: reuse paints, fonts and avoid allocations in hot paths.

Key concepts
- Retained-mode UI: the framework keeps a tree of controls (elements). Each frame the tree may be measured, arranged, and painted; the framework manages repaint invalidation and ordering.
- Measure/Arrange: controls must implement `GetPreferredSize(SKSize)` to report desired size, and the layout engine calls `IArrangedElement.SetBounds(SKRect, BoundsSpecified)` to apply final bounds.
- Visual styles: controls can configure visual styles via `ConfigureVisualStyles()` and `ColorScheme` provides centralized theme colors.
- Binding: a small binding system allows `Link()`-style bindings for properties and DataContext-based bindings.
- Animation: `AnimationManager` provides an easy-to-use, timer-driven animation helper that integrates with control invalidation.

Where to start in the code
- Application entrypoint and window loop: [Orivy/Application.cs](Orivy/Application.cs)
- The element base and control lifecycle: [Orivy/Controls/ElementBase.cs](Orivy/Controls/ElementBase.cs)
- Common controls: [Orivy/Controls](Orivy/Controls)
- Layout engine: [Orivy/Layout](Orivy/Layout)
- Rendering helpers and GPU path: [Orivy/Rendering](Orivy/Rendering)

