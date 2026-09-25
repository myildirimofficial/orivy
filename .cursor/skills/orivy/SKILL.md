---
name: orivy
description: >-
  Applies Orivy framework conventions when changing controls, layout, Skia
  painting, ColorScheme styling, data binding, animation, Win32 window
  messages, or the Orivy.Studio designer. Use when editing files under Orivy/,
  Orivy.Studio/, Orivy.Example/, or Orivy.SettingsPreview/, or when the user
  mentions ElementBase, DesignSurface, toolbox, InitializeComponent,
  Measure/Arrange, or a new control.
---

# Orivy development

Orivy is a .NET 8 retained-mode UI framework (SkiaSharp, Win32 message loop) plus a WYSIWYG designer. Match existing types. Do not add a second layout, paint, or persistence path.

## Before editing

1. Classify the change with the table below.
2. Open the owner file in [map.md](map.md). Read that file and one similar control before writing code.
3. Follow the hard rules, then the task file if one is linked.

## Classify

| Change | Follow |
| --- | --- |
| New or changed control, paint, input, theme look | [controls.md](controls.md) |
| Dock, Anchor, AutoSize, preferred size, `PerformLayout` | Layout section in [map.md](map.md) |
| Studio canvas, toolbox, undo, save/open | [studio.md](studio.md) |
| Example or settings window | Example section in [studio.md](studio.md) |
| Public behavior a reader would look up | Also update the matching page under `docs/` and `docs/toc.yml` |

## Hard rules

- `ImplicitUsings` is disabled. Add explicit `using`s. Nullable is on. Namespace matches the folder (`Orivy.Controls`, `Orivy.Layout`, `Orivy.Studio`, …).
- Private fields are `_camelCase`. One primary type per file. Split a large type as `TypeName.Concern.cs` partials. Put enums in `Orivy/Enums`, value types in `Orivy/Objects`.
- Identifiers, comments, and docs stay English.
- Visual-only change: `Invalidate()`. Size or child relationship change: layout (inside `Orivy`, `LayoutTransaction.DoLayout` / `DoLayoutIf` with `PropertyNames`). Preferred-size inputs: `InvalidateMeasure()` (internal; parent layout follows when `AutoSize` is set). Do not `PerformLayout` on every animation frame.
- `ElementBase.Invalidate` marks dirty and hops once to the window. Do not walk parents calling `Invalidate`. The comment in that method is load-bearing: cascades kill frame rate.
- Do not `new SKPaint`, `new SKFont`, or `new SKPath` inside `OnPaint` / per-frame ticks. Reuse fields owned by the control and dispose them in `Dispose(bool)`. Never dispose `Application.SharedDefaultFont`.
- Theme colors come from `ColorScheme` inside `ConfigureVisualStyles`. Do not copy a theme color into a field once and forget `ThemeChanged` (the base already subscribes).
- `ElementBase.BeginInvoke` / `Invoke` run `DynamicInvoke` on the caller thread. They do not marshal. UI-thread work goes through `WindowBase.BeginInvoke(Action)` (`WM_APP_INVOKE`).
- `AnimationManager` ticks on a shared `System.Timers.Timer` thread. Progress handlers may `Invalidate` and write fields the paint path reads. They must not `PerformLayout`, create windows, or touch the renderer. Dispose the manager and unsubscribe.
- Mouse capture is `GetParentWindow()?.SetMouseCapture(this)` / `ReleaseMouseCapture(this)`, not a raw `SetCapture` alone. Any drag or scrub that keeps state must implement `IMouseCaptureLost.OnMouseCaptureLost` and clear that state there. `WM_CAPTURECHANGED` nulls the window’s capture field before the callback. `MouseUp` is not guaranteed.
- Modal dialogs must not post `WM_QUIT`. That ends the outer `Application.Run`.
- OpenGL calls require that renderer’s context to be current. Do not add a DX11/Vulkan/Metal backend; `RendererFactory` only builds Software and OpenGL.

## Verify

```powershell
dotnet build Orivy.sln -c Debug
```

Run the smallest project that shows the change:

```powershell
dotnet run --project Orivy.Example/Orivy.Example.csproj -c Debug
dotnet run --project Orivy.Studio/Orivy.Studio.csproj -c Debug
dotnet run --project Orivy.SettingsPreview/Orivy.SettingsPreview.csproj -c Debug
```

Studio helpers: `ORIVY_STUDIO_SEED=1`, `ORIVY_STUDIO_SKIP_START=1`, `ORIVY_STUDIO_TEST_FOLDER=<path>`.

There is no automated UI test suite. After a control or canvas change, exercise the gesture (click, drag, capture-loss, zoom, save/open) in the app that hosts it.
