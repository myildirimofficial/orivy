namespace Orivy;

/// <summary>
/// Specifies how properties are ordered in a <see cref="Orivy.Controls.PropertyGrid"/>.
/// Mirrors System.Windows.Forms.PropertySort.
/// </summary>
public enum PropertySort
{
    /// <summary>Properties appear in the order returned by reflection.</summary>
    NoSort,

    /// <summary>Properties are sorted alphabetically by display name (flat list, no categories).</summary>
    Alphabetical,

    /// <summary>Properties are grouped by category, sorted alphabetically within each category.</summary>
    Categorized
}
