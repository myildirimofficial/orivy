using System;

namespace Orivy;

public readonly struct BackgroundImagePosition : IEquatable<BackgroundImagePosition>
{
    public BackgroundImagePosition(float x, float y)
    {
        X = Math.Clamp(x, 0f, 1f);
        Y = Math.Clamp(y, 0f, 1f);
    }

    public float X { get; }

    public float Y { get; }

    public static BackgroundImagePosition TopLeft => new(0f, 0f);

    public static BackgroundImagePosition TopCenter => new(0.5f, 0f);

    public static BackgroundImagePosition TopRight => new(1f, 0f);

    public static BackgroundImagePosition CenterLeft => new(0f, 0.5f);

    public static BackgroundImagePosition Center => new(0.5f, 0.5f);

    public static BackgroundImagePosition CenterRight => new(1f, 0.5f);

    public static BackgroundImagePosition BottomLeft => new(0f, 1f);

    public static BackgroundImagePosition BottomCenter => new(0.5f, 1f);

    public static BackgroundImagePosition BottomRight => new(1f, 1f);

    public static BackgroundImagePosition FromPercent(float xPercent, float yPercent)
    {
        return new BackgroundImagePosition(xPercent / 100f, yPercent / 100f);
    }

    public bool Equals(BackgroundImagePosition other)
    {
        return Math.Abs(X - other.X) < 0.0001f
            && Math.Abs(Y - other.Y) < 0.0001f;
    }

    public override bool Equals(object? obj)
    {
        return obj is BackgroundImagePosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(BackgroundImagePosition left, BackgroundImagePosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BackgroundImagePosition left, BackgroundImagePosition right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{X * 100f:0.##}% {Y * 100f:0.##}%";
    }
}
