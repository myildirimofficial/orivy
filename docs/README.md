# Orivy Documentation Hub

This folder is the main entry point for Orivy's user-facing documentation. It is organized to support three different needs at the same time:

- fast onboarding for first-time users
- deep technical references for framework contributors
- example-driven learning for day-to-day feature work

Use this page as the map for the rest of the documentation set, whether you are reading the docs in the repository or through a DocFX site build.

## Start Here

Choose the shortest path based on what you need to do:

| Goal | Read first | Then continue with |
| --- | --- | --- |
| Build the repo and run the sample app | [Getting Started](getting-started.md) | [Overview](overview.md), [examples/simple-window.md](examples/simple-window.md) |
| Understand how Orivy is structured | [Overview](overview.md) | [Architecture](architecture.md), [Rendering](rendering.md) |
| Theme and style controls consistently | [Styling](styling.md) | [Visual](visual.md), [Controls](controls.md) |
| Build or extend custom controls | [Controls](controls.md) | [Layout](layout.md), [Visual](visual.md), [Animation](animation.md) |
| Wire UI to data models | [Binding](binding.md) | [Controls](controls.md), [examples/binding-sample.md](examples/binding-sample.md) |
| Diagnose paint, layout, or performance issues | [Rendering](rendering.md) | [Layout](layout.md), [Architecture](architecture.md) |
| Browse runnable patterns before reading theory | [examples/controls-samples.md](examples/controls-samples.md) | [examples/animation-sample.md](examples/animation-sample.md), [examples/binding-sample.md](examples/binding-sample.md) |

## Documentation Map

The documentation is split into four layers so readers can move from orientation to implementation detail without losing context.

### 1. Foundations

- [Getting Started](getting-started.md): prerequisites, build/run flow, troubleshooting, and the fastest way to open the sample app
- [Overview](overview.md): design goals, retained-mode model, layout basics, and the source areas worth learning first
- [Architecture](architecture.md): how startup, windows, controls, layout, rendering, binding, animation, and styling fit together

### 2. Core Systems

- [Layout](layout.md): the Measure/Arrange pipeline, docking, anchoring, caching, and layout transactions
- [Controls](controls.md): `ElementBase`, control lifecycle, painting, input routing, and custom control guidance
- [Binding](binding.md): `Link`, `From`, `FromData`, collection binding, interactions, and lifecycle management
- [Animation](animation.md): `AnimationManager`, easing, value providers, and common animation patterns

### 3. Styling, Visual and Rendering

- [Styling](styling.md): `ColorScheme`, theme transitions, control-level visual rules, elevation, and native window theme integration
- [Visual](visual.md): visual states, motion effects, animated style snapshots, and the new button behavior
- [Rendering](rendering.md): Skia pipeline, software vs GPU paths, paints, fonts, and diagnostics

### 4. Examples and Reference

- [examples/simple-window.md](examples/simple-window.md): minimal application window setup
- [examples/controls-samples.md](examples/controls-samples.md): common control patterns and composition ideas
- [examples/binding-sample.md](examples/binding-sample.md): view model and binding examples
- [examples/animation-sample.md](examples/animation-sample.md): animation usage in practice
- [api.md](api.md): how to generate API documentation with DocFX

## Recommended Reading Paths

### Application Developer

1. Read [Getting Started](getting-started.md).
2. Skim [Overview](overview.md) to learn the vocabulary used across the docs.
3. Read [Styling](styling.md) before customizing theme-aware controls.
4. Jump to [Controls](controls.md) and [Binding](binding.md) for everyday UI work.
5. Use the example pages whenever you need a working pattern.

### Framework Contributor

1. Read [Overview](overview.md) and [Architecture](architecture.md).
2. Continue with [Layout](layout.md), [Styling](styling.md), [Rendering](rendering.md), and [Controls](controls.md).
3. Review [Visual](visual.md) and [Animation](animation.md) before changing style or motion behavior.
4. Generate the API reference through [api.md](api.md) when you want a symbol-level index.

### Debugging and Performance Pass

1. Start with [Rendering](rendering.md) for backend and Skia resource guidance.
2. Read [Layout](layout.md) for preferred-size caching, staged bounds, and batching.
3. Use [Architecture](architecture.md) to trace the runtime flow between window, renderer, layout, and control tree.

## Source Code Entry Points

These are the most useful code paths to open alongside the docs:

- `Orivy/Application.cs`: process startup, DPI setup, shared font lifecycle, and message loop entry
- `Orivy/Controls/WindowBase.cs`: top-level window orchestration, renderer integration, input dispatch, resize, and DPI events
- `Orivy/Controls/ElementBase.cs`: the core control model, layout participation, painting, focus, and invalidation
- `Orivy/ColorScheme.cs`: theme palette, dark mode state, accent color seeding, and theme transitions
- `Orivy/Controls/ElementBase.VisualStyles.cs`: snapshot resolution, animated style transitions, and state refresh logic
- `Orivy/Styling/ElementVisualStyles.cs`: style rules, builders, transitions, and predicate-based styling
- `Orivy/Layout/DefaultLayout.cs`: docking, anchoring, autosize, cached bounds, and preferred-size logic
- `Orivy/Binding/BindingExtensions.cs`: property binding, collection replacement, and conversion rules
- `Orivy/Animation/AnimationManager.cs`: animation timing, progress updates, and transition control
- `Orivy/Rendering/RendererFactory.cs`: backend selection and fallback behavior

## DocFX Site Workflow

This folder is also prepared for a DocFX site build.

- `docfx.json` defines metadata generation and site output
- `index.md` is the DocFX landing page
- `toc.yml` defines the navigation tree shown in the generated site
- `api.md` explains how to generate the API reference

Typical local build flow:

```powershell
cd docs
docfx metadata docfx.json
docfx build docfx.json
```

The generated static site is written to `docs/_site`.

## Documentation Maintenance Rules

When you add or update documentation, keep the structure consistent:

1. Link every new page from this hub and from `toc.yml` so it is discoverable in both GitHub and DocFX views.
2. Prefer conceptual pages for "why" and "how", and example pages for runnable snippets and concrete workflows.
3. Reference source files with code spans such as `Orivy/Controls/ElementBase.cs` unless the target is another documentation page.
4. Keep examples close to real Orivy APIs and update them when control, binding, or rendering behavior changes.
5. Keep styling and theme behavior documented in `styling.md`, and reserve `visual.md` for motion and state-visual examples.
6. Include at least one of these in every substantial page: a mental model, a lifecycle explanation, a working snippet, or a troubleshooting section.
7. Remove author notes, TODO-style drafting text, and one-off generation artifacts before committing docs.

## What This Hub Covers

This page is intentionally a navigation and maintenance document. Detailed API behavior, implementation notes, and system-specific guidance live in the linked pages above.
