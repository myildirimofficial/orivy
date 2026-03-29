Controls — usage samples
=========================

1) Button with custom styles

```csharp
var btn = new Button { Text = "Save", Location = new SKPoint(8,8) };
btn.ConfigureVisualStyles(s => s
	.Base(b => b.Background(ColorScheme.Primary).Foreground(SKColors.White))
	.OnHover(h => h.Background(ColorScheme.Primary.Brightness(0.06f))));
btn.Click += (_,_) => SaveDocument();
window.Controls.Add(btn);
```

2) ColorPicker (simple)

```csharp
var cp = new ColorPicker { Location = new SKPoint(16, 60) };
cp.ValueChanged += (_,_) => { var selected = cp.Value; /* handle color change */ };
window.Controls.Add(cp);
```

3) GridList — populating rows

```csharp
var grid = new GridList { Dock = DockStyle.Fill };
var items = new List<GridListItem>();
items.Add(new GridListItem { Cells = { new GridListCell { Text = "Row 1" } } });
grid.Items = items;
window.Controls.Add(grid);
```

Notes
- Many controls expose events and properties similar to WinForms; inspect `Orivy/Controls/*` for full APIs and examples.
