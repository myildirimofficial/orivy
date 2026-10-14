# Orivy.Studio

The canvas is live Orivy controls. Persistence is Designer C# (`InitializeComponent`), not a project file.

## Canvas

`DesignSurface` hosts `DesignRoot` plus a transparent `DesignOverlay`.

- In design mode the overlay receives pointer and keyboard input. Designed controls paint but do not take general input. `PreviewMode` hides the overlay and the controls become live.
- Clicking empty space on the form selects `DesignRoot` (shown as "Form" when unnamed) so the inspector can edit host properties. Root resize still uses the permanent right/bottom grips; the root is never moved, deleted, or duplicated. Layers lists the root as the hierarchy top.
- Zoom is `protected override float ChildRenderScale => _zoom`. That scale applies to both paint and hit-testing. `GetInputBounds` is the element's own rectangle plus anchor-overflow children only. Do not union every descendant: the overlay is `Width/zoom` locally and that union covers the side panels, so a click lands on the canvas while the wheel (own rectangle) still hits the panel.
- The overlay must not be `Dock = Fill`. `SyncOverlayBounds` sets its logical size to the viewport divided by zoom so the scaled overlay still covers the surface. Do not “simplify” that.
- Drop path: `ToolboxList` → `DragLayer.Begin` → `DesignSurface.DropAt` → `AddControl`. Every placed or imported control goes through `DesignSurface.PrepareForDesign` (`AutoSize = false` and related flags) so the designer owns bounds. That `AutoSize = false` is persisted (new drop, duplicate, and import) so runtime does not grow the control again. When the user turns `AutoSize` back on (or the file has `AutoSize = true`), import restores file `Size` then `RelayoutRoot` / `ApplyDesignAutoSize` re-runs `AdjustSize` for undocked AutoSize controls — SelfAutoSize buttons skip `DefaultLayout`'s AutoSize pass, so without that step a stale Size looks clipped or shifted after reopen. Enabling AutoSize in the inspector also records `Size` for Save.
- When the designed type is a `Window` / `WindowBase`, `DesignRoot` is an `Element` (not an HWND) but it paints title chrome and uses `Padding.Top = TitleHeight` so Dock/Anchor share the same `DisplayRectangle` as runtime. Pages / `Element` hosts stay chrome-less and opaque `ColorScheme.Surface`.
- The design root sets `ClipChildrenToShape` false so a control outside the form still paints. `DrawOutsideFormBorders` then strokes those bounds (including nested controls) from `VisualLocation`, after the selection stroke. Hidden controls (`Visible = false` or under a hidden ancestor) do not get the overflow stroke.

## Undo

`CommandStack` (`Orivy.Studio/History/CommandStack.cs`):

| Method | Use when |
| --- | --- |
| `Execute(command)` | The change is not applied yet. `Do()` runs, then the command is stacked. Add, delete, dock, align, z-order. |
| `Push(command)` | The change is already applied (drag end, property-grid commit). Stack only; do not run `Do()` again. |

`DelegateCommand(label, do, undo)` is the usual implementation. Capacity is 200. `Clear()` on import. Do not mutate the design tree outside the stack. Exception already in the product: the layers panel visibility/lock toggles are not undoable. Do not add more exceptions.

## Toolbox

`ControlCatalog.Discover()` reflects public, non-abstract `ElementBase` types with a parameterless constructor. It skips `WindowBase`, open generics, and `Excluded` (`ScrollBar`, `ContextMenuStrip`, `NotificationTray`, `MessageBox`). Open generics such as `ButtonGroup<T>` are not placeable; the closed `ButtonGroup` (`ButtonGroup<string>`) is what the toolbox instantiates.

A new control appears by existing in the Orivy assembly. Do not add a static toolbox array. Optional polish only: `Categories`, `Descriptions`, `DefaultSizes`, and `ApplyDesignDefaults`.

`CreateInstance(seedPlaceholderContent: true)` is for a toolbox drop. The importer calls `CreateInstance(seedPlaceholderContent: false)` so placeholder rows are not left beside real imported items. Keep that split.

Designed `TabView` pages: only the selected page is `Visible`. Design mode disables page transitions. After import and on tab-header clicks, call `TabView.SyncPageVisibility()`. Drops onto a TabView nest into the selected page, not as siblings of every page.

`ButtonGroup` is edited through its `Items` collection in the PropertyGrid (`ItemCollection<T>` implements `IList`). Do not nest toolbox Buttons into a ButtonGroup. Auto-generated item buttons are not in the design tree (layers / hit-test / selection); clicking them selects the group. Import maps `ButtonGroup<T>` to the closed `ButtonGroup` and stringifies enum item names.

