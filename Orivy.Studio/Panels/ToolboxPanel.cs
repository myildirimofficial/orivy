using Orivy;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;

namespace Orivy.Studio.Panels;

/// <summary>
/// Toolbox: a search box over a polished, owner-drawn <see cref="ToolboxList"/> of control types
/// discovered dynamically from Orivy.Controls. Entries can be double-clicked or dragged to the canvas.
/// </summary>
public sealed class ToolboxPanel : Element
{
    private readonly ToolboxList _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search;

    public ToolboxPanel()
    {
        BackColor = SKColors.Transparent;
        Border = new Thickness(0);
        Radius = new Radius(0);
        Padding = new Thickness(0);

        _search = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 36,
            Margin = new Thickness(0, 0, 0, 8),
            PlaceholderText = "Search controls…",
        };
        _search.TextChanged += (_, _) => _list.Filter(_search.Text);

        Controls.Add(_list);
        Controls.Add(_search);

        _list.SetEntries(ControlCatalog.Discover());
    }

    /// <summary>Raised when the user double-clicks an entry (place at default cascade position).</summary>
    public event Action<ControlEntry>? PlaceRequested
    {
        add => _list.PlaceRequested += value;
        remove => _list.PlaceRequested -= value;
    }

    /// <summary>Raised once an entry is dragged past the threshold (entry, screen point).</summary>
    public event Action<ControlEntry, SKPoint>? DragStarted
    {
        add => _list.DragStarted += value;
        remove => _list.DragStarted -= value;
    }
}
