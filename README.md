# Orivy

Orivy is a Windows-first retained-mode UI framework for .NET 8 built on top of SkiaSharp. It combines a custom layout engine, a lightweight control tree, state-driven styling, animation, data binding, and multiple rendering backends in a single codebase.

## Highlights

- retained-mode control tree with explicit Measure/Arrange layout
- Skia-based rendering with software and OpenGL paths
- state-driven styling through `ColorScheme` and `ConfigureVisualStyles(...)`
- built-in animation, motion effects, and theme transitions
- `Link(...)` data binding with `DataContext` and collection support
- WinForms-compatible API surface (`DockStyle`, `AnchorStyles`, `DialogResult`, `Application`, common dialogs) to ease migration
- example app and settings-preview app included in the solution

## Projects

| Project | Purpose |
| --- | --- |
| `Orivy` | Core UI framework library |
| `Orivy.Example` | Runnable sample app for controls, visual styles, binding, and rendering behavior |
| `Orivy.SettingsPreview` | Small settings-window preview app built on the same framework |
| `Orivy.Studio` | Figma-style visual designer: live WYSIWYG canvas, move/resize/snap, PropertyGrid editing, Designer-code export ([details](Orivy.Studio/README.md)) |

## Quick Start

Build the solution:

```powershell
dotnet build Orivy.sln -c Debug
```

Run the example app:

```powershell
dotnet run --project Orivy.Example/Orivy.Example.csproj -c Debug
```

Run the settings-preview app:

```powershell
dotnet run --project Orivy.SettingsPreview/Orivy.SettingsPreview.csproj -c Debug
```

## Documentation

The documentation hub lives under [`docs/`](docs/README.md).

- [Documentation Hub](docs/README.md)
- [Getting Started](docs/getting-started.md)
- [Overview](docs/overview.md)
- [Architecture](docs/architecture.md)
- [Styling System](docs/styling.md)
- [Controls](docs/controls.md)
- [Binding](docs/binding.md)
- [Rendering](docs/rendering.md)

If you are exploring Orivy for the first time, start with `docs/getting-started.md`, then `docs/overview.md`, then `docs/styling.md` and `docs/controls.md`.

## Styling at a Glance

Orivy's styling model is not limited to color constants. The framework combines:

- `ColorScheme` for theme colors, dark/light mode, accent seeding, and animated theme transitions
- `ElementBase.ConfigureVisualStyles(...)` for hover, pressed, checked, focused, disabled, and custom predicate-based visual rules
- `WindowThemeType` for native host effects such as `Mica`, `Acrylic`, and `Tabbed`

Example:

```csharp
ColorScheme.SetThemeInstant(dark: false);
ColorScheme.SetPrimarySeedColor(new SKColor(33, 150, 243));

var button = new Button
{
    Text = "Deploy",
    AccentMotionEnabled = true
};

button.ConfigureVisualStyles(styles =>
{
    styles
        .DefaultTransition(TimeSpan.FromMilliseconds(140), AnimationType.CubicEaseOut)
        .OnHover(rule => rule.Background(ColorScheme.Primary.Brightness(0.06f)))
        .OnChecked(rule => rule.Background(ColorScheme.Primary.Brightness(-0.04f)))
        .OnPressed(rule => rule.Opacity(0.94f));
});
```

See [docs/styling.md](docs/styling.md) for the full theme and styling guide.
