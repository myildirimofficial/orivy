namespace Orivy;

/// <summary>
/// Controls how tabs are distributed along the tab strip inside the available width.
/// Applies to embedded tab mode only.
/// </summary>
public enum WindowPageTabAlignment
{
    /// <summary>Tabs are packed to the leading (left) edge.</summary>
    Start,

    /// <summary>Tabs are centered horizontally in the strip.</summary>
    Center,

    /// <summary>Tabs are packed to the trailing (right) edge.</summary>
    End
}
