using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace Orivy.Studio;

/// <summary>Literal text for Designer C# so merged/generated files compile without extra usings or float/double ambiguity.</summary>
internal static class DesignSourceFormat
{
    public static string Dock(DockStyle dock) => $"Orivy.DockStyle.{dock}";

    public static string Anchor(AnchorStyles anchor)
    {
        if (anchor == AnchorStyles.None)
            return "Orivy.AnchorStyles.None";

        var flags = Enum.GetValues<AnchorStyles>()
            .Where(flag => flag != AnchorStyles.None && anchor.HasFlag(flag))
            .Select(flag => $"Orivy.AnchorStyles.{flag}");
        return string.Join(" | ", flags);
    }

    public static string NormalizeLayoutEnumExpression(string text) =>
        text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("Orivy.", string.Empty, StringComparison.Ordinal);

    public static bool SameAnchorExpression(string existing, AnchorStyles anchor) =>
        string.Equals(
            NormalizeLayoutEnumExpression(existing),
            NormalizeLayoutEnumExpression(Anchor(anchor)),
            StringComparison.Ordinal);

    /// <summary>Rounded integer — SKPoint/SKSize literals must not be bare decimals (inferred as double).</summary>
    public static string Pixel(float value) =>
        ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture);

    /// <summary>Canvas position to write — must match what the user placed (not post-layout stack).</summary>
    public static SKPoint PersistedLocation(ElementBase control) => control.VisualLocation;

    /// <summary>Right-hand side expression for a property assignment (no trailing semicolon).</summary>
    public static string FormatPropertyValue(object? value, Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (value == null)
            return "null";

        if (type == typeof(string))
            return "\"" + EscapeString((string)value) + "\"";

        if (type == typeof(bool))
            return (bool)value ? "true" : "false";

        if (type == typeof(SKColor))
            return FormatSKColor((SKColor)value);

        if (type == typeof(SKSize))
        {
            var size = (SKSize)value;
            return $"new SKSize({Pixel(size.Width)}, {Pixel(size.Height)})";
        }

        if (type == typeof(SKPoint))
        {
            var point = (SKPoint)value;
            return $"new SKPoint({Pixel(point.X)}, {Pixel(point.Y)})";
        }

        if (type == typeof(Thickness))
            return FormatThickness((Thickness)value);

        if (type == typeof(Radius))
            return FormatRadius((Radius)value);

        if (type.IsEnum)
            return FormatEnum(value);

        if (type == typeof(int))
            return ((int)value).ToString(CultureInfo.InvariantCulture);

        if (type == typeof(byte))
            return ((byte)value).ToString(CultureInfo.InvariantCulture);

        if (type == typeof(long))
            return ((long)value).ToString(CultureInfo.InvariantCulture) + "L";

        if (type == typeof(float))
            return ((float)value).ToString(CultureInfo.InvariantCulture) + "f";

        if (type == typeof(double))
            return ((double)value).ToString(CultureInfo.InvariantCulture);

        return value.ToString() ?? "null";
    }

    /// <summary>Best-effort C# expression for a property value; validates with <see cref="CodeImporter.TryEvaluatePropertyExpression"/>.</summary>
    public static bool TryFormatExpression(object? value, Type propertyType, string? designerFilePath, out string expression)
    {
        expression = string.Empty;
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (value is SKFont font)
        {
            expression = FormatFont(font);
            return true;
        }

        if (value == null)
        {
            if (!type.IsValueType)
            {
                expression = "null";
                return RoundTrip(expression, type, null, designerFilePath);
            }

            return false;
        }

        if (TryFormatScalar(value, type, out expression) && RoundTrip(expression, type, value, designerFilePath))
            return true;

        if (TryFormatStaticMember(value, type, out expression) && RoundTrip(expression, type, value, designerFilePath))
            return true;

        if (TryFormatConstructorCall(value, type, designerFilePath, out expression))
            return true;

        if (TryFormatObjectInitializer(value, type, designerFilePath, out expression))
            return true;

        expression = string.Empty;
        return false;
    }

    private static bool TryFormatScalar(object value, Type type, out string expression)
    {
        try
        {
            expression = FormatPropertyValue(value, type);
            return true;
        }
        catch
        {
            expression = string.Empty;
            return false;
        }
    }

    private static bool TryFormatStaticMember(object value, Type type, out string expression)
    {
        expression = string.Empty;
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!type.IsAssignableFrom(field.FieldType))
                continue;
            if (field.GetValue(null) is { } candidate && candidate.Equals(value))
            {
                expression = type.Name == "SKColor"
                    ? $"SKColors.{field.Name}"
                    : $"{FormatTypeName(type)}.{field.Name}";
                return true;
            }
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            if (prop.GetValue(null) is { } candidate && candidate.Equals(value))
            {
                expression = $"{FormatTypeName(type)}.{prop.Name}";
                return true;
            }
        }

        return false;
    }

    private static bool TryFormatConstructorCall(object value, Type type, string? designerFilePath, out string expression)
    {
        expression = string.Empty;
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(c => c.GetParameters().Length))
        {
            var parameters = ctor.GetParameters();
            var args = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                if (!TryGetMemberValue(value, parameters[i].Name, out var argValue))
                {
                    if (!parameters[i].HasDefaultValue)
                        goto nextCtor;

                    argValue = parameters[i].DefaultValue;
                }

                if (!TryFormatExpression(argValue, parameters[i].ParameterType, designerFilePath, out args[i]))
                    goto nextCtor;
            }

            expression = $"new {FormatTypeName(type)}({string.Join(", ", args)})";
            if (RoundTrip(expression, type, value, designerFilePath))
                return true;

            nextCtor: ;
        }

        return false;
    }

    private static bool TryFormatObjectInitializer(object value, Type type, string? designerFilePath, out string expression)
    {
        expression = string.Empty;
        if (type.GetConstructor(Type.EmptyTypes) == null)
            return false;

        var parts = new List<string>();
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(value))
        {
            if (!DesignPersistableProperties.ShouldShowInInspector(pd) || pd.IsReadOnly)
                continue;
            if (DesignPersistableProperties.IsCollectionProperty(pd))
                continue;

            object? propValue;
            try { propValue = pd.GetValue(value); }
            catch { continue; }

            if (!DesignPersistableProperties.ShouldWriteValue(value, pd, propValue))
                continue;

            if (!TryFormatExpression(propValue, pd.PropertyType, designerFilePath, out var rhs))
                continue;

            parts.Add($"{pd.Name} = {rhs}");
        }

        if (parts.Count == 0)
            return false;

        expression = $"new {FormatTypeName(type)} {{ {string.Join(", ", parts)} }}";
        return RoundTrip(expression, type, value, designerFilePath);
    }

    private static bool TryGetMemberValue(object target, string? parameterName, out object? value)
    {
        value = null;
        if (string.IsNullOrEmpty(parameterName))
            return false;

        var prop = target.GetType().GetProperty(parameterName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop?.CanRead == true)
        {
            value = prop.GetValue(target);
            return true;
        }

        var field = target.GetType().GetField(parameterName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null)
        {
            value = field.GetValue(target);
            return true;
        }

        return false;
    }

    private static string FormatFont(SKFont font)
    {
        var family = font.Typeface?.FamilyName;
        if (string.IsNullOrWhiteSpace(family))
            family = "Segoe UI";
        family = family.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        var weight = font.Typeface?.FontWeight ?? (int)SKFontStyleWeight.Normal;
        var slant = font.Typeface?.FontSlant ?? SKFontStyleSlant.Upright;
        var weightExpr = weight >= (int)SKFontStyleWeight.SemiBold
            ? "SKFontStyleWeight.Bold"
            : "SKFontStyleWeight.Normal";
        var slantExpr = slant is SKFontStyleSlant.Italic or SKFontStyleSlant.Oblique
            ? "SKFontStyleSlant.Italic"
            : "SKFontStyleSlant.Upright";
        var size = font.Size.ToString("0.##", CultureInfo.InvariantCulture);
        return $"new SKFont(SKTypeface.FromFamilyName(\"{family}\", {weightExpr}, SKFontStyleWidth.Normal, {slantExpr}) ?? SKTypeface.Default, {size}f)";
    }

    private static string FormatTypeName(Type type) =>
        type.Namespace is "Orivy" or "Orivy.Controls" or "Orivy.Enums"
            ? type.Name
            : type.Name;

    private static bool RoundTrip(string expression, Type type, object? expected, string? designerFilePath)
    {
        try
        {
            var parsed = SyntaxFactory.ParseExpression(expression);
            if (!CodeImporter.TryEvaluatePropertyExpression(parsed, type, out var value, designerFilePath))
                return false;

            return DesignPersistableProperties.ValuesEqual(expected, value, type);
        }
        catch
        {
            return false;
        }
    }

    private static string FormatEnum(object value) =>
        value.GetType().Namespace switch
        {
            "Orivy" or "Orivy.Controls" or "Orivy.Enums" or "Orivy.Objects" =>
                $"Orivy.{value.GetType().Name}.{value}",
            _ => $"{value.GetType().Name}.{value}"
        };

    private static string FormatSKColor(SKColor color)
    {
        foreach (var field in typeof(SKColors).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(SKColor) && field.GetValue(null) is SKColor named && named == color)
                return $"SKColors.{field.Name}";
        }

        if (color.Alpha == byte.MaxValue)
            return $"new SKColor({color.Red}, {color.Green}, {color.Blue})";

        return $"new SKColor({color.Red}, {color.Green}, {color.Blue}, {color.Alpha})";
    }

    private static string FormatThickness(Thickness thickness)
    {
        if (thickness.Left == thickness.Top
            && thickness.Top == thickness.Right
            && thickness.Right == thickness.Bottom)
            return $"new Thickness({Pixel(thickness.Left)})";

        return $"new Thickness({Pixel(thickness.Left)}, {Pixel(thickness.Top)}, {Pixel(thickness.Right)}, {Pixel(thickness.Bottom)})";
    }

    private static string FormatRadius(Radius radius)
    {
        if (radius.TopLeft == radius.TopRight
            && radius.TopRight == radius.BottomLeft
            && radius.BottomLeft == radius.BottomRight)
            return $"new Radius({Pixel(radius.TopLeft)})";

        return $"new Radius({Pixel(radius.TopLeft)}, {Pixel(radius.TopRight)}, {Pixel(radius.BottomLeft)}, {Pixel(radius.BottomRight)})";
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", string.Empty);
}
