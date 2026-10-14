namespace Orivy;

/// <summary>Visual treatment for <see cref="Controls.Carousel"/> previous/next chrome.</summary>
public enum CarouselNavigationStyle
{
    /// <summary>Frosted light circle with soft elevation and charcoal chevron.</summary>
    Glass,

    /// <summary>Chevron only, with a dual-tone halo for contrast on busy photos.</summary>
    Minimal,

    /// <summary>Tall light capsule parked on the edge.</summary>
    Pill,

    /// <summary>Opaque dark circle with a light chevron.</summary>
    Solid,

    /// <summary>Hairline ring and haloed chevron; fills lightly on hover.</summary>
    Outline,

    /// <summary>Rounded square tile with the same chevron glyph.</summary>
    Arrow,
}
