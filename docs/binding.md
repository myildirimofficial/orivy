
Binding system — concepts and examples
=====================================

Orivy provides a concise binding API that supports both explicit source bindings and DataContext-based bindings. The API is intentionally small but covers common scenarios: property binding, two-way binding, and collection binding.

Core concepts
- `Link()` extension: start binding from a target `ElementBase` property (see `Orivy/Binding/BindingExtensions.cs`).
- `From(source, expression)`: bind to a concrete source object.
- `FromData(expression)`: bind to a property on the control's `DataContext`.
- Modes: `OneWay` and `TwoWay`.

Basic example (one-way)

```csharp
public class ViewModel : INotifyPropertyChanged
{
	private string _name;
	public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
	public event PropertyChangedEventHandler? PropertyChanged;
}

var vm = new ViewModel { Name = "Alice" };
var btn = new Button();
// Bind button text to vm.Name (one-way)
btn.Link(b => b.Text).From(vm, m => m.Name).OneWay();

// Using DataContext
var label = new Element();
label.SetDataContext(vm);
label.Link(l => l.Text).FromData<ViewModel, string>(m => m.Name).OneWay();
```

Two-way binding (example with a text box)

```csharp
// Assuming TextBox implements a Text property and change event hooks
var input = new TextBox();
input.Link(t => t.Text).From(vm, m => m.Name).TwoWay();
```

Collection binding & GridList
- `BindingTargetBuilder.FromData(..., itemFactory)` supports mapping source collection items into `GridListItem` instances required by `GridList` controls. See `BindingExtensions.CreateDataContextPropertyBinding` for implementation details.

Internals (for advanced users)
- `BindingExtensions.CreatePropertyBinding` constructs a `PropertyBinding` which wires `INotifyPropertyChanged` events and target change events (when two-way mode is used).
- Event proxies are created dynamically to adapt different event handler types into `EventHandler` callbacks used by the binding system.

Best practices
- Prefer `FromData` with a DataContext on windows/pages to keep view code small and testable.
- For collections, prefer mapping to `GridListItem` using a dedicated `itemFactory` so the UI layer controls the presentation of rows.

Where to look
- Binding implementation: [Orivy/Binding/BindingExtensions.cs](Orivy/Binding/BindingExtensions.cs)
- Binding helpers: [Orivy/Binding/BindingTargetBuilder.cs](Orivy/Binding/BindingTargetBuilder.cs)

