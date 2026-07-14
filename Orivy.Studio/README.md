# Orivy.Studio

A Figma-style visual design platform for Orivy. Because Orivy paints its own controls with
SkiaSharp, the canvas is **true WYSIWYG** — every element on the surface is a real, live Orivy
control rendered exactly as it will appear at runtime.

## Architecture

The app is built as independent modules so features can grow without entangling each other:

| Module | File(s) | Responsibility |
| --- | --- | --- |
| **Catalog** | `Toolbox/ControlCatalog.cs` | Reflection-based discovery of every instantiable `ElementBase` in Orivy.Controls. No static list — a new control appears automatically. |
| **Selection** | `Canvas/SelectionService.cs` | Multi-selection state with a primary anchor; raises `Changed`. |
| **History** | `History/CommandStack.cs` | Undo/redo command stack (`Execute` for new ops, `Push` for already-applied interactive edits). |
| **Canvas Engine** | `DesignSurface.cs` | Zoom/pan viewport (rides `ChildRenderScale`), overlay input, selection adorners, grip resize, grid + smart-guide snapping, alignment/distribution, Z-order, preview mode. Every mutation is an undoable command. |
| **Persistence** | `Persistence/DesignSerializer.cs` | Save/load the document as JSON (`.orivy.json`). |
| **Export** | `CodeGenerator.cs` | Emits WinForms-style `InitializeComponent` Designer code. |
| **UI panels** | `Panels/ToolboxPanel.cs`, `Panels/LayersPanel.cs` | Searchable categorized toolbox; layers outline with visibility/lock toggles. |
| **Shell** | `StudioWindow.cs` | Toolbar + panel layout, wires all modules together. |

```
┌ Toolbar · Undo/Redo · New/Open/Save · Export · Preview · Zoom · Snap/Guides/Theme ┐
├──────────────┬────────────────────────────────────┬──────────────────────────────┤
│ Toolbox      │            Canvas                  │ Layers (visibility · lock)   │
│ (auto-       │  ┌ design root (the "form") ┐      ├──────────────────────────────┤
│  discovered, │  │  live Orivy controls     │      │ Properties                   │
│  searchable, │  │  + adorners / guides     │      │ (live PropertyGrid, edits    │
│  categorized)│  └──────────────────────────┘      │  become undoable commands)   │
└──────────────┴────────────────────────────────────┴──────────────────────────────┘
```

## The core design decision

`DesignSurface` hosts the designed root plus a transparent **overlay** on top. Designed controls
render live but receive no input in design mode; the overlay is the topmost hit target and owns all
designer gestures. Zoom is applied through `ChildRenderScale`, which Orivy already honours for both
**rendering and input routing**, so hit-testing stays correct at any zoom level.

## Features

**Canvas** — infinite pan/zoom (Ctrl+wheel zoom-to-cursor, wheel/Shift+wheel pan, middle-drag pan,
Fit), 8px grid with snap, magenta smart guides (edge/center alignment to siblings and the frame),
marquee box-selection, multi-select move, 8-grip resize, arrow-key nudge, alignment &
distribution, bring-to-front/send-to-back, lock, per-control visibility, right-click context menu,
and a **Preview** mode that makes the design fully interactive.

**Inspector** — Orivy's own `PropertyGrid` edits the live control (color→ColorPicker,
enum→drop-down, number→NumericUpDown, date→DatePicker). Edits are recorded as undoable commands.

**History** — full undo/redo (Ctrl+Z / Ctrl+Y) over every add, delete, move, resize, reorder and
property edit.

**Files** — Save/Open JSON projects (`.orivy.json`), Export C# Designer code (preview + Save .cs).

## Shortcuts

| Action | Input |
| --- | --- |
| Select / multi-select | click · Ctrl+click · drag marquee |
| Move / resize | drag body · drag a grip |
| Nudge | arrows (Shift = 8px) |
| Delete / Duplicate | `Del` / `Ctrl+D` |
| Select all | `Ctrl+A` |
| Undo / Redo | `Ctrl+Z` / `Ctrl+Y` |
| Save | `Ctrl+S` |
| Zoom | `Ctrl`+wheel · toolbar ± · Fit |
| Pan | wheel · `Shift`+wheel · middle-drag |

## Roadmap

- Container drop targets (design inside Card/Panel/TabView/SplitContainer children)
- Rotation handles and design tokens / component variants
- Dock/Anchor + Auto-Layout visual editors, responsive breakpoints
- Round-trip code import and event-stub generation in the exporter
- Assets/icons manager and a plugin system for custom design-time behaviors
