Example: Simple Window
================================

This example demonstrates creating a top-level `Window`, adding a `Button` and running the application loop. Copy the example into a small console application or into `Orivy.Example` as a learning exercise.

```csharp
using Orivy;
using Orivy.Controls;
using SkiaSharp;

class Program
{
    static void Main()
    {
        var win = new Window { Size = new SKSize(800, 600), Text = "Orivy Demo" };

        var btn = new Button { Text = "Click me", Location = new SKPoint(20, 20) };
        btn.Click += (_, _) => Console.WriteLine("Button clicked");

        var toggle = new Orivy.Controls.Element();
        // add more controls and layout them using Location/Size or a custom container

        win.Controls.Add(btn);

        Application.Run(win);
    }
}
```

Notes
- Use `Controls.Add(...)` to add children to a window or container. Controls are positioned using `Location` and sized by `Size`, or left to a layout manager.
- To create richer layouts, combine `ElementBase` containers with `Dock` and `Anchor` settings or implement a custom layout container.