Assigning `Dock` in the designer must stretch the layout-owned axis immediately (`Top`/`Bottom` → parent width, `Left`/`Right` → parent height). Design mode still skips spontaneous `DefaultLayout` for anchor drag stability; Dock/Anchor-named layout (including ResumeLayout after `SetDock`) is the exception.

`DeleteSelection` removes the control from its real parent (not always the design root), includes nested descendants in `DeletedControlNames` / events / bindings, and clears related edit tracking so Save does not leave orphan fields.

## Save / open

- Saving an existing file goes through `CodeMerger.Apply` only after the document is dirty (`mergeLiveEdits`). Until then the Code tab and Save return the loaded source unchanged. When merging, layout and PropertyGrid sync apply only to controls editable in this document. Inspector commits are recorded on `DesignSurface.EditedProperties`; Save inserts or updates only those properties. Existing assignments, `Controls.Add` lines, and comments stay unless that property was edited. A nested edit (`Margin.Left`) persists the root property (`Margin`) at the file's existing indent and `this.` style. `Font` is written as `new SKFont(SKTypeface.FromFamilyName(...), size)`. `Thickness` and `Radius` are `new Orivy.Thickness` / `new Orivy.Radius` so files without `using Orivy` still compile. Location, Size, Dock, Anchor, Text, Visible, and form ClientSize patch an existing line only when that property is in `EditedProperties` (canvas move/resize and the layout bar record Location, Size, Dock, and Anchor). Flag enums are written as `Orivy.AnchorStyles.Top | Orivy.AnchorStyles.Left`, not the comma form of `Enum.ToString()`. A grid cell change writes or updates `grid.SetPlacement(child, row, column, rowSpan, columnSpan)` and import reads that call (and `grid.Add(child, row, column, ...)`). Bring-to-front / layer reorder permutes the child names already inside that parent's `Controls.Add` / `AddRange`; it does not insert or delete those lines. An event or binding that already appears anywhere in `InitializeComponent` (including inside `if`) is not inserted a second time. Several handlers on the same event each keep their own line. Renaming a control in the PropertyGrid rewrites that identifier in the file (field, `this.name`, receivers) and leaves member names such as `.Text` alone. Properties already inside `new Type { ... }` are updated in place and are not duplicated as a second statement. Inner trees loaded from another type's `.Designer.cs` are registered on the surface by **element reference** (names are re-normalized on the host, so name-only sets are not enough): they are not selectable (`Selectable = false`), not in the merger `live` map, and not persisted into the host file. Clicking inside a hosted custom control selects the host. Edit the inner layout in that type's own designer file.
- `CodeGenerator.Generate` is the stub used when there is no original source (a new document, or a file with no `InitializeComponent`). Do not point Save or the Code tab at `Generate` when `OriginalSourceText` is set.
- Hit-testing and drag use `VisualLocation` / design-space bounds and are not clipped to the parent. After import, restore the file's Location/Size so dock/anchor layout cannot persist overflow coordinates. A designer Location/Size write updates anchor info and clears any render-location override. In design mode a right/bottom anchor that crosses the parent edge still records the new margin; keeping the old negative margin snapped the control back on the next layout.
- Save/Code merge call <see cref="DesignSurface.PrepareForPersist"/> then write <see cref="ElementBase.VisualLocation"/> — no layout pass on save (layout was stacking both <c>Top|Right</c> buttons on the same point). Import restores file coordinates without relayout. Preview / form resize still use <see cref="DesignSurface.RelayoutRoot"/>. Runtime anchor layout recalculates margins from each control's <c>Location</c> every pass.
- `resources.GetObject("…")` assignments load from the sibling `.resx` next to the designer file when present (`DesignResourceLoader`).
- When `Dock` is not `None`, the canvas and inspector follow layout-owned axes: Top/Bottom only height (center top/bottom grips), Left/Right only width, Fill no resize and read-only `Size`/`Location`. The same Dock rules apply to `MinimumSize` / `MaximumSize` (Fill blocks them; otherwise `ClampSize` keeps the layout-owned axis on commit). Runtime Min/Max ignore layout-owned axes so they cannot inflate a Dock.Top Width. Import does not restore file Width for Top/Bottom (Height for Left/Right). `SetDock` caps the layout-owned seed to the parent client; `DefaultLayout` then stacks remaining regions. `AutoSize = true` hides resize grips and blocks Size/Width/Height in the inspector.
- Preview (▶) runs only for `DesignDocument` tabs (disabled on text / .resx / etc.). Inline on the canvas by default, or in a separate owned `Window` when the toolbar “Preview in separate window” toggle is on. Escape on that window stops preview.
- `CodeMerger` replaces arguments on the existing `new Type(...)` / `new()` node. Do not build a fresh `ObjectCreationExpression`; that drops the space after `new` and emits `newSKSize`. `DesignSourceFormat` emits `Orivy.DockStyle` / `Orivy.AnchorStyles` and integer `SKPoint`/`SKSize` literals so hand-edited files compile without extra usings. Merger treats `AnchorStyles` and `Orivy.AnchorStyles` as equivalent when the flags match.
- `CodeImporter` reads object initializers, `this.control.Property =` statements, `Controls.Add` / `AddRange`, and `var name = new KnownType { ... }` locals (including inside blocks). Helper calls such as `CreateCard(...)` are not executed. Form self-assignments `this.Width` / `this.Height` / `this.Text` / `ShowTitle` / `TitleHeight` / `Padding` apply to the design root. `ColorScheme.Surface` (and other palette members) and `.WithAlpha(...)` evaluate to the current palette. A control without a Size assignment keeps its constructor/catalog size — do not write 0×0.
- Opening a `.cs` file requires a method named `InitializeComponent`. Anything else is a text document. Do not invent another on-disk format.

