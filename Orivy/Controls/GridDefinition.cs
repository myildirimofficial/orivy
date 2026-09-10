using Orivy;

namespace Orivy.Controls;

public sealed class RowDefinition
{
    private GridLength _height = GridLength.FromStar();

    internal Grid? Owner { get; set; }

    public GridLength Height
    {
        get => _height;
        set
        {
            if (_height == value)
                return;
            _height = value;
            Owner?.OnDefinitionsChanged();
        }
    }

    public float ActualHeight { get; internal set; }
}

public sealed class ColumnDefinition
{
    private GridLength _width = GridLength.FromStar();

    internal Grid? Owner { get; set; }

    public GridLength Width
    {
        get => _width;
        set
        {
            if (_width == value)
                return;
            _width = value;
            Owner?.OnDefinitionsChanged();
        }
    }

    public float ActualWidth { get; internal set; }
}
