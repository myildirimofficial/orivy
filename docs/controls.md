
Controls — deep dive and custom control guide
=============================================

This document explains `ElementBase` lifecycle, common control patterns, and how to implement custom controls that follow Orivy's performance and rendering conventions.

ElementBase responsibilities
- Property storage and default values (padding, border, radius, background/foreground colors).
- Visual style system: `VisualStyles` and `ConfigureVisualStyles` allow controls to declare base, hover, pressed, and disabled visual states.
- Layout hooks: `GetPreferredSize`, `OnSizeChanged`, and `IArrangedElement` support.
- Rendering hook: `OnPaint(SKCanvas canvas)` — draw using Skia.
- Input handlers: `OnMouseDown`, `OnMouseUp`, `OnKeyDown`, etc.

Common built-in controls
- `Button` — a fully styled interactive button; see [Orivy/Controls/Button.cs](Orivy/Controls/Button.cs).
- `ComboBox`, `MenuStrip`, `GridList` — higher-level controls for selection and lists, often including popups and context menus.
- `Window` / `WindowBase` — top-level window management and titlebar logic.

Deriving a custom control (example: ToggleSwitch)

```csharp
public class ToggleSwitch : ElementBase
{
	private bool _checked;

	public bool Checked
	{
		get => _checked;
		set { if (_checked == value) return; _checked = value; Invalidate(); }
	}

	public ToggleSwitch()
	{
		Size = new SKSize(48, 28);
		Cursor = Cursors.Hand;
		ConfigureVisualStyles(s => s
			.Base(b => b.Background(ColorScheme.SurfaceVariant).Radius(14))
			.OnChecked(c => c.Background(ColorScheme.Primary)));
	}

	public override SKSize GetPreferredSize(SKSize proposedSize) => Size;

	public override void OnPaint(SKCanvas canvas)
	{
		base.OnPaint(canvas);
		var radius = Math.Min(Height / 2f, 14f);
		using var paint = new SKPaint { Color = Checked ? ColorScheme.Primary : ColorScheme.SurfaceVariant };
		canvas.DrawRoundRect(DisplayRectangle, radius, radius, paint);
		// draw thumb
		var thumbX = Checked ? Width - radius*2 - 4 : 4;
		var thumbRect = new SKRect(thumbX, 4, thumbX + (radius*2), Height - 4);
		using var thumbPaint = new SKPaint { Color = SKColors.White }; 
		canvas.DrawOval(thumbRect.MidX, thumbRect.MidY, radius-2, radius-2, thumbPaint);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (!Enabled) return;
		Checked = !Checked;
		OnClick(EventArgs.Empty);
	}
}
```

Styling and visual states
- Use `ConfigureVisualStyles` to declare transitions and visual rules for default, hover, pressed and focused states. The `Button` class contains an extensive example of the recommended style pattern.

Performance tips
- Reuse `SKPaint`, `SKPath`, and `SKFont` instances — allocate them once and cache in fields.
- Avoid LINQ in paint/measure hot paths.
- Measure text once per layout pass and cache results if the text does not change.

Where to look in the code
- `ElementBase` implementation and events: [Orivy/Controls/ElementBase.cs](Orivy/Controls/ElementBase.cs)
- Example control with full visual style: [Orivy/Controls/Button.cs](Orivy/Controls/Button.cs)

