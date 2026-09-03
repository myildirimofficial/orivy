# Orivy.Studio

A Figma-style visual design platform for Orivy. Because Orivy paints its own controls with
SkiaSharp, the canvas is **true WYSIWYG** — every element on the surface is a real, live Orivy
control rendered exactly as it will appear at runtime.

## Architecture

The app is built as independent modules so features can grow without entangling each other:

| Module | File(s) | Responsibility |
| --- | --- | --- |
| **Catalog** | `Toolbox/ControlCatalog.cs` | Reflection-based discovery of every instantiable `ElementBase` in Orivy.Controls. No static list — a new control appears automatically. Frees placed controls from ctor `AutoSize` so the designer fully owns their bounds. |
| **Selection** | `Canvas/SelectionService.cs` | Multi-selection state with a primary anchor; raises `Changed`. |
| **History** | `History/CommandStack.cs` | Undo/redo command stack (`Execute` for new ops, `Push` for already-applied interactive edits). |
| **Canvas Engine** | `DesignSurface.cs` | Zoom/pan viewport (rides `ChildRenderScale`), overlay input, selection adorners, grip resize, grid + smart-guide snapping, alignment/distribution, Z-order, preview mode, external `DropAt`. Every mutation is an undoable command. |
| **Documents** | `Documents/DesignDocument.cs` | One design document (a `Container` page hosting a surface) — the unit of the multi-document TabView. |
| **Drag & drop** | `Canvas/DragLayer.cs` | Full-window capture overlay that ghosts a toolbox entry to the cursor and drops it on the active canvas. |
| **Persistence** | `CodeGenerator.cs` / `CodeImporter.cs` | Save/Open round-trips through plain Designer C# code (fields + `InitializeComponent`, Dock/Anchor/ZOrder/Visible included) — no separate project file format. |
| **UI panels** | `Panels/ToolboxPanel.cs` + `Panels/ToolboxList.cs`, `Panels/LayersPanel.cs`, `Panels/LayoutHelperBar.cs` | Owner-drawn categorized toolbox (glyph badges, search, double-click or drag); rebindable layers outline with visibility/lock; quick Dock/Anchor editors. |
| **Shell** | `StudioWindow.cs` | Toolbar + an embedded-tab TabView of documents inside resizable `SplitContainer` columns (left \| canvas \| right); rebinds panels to the active document. |

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

**Toolbox** — every entry can be **double-clicked** or **dragged** onto the canvas; a ghost chip
follows the cursor and the control drops where you release.

**Multi-document** — the center is a TabView; **＋ Doc** opens another Window design and every panel
(toolbox, layers, inspector, layout, history) rebinds to the active tab.

**Inspector** — Orivy's own `PropertyGrid` edits the live control (color→ColorPicker,
enum→drop-down, number→NumericUpDown, date→DatePicker), plus a quick **Dock/Anchor** helper bar.
Every edit relayouts the canvas and is recorded as an undoable command.

**History** — full undo/redo (Ctrl+Z / Ctrl+Y) over every add, delete, move, resize, reorder,
dock/anchor and property edit.

**Files** — no project format: Save/Open read and write plain Designer C# code directly (any `.cs`
file with a recognizable `InitializeComponent` opens in the visual designer); Export/Import Designer
Code offer the same round trip via a paste-in dialog instead of a file. Pick a folder and every file
and subfolder in it shows up in the Explorer sidebar — nothing else is required to start editing.

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

- Container drop targets (design *inside* Card/Panel/TabView/SplitContainer children)
- Rotation handles and design tokens / component variants
- Auto-Layout (flex/grid) editors and responsive breakpoints
- Event-stub generation in the exporter
- Assets/icons manager and a plugin system for custom design-time behaviors
