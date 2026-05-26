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

4) DatePicker and TimePicker

```csharp
var datePicker = new DatePicker
{
    Location = new SKPoint(16, 120),
    Value = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30),
    MinDate = DateTime.Today,
    Format = "MMM d, yyyy",
    DateTimeFormat = "MMM d, yyyy HH:mm",
    ShowTimePicker = true,
    TimeFormat = "HH:mm",
    MinuteStep = 5,
    TextBoxMode = true
};

var timePicker = new TimePicker
{
    Location = new SKPoint(290, 120),
    Value = new TimeSpan(14, 30, 0),
    MinuteStep = 5,
    Use24HourClock = true,
    TextBoxMode = true
};

datePicker.ValueChanged += (_, _) => ScheduleFor(datePicker.Value, timePicker.Value);
timePicker.ValueChanged += (_, _) => ScheduleFor(datePicker.Value, timePicker.Value);

// Popup visibility can be controlled by the host workflow.
datePicker.ShowDropDown();
timePicker.HideDropDown();
```

5) Breadcrumb with custom child controls

```csharp
var breadcrumb = new Breadcrumb { Location = new SKPoint(16, 176), AutoSize = true };
breadcrumb.Controls.Add(new Button { Text = "Settings", AutoSize = true, Shadow = BoxShadow.None });
breadcrumb.Controls.Add(new Badge { Text = "System", Variant = BadgeVariant.Primary, AutoSize = true });
breadcrumb.Controls.Add(new Button { Text = "Display", AutoSize = true, Shadow = BoxShadow.None });
window.Controls.Add(breadcrumb);
```

Notes
- Many controls expose events and properties similar to WinForms; inspect `Orivy/Controls/*` for full APIs and examples.
