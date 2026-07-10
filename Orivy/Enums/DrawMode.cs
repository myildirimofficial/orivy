namespace Orivy;

/// <summary>
/// Specifies how the elements of a control are drawn. Mirrors System.Windows.Forms.DrawMode.
/// </summary>
public enum DrawMode
{
    /// <summary>The control draws its own items using the built-in appearance.</summary>
    Normal,

    /// <summary>Items are drawn by the owner via the DrawItem event and are the same size.</summary>
    OwnerDrawFixed,

    /// <summary>Items are drawn by the owner via DrawItem and can differ in size (MeasureItem).</summary>
    OwnerDrawVariable
}

/// <summary>
/// Specifies the visual state of an item being drawn in an owner-drawn control. Flags.
/// </summary>
[System.Flags]
public enum DrawItemState
{
    None = 0,
    Selected = 0x0001,
    Disabled = 0x0004,
    Focus = 0x0010,
    HotLight = 0x0040,
    Default = 0x0020,
}
