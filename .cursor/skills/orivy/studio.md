# Orivy.Studio

The canvas is live Orivy controls. Persistence is Designer C# (`InitializeComponent`), not a project file.

## Canvas

`DesignSurface` hosts `DesignRoot` plus a transparent `DesignOverlay`.

- In design mode the overlay receives pointer and keyboard input. Designed controls paint but do not take general input. `PreviewMode` hides the overlay and the controls become live.
- Zoom is `protected override float ChildRenderScale => _zoom`. That scale applies to both paint and hit-testing. `GetInputBounds` is the element's own rectangle plus anchor-overflow children only. Do not union every descendant: the overlay is `Width/zoom` locally and that union covers the side panels, so a click lands on the canvas while the wheel (own rectangle) still hits the panel.
- The overlay must not be `Dock = Fill`. `SyncOverlayBounds` sets its logical size to the viewport divided by zoom so the scaled overlay still covers the surface. Do not “simplify” that.
- Drop path: `ToolboxList` → `DragLayer.Begin` → `DesignSurface.DropAt` → `AddControl`. Every placed or imported control goes through `DesignSurface.PrepareForDesign` (`AutoSize = false` and related flags) so the designer owns bounds.
- The design root sets `ClipChildrenToShape` false so a control outside the form still paints. `DrawOutsideFormBorders` then strokes those bounds (including nested controls) from `VisualLocation`, after the selection stroke.

## Undo

`CommandStack` (`Orivy.Studio/History/CommandStack.cs`):

| Method | Use when |
| --- | --- |
| `Execute(command)` | The change is not applied yet. `Do()` runs, then the command is stacked. Add, delete, dock, align, z-order. |
| `Push(command)` | The change is already applied (drag end, property-grid commit). Stack only; do not run `Do()` again. |

`DelegateCommand(label, do, undo)` is the usual implementation. Capacity is 200. `Clear()` on import. Do not mutate the design tree outside the stack. Exception already in the product: the layers panel visibility/lock toggles are not undoable. Do not add more exceptions.

## Toolbox

`ControlCatalog.Discover()` reflects public, non-abstract `ElementBase` types with a parameterless constructor. It skips `WindowBase`, open generics, and `Excluded` (`ScrollBar`, `ContextMenuStrip`, `NotificationTray`, `MessageBox`).

A new control appears by existing in the Orivy assembly. Do not add a static toolbox array. Optional polish only: `Categories`, `Descriptions`, `DefaultSizes`, and `ApplyDesignDefaults`.

`CreateInstance(seedPlaceholderContent: true)` is for a toolbox drop. The importer calls `CreateInstance(seedPlaceholderContent: false)` so placeholder rows are not left beside real imported items. Keep that split.

## Save / open

- Saving an existing file goes through `CodeMerger.Apply`. It rewrites `Location`, `Size`, `Dock`, `Anchor`, `Text`, `Visible`, and the form size only when the live value differs from the source, appends only names in `AddedControlNames`, and removes only `DeletedControlNames`. If nothing changed it returns the original text unchanged. Methods, usings, and untracked properties stay in the file. A new or edited `+=` or `Link` line is placed immediately after that control's initializer or last property assignment; an unchanged one is left in place. `ZOrder` stays internal; neither the generator nor the merger writes it.
- `CodeGenerator.Generate` is the stub used when there is no original source (a new document, or a file with no `InitializeComponent`). Do not point Save or the Code tab at `Generate` when `OriginalSourceText` is set.
- Hit-testing and drag use `VisualLocation` / design-space bounds and are not clipped to the parent. After import, restore the file's Location/Size so dock/anchor layout cannot persist overflow coordinates. A designer Location/Size write updates anchor info and clears any render-location override. In design mode a right/bottom anchor that crosses the parent edge still records the new margin; keeping the old negative margin snapped the control back on the next layout.
- A control with `IsAncestorSiteInDesignMode` does not run `DefaultLayout` when the design page is shown again. That show happens before the code tab is filled, so a spontaneous layout both snaps the canvas back and makes the generated code match the old file. Dock/Anchor edits go through `ApplyStoredDesignLayout`.
- `CodeMerger` replaces arguments on the existing `new Type(...)` / `new()` node. Do not build a fresh `ObjectCreationExpression`; that drops the space after `new` and emits `newSKSize`.
- `CodeImporter` reads object initializers, `this.control.Property =` statements, `Controls.Add` / `AddRange`, and `var name = new KnownType { ... }` locals (including inside blocks). Helper calls such as `CreateCard(...)` are not executed.
- Opening a `.cs` file requires a method named `InitializeComponent`. Anything else is a text document. Do not invent another on-disk format.

## Shell

`StudioWindow` builds its UI in code (toolbar, `SplitContainer`, document `TabView`). Panels rebind in `SwitchActive` when the active document changes. New panel state must rebind there too.

The right inspector tabs are Properties, Bindings (`BindingHelper`), and Events (`EventHelper`), in that order. Events edit `name.Event += handler` lines. A new or edited event or binding line is inserted immediately after that control's initializer or last property assignment. Lines that did not change stay where they are. The handler name is typed in the Handler cell (click, F2, or Enter); double-clicking an empty row fills `control_Event`. A plain handler name also inserts a private method stub into the same class when that method is missing from this file and from the sibling non-designer partial. Clearing the cell text removes the wire and does not delete the method. Bindings edit `Link(...).From(...)` / `FromData` lines. The member list is the source control's browsable properties; DataContext still takes a typed member name. Both round-trip through `DesignInteractions` on import and save. Do not invent a second binding engine for the designer.

The property inspector is applied only after the mouse button is up (`TryFlushInspectorRefresh`). Do not set `PropertyGrid.SelectedObject` from `OnMouseDown` or from a posted message that still runs before mouse-up. Rebuilding while the grid or its editor holds capture leaves the right-hand panel unable to take the next click. Drop that capture before the rebuild.

`DragLayer`, `DesignOverlay`, `ToolboxList`, and `GridList` implement `IMouseCaptureLost`. Keep that when changing their drag loops.

## Example apps

```csharp
[STAThread]
static void Main() => Application.Run(new MainWindow());
```

`Orivy.Example` and `Orivy.SettingsPreview` use `Window` plus `InitializeComponent()` in a `.Designer.cs` partial (`SuspendLayout`, build the tree, `ResumeLayout`). Theme-reactive colors go through `ConfigureVisualStyles` / `ColorScheme`, not a one-time `BackColor` snapshot. `Orivy.Studio` is code-built and does not follow the Designer partial pattern.
