# File map

Open the owner before editing. Do not duplicate its job in a new type.

## Process and window

| File | Owns |
| --- | --- |
| `Orivy/Application.cs` | DPI awareness, shared font, `Run` message loop, `RenderBackend`, `OpenForms` |
| `Orivy/Controls/WindowBase.cs` | HWND, `WndProc`, show/close/modal, focus, mouse capture, DPI messages, `BeginInvoke` |
| `Orivy/Controls/WindowBase.Rendering.cs` | `IWindowRenderer`, `Invalidate` / `HandlePaint` / `RenderScene`, software back-buffer |
| `Orivy/Controls/Window.cs` | Title bar, system buttons, window drag, tabbed chrome |
| `Orivy/Native/Windows/` | P/Invoke enums and dialog COM. Window procedure stays in `WindowBase` |
| `Orivy/Rendering/RendererFactory.cs` | Software vs OpenGL. Others throw |
| `Orivy/Rendering/SoftwareRenderer.cs` | GDI DIB + CPU Skia surface |
| `Orivy/Rendering/OpenGLRenderer.cs` | WGL, `GRContext`, context-current rule |

## Control tree

| File | Owns |
| --- | --- |
| `Orivy/Controls/ElementBase.cs` | Properties, events, `Render` / `OnPaint`, input, focus, DPI, validation, `PerformLayout`, `Dispose` |
| `Orivy/Controls/ElementBase.IArrangedElement.cs` | `IArrangedElement` explicit implementation |
| `Orivy/Controls/ElementBase.VisualStyles.cs` | `ConfigureVisualStyles`, snapshots, state transitions |
| `Orivy/Controls/ElementBase.MotionEffects.cs` | Motion effects and their timer |
| `Orivy/Controls/ElementBase.BackgroundImages.cs` | Slideshow, blur, image transitions |
| `Orivy/Controls/ElementBase.RemoteImages.cs` | URL image load |
| `Orivy/Controls/ElementBase.Backdrop.cs` | Mica / acrylic / glass element backdrop |
| `Orivy/Controls/IMouseCaptureLost.cs` | Cancel drag when capture is stolen |
| `Orivy/Collections/ElementCollection.cs` | Children, z-order, parent pointer |

Leaf controls live in `Orivy/Controls/<Name>.cs`. Large ones split further (`TabView`, `GridList`, `RichText`, `Markdown`). Copy the closest existing control, not `ElementBase` itself.

## Layout, style, binding, motion

| File | Owns |
| --- | --- |
| `Orivy/Layout/DefaultLayout.cs` | Dock, anchor, autosize, cached bounds |
| `Orivy/Layout/LayoutTransaction.cs` | Preferred-size cache clear + `PerformLayout` (`internal`) |
| `Orivy/Layout/CommonProperties.cs` | Packed dock/anchor/autosize/margin state |
| `Orivy/Layout/PropertyNames.cs` | Layout property name constants |
| `Orivy/ColorScheme.cs` | Palette, dark mode, accent seed, `ThemeChanged` |
| `Orivy/Styling/ElementVisualStyles.cs` | Style rules and builders |
| `Orivy/Binding/BindingExtensions.cs` | `Link` / `From` / `FromData` |
| `Orivy/Animation/AnimationManager.cs` | Shared timer, easing, `StartNewAnimation` / `Stop` |

## Studio

| File | Owns |
| --- | --- |
| `Orivy.Studio/StudioWindow.cs` | Shell, document tabs, panel rebind |
| `Orivy.Studio/DesignSurface.cs` | Zoom, overlay, adorners, snap, `PrepareForDesign`, commands |
| `Orivy.Studio/Canvas/DragLayer.cs` | Toolbox ghost drag |
| `Orivy.Studio/Canvas/SelectionService.cs` | Multi-selection, primary anchor |
| `Orivy.Studio/History/CommandStack.cs` | `Execute` vs `Push` |
| `Orivy.Studio/Toolbox/ControlCatalog.cs` | Reflection discovery |
| `Orivy.Studio/CodeGenerator.cs` | Designer C# emit |
| `Orivy.Studio/CodeImporter.cs` | Designer C# parse |
| `Orivy.Studio/Documents/DesignDocument.cs` | One document, dirty flag |
| `Orivy.Studio/Panels/` | Toolbox, layers, layout helper, start screen |

## Docs

Behavior changes that affect callers: update the page under `docs/` (`controls.md`, `layout.md`, `styling.md`, `binding.md`, `rendering.md`, `animation.md`, `architecture.md`) and link it from `docs/toc.yml` if it is new. `Orivy.Studio/README.md` is the designer contract.
