Architecture — Core Components
================================

This architecture reference explains how Orivy is composed into consistent layers, from startup and message loop to layout, render, and event dispatch.

## 1. Startup and window lifecycle

- `Application` (Orivy/Application.cs)
  - Static constructor sets DPI awareness (`EnableDpiAwareness`) before any windows are created.
  - Holds shared `SKFont` via `SharedDefaultFont` and propagates font change events to open windows.
  - `Run(WindowBase window)`: creates window handle, shows window, then enters Win32 message loop (`GetMessage`, `TranslateMessage`, `DispatchMessage`).

- `WindowBase` (Orivy/Controls/WindowBase.cs)
  - Implements top-level window behavior and hosting.
  - Integrates with `IWindowRenderer` to own rendering loop and automatic `Invalidate`.
  - Handles window messages (`WndProc`) and orchestrates input, resizing, DPI changes, and lifecycle events.

- `Window` (Orivy/Controls/Window.cs)
  - Higher-level window semantics (title bar, content panel, page transitions, and in-window overlays).
  - Contains specific rendering for decorated chrome and tab-like interactions.

## 2. UI model and control tree

- `ElementBase` (Orivy/Controls/ElementBase.cs)
  - Base class for all UI elements; includes properties, events, layout references, hit testing, and notifications.
  - Implements `IArrangedElement` to participate in layout engine.
  - Exposes lifecycle methods: `PerformLayout`, `OnPaint`, `OnMouse...`, `OnKey...`, `OnDpiChanged`, `Dispose`, etc.

- `ElementCollection` (Orivy/Collections/ElementCollection.cs)
  - Tracks children, ensures Z-order and parent references.
  - Supports add/remove semantics and notify events to controlling containers.

- `IElement` / `IArrangedElement`
  - `IArrangedElement` (Orivy/Layout/IArrangedElement.cs) defines the layout contract.

## 3. Layout subsystem

- `LayoutEngine` (Orivy/Layout/LayoutEngine.cs)
  - Abstract base for measure/arrange implementations.
  - Default layout engine: `DefaultLayout` (Orivy/Layout/DefaultLayout.cs).

- `DefaultLayout` behavior
  - Supports Dock, Anchor, AutoSize, and priority ordering.
  - Uses `CommonProperties` to store state (Dock, Anchor, AutoSize flags, preferred sizes).
  - Contains optimized cache path with `SetCachedBounds`, `ApplyCachedBounds`, and preferred-size calculations.
  - Uses `LayoutTransaction` to batch layout updates and clear caches (`Orivy/Layout/LayoutTransaction.cs`).

## 4. Rendering subsystem

- `IWindowRenderer` (Orivy/Rendering/IWindowRenderer.cs)
  - Interface for render backends: `SoftwareRenderer`, `OpenGLRenderer`, and `RenderBackend` values.

- `RendererFactory` (Orivy/Rendering/RendererFactory.cs)
  - Creates chosen backend based on ability and configuration.

- `SoftwareRenderer` and `OpenGLRenderer` implementation details
  - `SoftwareRenderer` uses GDI DIBs and `SKSurface.Create(info, pixelBuffer)`.
  - `OpenGLRenderer` creates OpenGL context, `GRContext`, and `SKSurface` for GPU rendering.

- `ColorScheme` (Orivy/ColorScheme.cs)
  - Central theme definitions with light/dark mode support and animated transitions.

## 5. Input and event model

- Hit testing via `ElementBase.FindTopmostInputTarget`.
- Mouse events dispatched through `ElementBase.OnMouseMove/OnMouseDown/OnMouseUp/OnMouseClick`.
- Keyboard events route via focused element (FocusManager) and tab navigation.
- Event lifecycle: `OnLoad` / `OnUnload`, focus events, validation events.

## 6. Binding subsystem

- `BindingExtensions.Link()` to start fluent binding for target property.
- `BindingTargetBuilder` and `BindingSourceBuilder` for explicit and DataContext bindings.
- `PropertyBinding` in `BindingExtensions.cs` wires source and target updates with `INotifyPropertyChanged`.
- `ObservableObject` base class provides property notification wiring.

## 7. Animation subsystem

- `AnimationManager` (Orivy/Animation/AnimationManager.cs) is primary animator.
- `ValueProvider<T>` controls low-level value interpolation and easing.
- `ValueFactories` and `EasingMethods` compose physical animation semantics.

## 8. Styling subset

- `ElementBase.VisualStyles` manages visual state snapshots and transitions.
- `ElementBase.MotionEffects` provides reusable motion effect rendering.
- `Styling` classes centralize style keys and interpolations.

## 9. Performance and robustness

- Maintain low allocations in hot paths (`OnPaint`, `GetPreferredSize`).
- Use `SuspendLayout`, `ResumeLayout`, and `LayoutTransaction` to avoid thrash.
- Dispose of Skia resources in `Dispose()`.
- Fallback and resilience: GPU pass falling back to software when unavailable.

## 10. Where to look

- `Orivy/Application.cs`
- `Orivy/Controls/WindowBase.cs`, `Orivy/Controls/Window.cs`, `Orivy/Controls/ElementBase.cs`
- `Orivy/Layout/*`
- `Orivy/Rendering/*`
- `Orivy/Animation/*`
- `Orivy/Binding/*`
- `Orivy/Collections/ElementCollection.cs`
