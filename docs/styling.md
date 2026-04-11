# Orivy Styling System

This page documents Orivy's styling system: theme colors, visual style rules, theme transitions, elevation behavior, and native window appearance hooks. If you are looking for motion effects and ambient decorative animation, continue with [Visual](visual.md) after reading this page.

## Overview

Orivy styling is split across three layers:

1. `ColorScheme` defines the shared palette, theme state, and color derivation rules.
2. `ElementBase.ConfigureVisualStyles(...)` applies state-driven appearance rules to individual controls.
3. `WindowBase.WindowThemeType` bridges Orivy theme colors into native window chrome on supported Windows hosts.

Together these layers give controls a common visual language while still letting each control opt into richer hover, pressed, focused, and disabled states.

## 1. Theme Palette with `ColorScheme`

`ColorScheme` is the global styling entry point in `Orivy/ColorScheme.cs`.

Core responsibilities:

- expose theme-aware surface colors such as `Surface`, `SurfaceVariant`, `SurfaceContainer`, and `SurfaceContainerHigh`
- expose semantic colors such as `Primary`, `Error`, `Success`, and `Warning`
- derive `ForeColor`, `Outline`, `BorderColor`, and `ShadowColor` from the current theme state
- animate background and theme changes through `StartThemeTransition(...)`
- notify controls through the `ThemeChanged` event

Useful APIs:

- `ColorScheme.IsDarkMode`
- `ColorScheme.SetThemeInstant(bool dark)`
- `ColorScheme.StartThemeTransition(SKColor targetBackground)`
- `ColorScheme.SetPrimarySeedColor(SKColor seed)`
- `ColorScheme.ThemeChanged`
- `ColorScheme.DrawDebugBorders`

Notes:

- `ForeColor` is derived from the current surface color, so controls usually do not need to hard-code text contrast.
- `Primary` is seeded independently and can be changed without replacing the whole theme.

## 2. State-Driven Control Styling

Every `ElementBase` can opt into visual styles through `ConfigureVisualStyles(...)`. The styling engine is implemented in:

- `Orivy/Controls/ElementBase.VisualStyles.cs`
- `Orivy/Styling/ElementVisualStyles.cs`

The system is snapshot-based:

- the control captures a base visual snapshot from its current properties
- matching rules are applied on top of that snapshot
- transitions interpolate from the current effective snapshot to the new target snapshot

Supported style values:

- width and height
- background and foreground colors
- border thickness and border color
- radius
- shadows
- opacity

Built-in state flags:

- `PointerOver`
- `Pressed`
- `Focused`
- `Disabled`
- `Hidden`

## 3. Style Builder Surface

The fluent builder API is centered around `ElementVisualStyleBuilder`.

Most useful entry points:

- `DefaultTransition(...)`
- `Base(...)`
- `OnHover(...)`
- `OnPressed(...)`
- `OnFocused(...)`
- `OnDisabled(...)`
- `OnHidden(...)`
- `When(ElementVisualStates, ...)`
- `When((element, state) => ..., ...)`

Example:

```csharp
card.ConfigureVisualStyles(styles =>
{
    styles
        .DefaultTransition(TimeSpan.FromMilliseconds(180), AnimationType.CubicEaseOut)
        .Base(baseStyle => baseStyle
            .Background(ColorScheme.Surface)
            .Foreground(ColorScheme.ForeColor)
            .Border(1)
            .BorderColor(ColorScheme.Outline)
            .Radius(16))
        .OnHover(rule => rule
            .Background(ColorScheme.SurfaceContainer)
            .BorderColor(ColorScheme.Primary))
        .OnPressed(rule => rule
            .Opacity(0.94f))
        .When(
            (element, state) => Equals(element.Tag, "danger") && state.IsPointerOver,
            rule => rule
                .Background(ColorScheme.Error)
                .Foreground(SKColors.White));
});
```

## 4. Theme-Aware Control Pattern

Controls with richer appearance often follow this pattern:

1. set basic appearance defaults in the constructor
2. subscribe to `ColorScheme.ThemeChanged`
3. run a dedicated `ApplyTheme()` method
4. call `ReevaluateVisualStyles()` when state-dependent styling should be recalculated

`ComboBox` and `ColorPicker` are good examples of this pattern. They rebuild their theme-derived colors when the global palette changes, then refresh their effective visual styles without requiring the entire control to be recreated.

The default `Button` also demonstrates this flow by defining a full visual-style profile in its constructor, including hover, pressed, focused, and disabled states.

## 5. Native Window Theme Integration

`WindowBase` exposes `WindowThemeType` to align the native window with Orivy's theme. Supported values are:

- `None`
- `Mica`
- `Acrylic`
- `Tabbed`

The native host integration uses the current `ColorScheme.Surface` color when applying supported effects. This gives top-level windows a more coherent look when the app theme changes.

Example:

```csharp
var window = new Window
{
    Text = "Orivy Studio",
    WindowThemeType = WindowThemeType.Mica
};

ColorScheme.SetThemeInstant(dark: true);
```

## 6. Elevation and Flat Design

`Orivy/Helpers/ElevationHelper.cs` provides higher-level styling helpers for depth and polish.

Key behaviors:

- `DrawElevation(...)` renders theme-aware elevation shadows and dark-mode tinting
- `DrawGlassEffect(...)` adds a gradient overlay for glass-like surfaces
- `DrawStateLayer(...)` paints hover or pressed overlays

Use these helpers when a custom control needs manual paint logic but still wants to match the shared theme vocabulary.

## 7. Best Practices

- Prefer `ColorScheme` properties over hard-coded colors for default surfaces and text.
- Use `ConfigureVisualStyles(...)` for state changes instead of manually mutating colors in every mouse event.
- Reuse a single `ApplyTheme()` method when a control listens to `ThemeChanged`.
- Keep visual style rules small and composable; put the common shape in `Base(...)` and state deltas in individual rules.
- Use `ReevaluateVisualStyles()` after state or theme changes that affect predicate-based rules.
- Reserve motion and ambient decorative effects for `ConfigureMotionEffects(...)`; keep them separate from the core styling contract.

## 8. Source Reference

- `Orivy/ColorScheme.cs`
- `Orivy/Controls/ElementBase.VisualStyles.cs`
- `Orivy/Styling/ElementVisualStyles.cs`
- `Orivy/Helpers/ElevationHelper.cs`
- `Orivy/Controls/Button.cs`
- `Orivy/Controls/ComboBox.cs`
- `Orivy/Controls/ColorPicker.cs`
- `Orivy/Controls/WindowBase.cs`
- `Orivy/WindowThemeType.cs`
