namespace Orivy;

public enum ImageLayout
{
    /// <summary>
    /// Draws the image at its original pixel size and positions it inside the target bounds.
    /// </summary>
    None,

    /// <summary>
    /// Repeats the image from the configured position.
    /// </summary>
    Tile,

    /// <summary>
    /// Draws the image at its original pixel size and centers/positions it inside the target bounds.
    /// </summary>
    Center,

    /// <summary>
    /// Stretches the image to exactly match the target bounds.
    /// </summary>
    Stretch,

    /// <summary>
    /// Scales the whole image so it is fully visible inside the target bounds.
    /// </summary>
    Zoom,

    /// <summary>
    /// Scales the image to fill the target bounds and crops the overflow according to BackgroundImagePosition.
    /// </summary>
    Cover
}
