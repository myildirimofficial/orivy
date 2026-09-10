using System;
using System.Globalization;

namespace Orivy;

public enum GridUnitType
{
    Auto,
    Pixel,
    Star,
}

public readonly struct GridLength : IEquatable<GridLength>
{
    public GridLength(float value, GridUnitType unitType = GridUnitType.Pixel)
    {
        if (value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value));

        Value = unitType == GridUnitType.Auto ? 0f : value;
        UnitType = unitType;
    }

    public float Value { get; }
    public GridUnitType UnitType { get; }

    public static GridLength Auto { get; } = new(0f, GridUnitType.Auto);
    public static GridLength FromPixels(float pixels) => new(pixels, GridUnitType.Pixel);
    public static GridLength FromStar(float weight = 1f) => new(weight, GridUnitType.Star);

    public bool IsAuto => UnitType == GridUnitType.Auto;
    public bool IsPixel => UnitType == GridUnitType.Pixel;
    public bool IsStar => UnitType == GridUnitType.Star;

    public bool Equals(GridLength other) => Value.Equals(other.Value) && UnitType == other.UnitType;
    public override bool Equals(object? obj) => obj is GridLength other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Value, UnitType);
    public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);
    public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);

    public override string ToString() => UnitType switch
    {
        GridUnitType.Auto => "Auto",
        GridUnitType.Star => Value == 1f ? "*" : Value.ToString(CultureInfo.InvariantCulture) + "*",
        _ => Value.ToString(CultureInfo.InvariantCulture),
    };
}