## Shell

`StudioWindow` builds its UI in code (toolbar, `SplitContainer`, document `TabView`). Panels rebind in `SwitchActive` when the active document changes. New panel state must rebind there too. Project Explorer opens each path in its own permanent tab (`OpenPath(..., asPreview: false)`); an already-open path is focused instead of duplicated. File ▸ Open File / the file toolbar button use `FileSelectionDialog` → `OpenPath`. There is no Export Designer Code dialog; Save / the Code tab are the persistence surface. Clear Canvas is not exposed in the shell.

Shell preferences live in `%LocalAppData%\OrivyStudio\settings.json` via `StudioSettings` (theme Light/Dark/Auto with sun/moon/theme-auto icons, preview-in-separate-window). `RecentProjects` stays in `recent.json` beside it. Theme Auto follows `Helpers.IsDark()` and re-applies on window activate.

The right inspector tabs are Properties, Bindings (`BindingHelper`), and Events (`EventHelper`), in that order. A shared category/A–Z sort icon sits on the inspector tab strip (right of the three tabs) and toggles `PropertyGrid.PropertySort` for all of them. The Properties grid uses `DesignPersistableProperties.ShouldShowInInspector` (same hide list as Save). Categories sort Appearance, Layout, Behavior, Data, Window, Design, Animation, Misc. Search matches display name, category, and description, and expands only the matching ancestor chain. Events edit `name.Event += handler` lines. A new or edited event or binding line is inserted immediately after that control's initializer or last property assignment. Lines that did not change stay where they are. The handler name is typed in the Handler cell (click, F2, or Enter); double-clicking an empty row fills `control_Event`. A plain handler name also inserts a private method stub into the same class when that method is missing from this file and from the sibling non-designer partial. Clearing the cell text removes the wire and does not delete the method. Bindings edit `Link(...).From(...)` / `FromData` lines. The member list is the source control's browsable properties; DataContext still takes a typed member name. Both round-trip through `DesignInteractions` on import and save. Do not invent a second binding engine for the designer.

The property inspector is applied only after the mouse button is up (`TryFlushInspectorRefresh`). Do not set `PropertyGrid.SelectedObject` from `OnMouseDown` or from a posted message that still runs before mouse-up. Rebuilding while the grid or its editor holds capture leaves the right-hand panel unable to take the next click. Drop that capture before the rebuild. Edits such as `MinimumSize` / `MaximumSize` invalidate only the control — they do not rerun dock/anchor layout, so lowering MinimumSize to zero does not collapse the current Size.

`DragLayer`, `DesignOverlay`, `ToolboxList`, and `GridList` implement `IMouseCaptureLost`. Keep that when changing their drag loops.

## Example apps

```csharp
[STAThread]
static void Main() => Application.Run(new MainWindow());
```

`Orivy.Example` and `Orivy.SettingsPreview` use `Window` plus `InitializeComponent()` in a `.Designer.cs` partial (`SuspendLayout`, build the tree, `ResumeLayout`). Theme-reactive colors go through `ConfigureVisualStyles` / `ColorScheme`, not a one-time `BackColor` snapshot. `Orivy.Studio` is code-built and does not follow the Designer partial pattern.
