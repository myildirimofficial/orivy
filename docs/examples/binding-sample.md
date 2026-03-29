Binding example
=========================

This example demonstrates one-way and DataContext-based binding with a small ViewModel.

```csharp
public class Person : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private string _name;
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
}

var vm = new Person { Name = "Mehmet" };

// Explicit source binding
var btn = new Button();
btn.Link(b => b.Text).From(vm, m => m.Name).OneWay();

// DataContext-based binding
var label = new Element();
label.SetDataContext(vm);
label.Link(l => l.Text).FromData<Person, string>(p => p.Name).OneWay();

// Two-way example with a text box (if TextBox supports two-way hooks)
// var textbox = new TextBox();
// textbox.Link(t => t.Text).From(vm, m => m.Name).TwoWay();
```
