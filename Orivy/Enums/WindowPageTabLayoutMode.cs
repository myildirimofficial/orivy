namespace Orivy;

/// <summary>
/// Determines which edge hosts the embedded tab strip.
/// Window chrome tabs always remain at the top edge.
/// </summary>
public enum WindowPageTabLayoutMode
{
    Top,
    Left,
    Right,
    Bottom
}