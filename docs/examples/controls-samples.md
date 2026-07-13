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
cp.SelectedColorChanged += (_, _) => { var selected = cp.SelectedColor; /* handle color change */ };
window.Controls.Add(cp);
```

3) GridList — columns, rows, colors, hit testing

```csharp
var grid = new GridList { Dock = DockStyle.Fill, MultiSelect = true };
grid.Columns.Add(new GridListColumn { Name = "name", Text = "Name", Width = 180f });
grid.Columns.Add(new GridListColumn { Name = "state", Text = "State", SizeMode = GridListColumnSizeMode.Fill });

// WinForms-style ergonomics: one cell per string, keyed items, item colors and tooltips.
var row = grid.Items.Add(new[] { "Renderer", "Healthy" });
row.Name = "renderer";
row.ToolTipText = "Skia render pipeline";
row.ForeColor = new SKColor(34, 197, 94);

grid.Items.Add("Layout Engine");            // single text cell
if (grid.Items.ContainsKey("renderer"))
    grid.Items["renderer"]!.Selected = true;

grid.CellClick += (_, e) => Console.WriteLine($"{e.Item.Text} / {e.Column.Text}");
grid.MouseDown += (_, e) =>
{
    var hit = grid.HitTest(e.Location);      // row / cell / header / check box / resize region
    if (hit.Region == GridListHitTestRegion.Cell)
        contextMenu.Show(grid.PointToScreen(e.Location));
};
window.Controls.Add(grid);
```

3b) ListBox and checked mode

```csharp
var list = new ListBox { Size = new SKSize(220, 260), SelectionMode = SelectionMode.MultiExtended };
list.Items.AddRange("Alpha", "Beta", "Gamma");
list.SelectedIndexChanged += (_, _) => Console.WriteLine(list.SelectedItem);

var options = new ListBox { CheckBoxes = true, CheckOnClick = true };
options.Items.AddRange("Auto save", "Telemetry", "Beta channel");
options.SetItemChecked(0, true);
options.ItemCheck += (_, e) => Console.WriteLine($"{options.Items[e.Index]} -> {e.NewValue}");
```

3c) PropertyGrid — edit any object

```csharp
var propertyGrid = new PropertyGrid
{
    Dock = DockStyle.Fill,
    PropertySort = PropertySort.Categorized,
    SelectedObject = appSettings          // [Category]/[Description]/[ReadOnly] honored
};
propertyGrid.PropertyValueChanged += (_, e) =>
    Console.WriteLine($"{e.ChangedItem?.Name} changed (was {e.OldValue})");
propertyGrid.ExpandAllGridItems();        // expand nested objects / collections
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

6) Absolute child badge inside a button

```csharp
var inbox = new Button { Text = "Inbox", Size = new SKSize(116, 36) };
inbox.Controls.Add(new Badge
{
    Text = "12",
    Variant = BadgeVariant.Danger,
    Anchor = AnchorStyles.Top | AnchorStyles.Right,
    Location = new SKPoint(-4, -4),
    CanSelect = false,
    TabStop = false
});
window.Controls.Add(inbox);
```

Notes
- Many controls expose events and properties similar to WinForms; inspect `Orivy/Controls/*` for full APIs and examples.
