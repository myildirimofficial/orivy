using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace Orivy.Studio;

/// <summary>
/// Which properties the inspector may edit and Save may write — aligned with <see cref="PropertyGrid"/>.
/// </summary>
internal static class DesignPersistableProperties
{
    /// <summary>Designer never emits these as simple assignments.</summary>
    private static readonly HashSet<string> NeverPersist = new(StringComparer.Ordinal)
    {
        "Controls", "Parent", "DataContext", "Site", "Handle",
        "Bounds", "ClientRectangle", "DisplayRectangle", "PreferredSize",
        "Properties", "Window", "IsHandleCreated",
    };

    /// <summary>Same rule as <see cref="PropertyGrid"/> noise rows (read-only <c>Is*</c> booleans).</summary>
    public static bool IsNoiseProperty(PropertyDescriptor descriptor) =>
        descriptor.IsReadOnly
        && descriptor.PropertyType == typeof(bool)
        && descriptor.Name.StartsWith("Is", StringComparison.Ordinal);

    public static bool ShouldShowInInspector(PropertyDescriptor descriptor)
    {
        if (!descriptor.IsBrowsable || IsNoiseProperty(descriptor))
            return false;
        if (descriptor.SerializationVisibility == DesignerSerializationVisibility.Hidden)
            return false;
        if (NeverPersist.Contains(descriptor.Name))
            return false;
        return true;
    }

    public static bool IsCollectionProperty(PropertyDescriptor descriptor)
    {
        if (descriptor.Name == "Controls")
            return false;

        var type = descriptor.PropertyType;
        if (type == typeof(string))
            return false;

        return typeof(IEnumerable).IsAssignableFrom(type);
    }

    public static bool ShouldAttemptPersist(PropertyDescriptor descriptor)
    {
        if (!ShouldShowInInspector(descriptor) || descriptor.IsReadOnly)
            return false;

        var type = Nullable.GetUnderlyingType(descriptor.PropertyType) ?? descriptor.PropertyType;
        if (typeof(Delegate).IsAssignableFrom(type))
            return false;
        if (typeof(ElementBase).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool CanPersistType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(string) || type == typeof(bool) || type.IsEnum)
            return true;
        if (type == typeof(SKColor) || type == typeof(SKSize) || type == typeof(SKPoint))
            return true;
        if (type == typeof(Thickness) || type == typeof(Radius))
            return true;
        if (type == typeof(int) || type == typeof(float) || type == typeof(double)
            || type == typeof(byte) || type == typeof(long) || type == typeof(decimal))
            return true;
        if (typeof(Delegate).IsAssignableFrom(type))
            return false;
        if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            return false;
        if (typeof(ElementBase).IsAssignableFrom(type))
            return false;
        // Other structs with ctor (BoxShadow, GridLength, …) — try format round-trip at sync time.
        return type.IsValueType;
    }

    public static object? GetLiveValue(ElementBase control, PropertyDescriptor descriptor)
    {
        if (descriptor.Name == "Location")
            return DesignSourceFormat.PersistedLocation(control);

        return descriptor.GetValue(control);
    }

    internal static object? GetTrackedLayoutLiveValue(ElementBase control, string propertyName) =>
        propertyName switch
        {
            "Location" => DesignSourceFormat.PersistedLocation(control),
            "Size" => new SKSize(control.Width, control.Height),
            _ => TypeDescriptor.GetProperties(control)[propertyName]?.GetValue(control),
        };

    internal static bool ShouldWriteTrackedLayoutProperty(ElementBase control, string propertyName)
    {
        var descriptor = TypeDescriptor.GetProperties(control)[propertyName];
        if (descriptor == null)
            return true;

        var liveValue = GetTrackedLayoutLiveValue(control, propertyName);
        return ShouldWriteValue(control, descriptor, liveValue);
    }

    public static bool ShouldWriteValue(object component, PropertyDescriptor descriptor, object? liveValue)
    {
        var shouldSerialize = component.GetType().GetMethod(
            $"ShouldSerialize{descriptor.Name}",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (shouldSerialize != null && shouldSerialize.ReturnType == typeof(bool))
        {
            try
            {
                if (!(bool)shouldSerialize.Invoke(component, null)!)
                    return false;
            }
            catch
            {
                // fall through to default heuristics
            }
        }

        if (TryGetCoreDefaultValue(component, descriptor, out var defaultValue)
            && ValuesEqual(defaultValue, liveValue, descriptor.PropertyType))
            return false;

        return true;
    }

    /// <summary>Default from <see cref="DefaultValueAttribute"/> on the declaring property or Orivy field defaults.</summary>
    internal static bool TryGetCoreDefaultValue(object component, PropertyDescriptor descriptor, out object? defaultValue)
    {
        if (descriptor.Attributes[typeof(DefaultValueAttribute)] is DefaultValueAttribute descriptorDefault)
        {
            defaultValue = ConvertDefaultAttribute(descriptorDefault, descriptor.PropertyType);
            return true;
        }

        for (var type = component.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperty(
                descriptor.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property == null)
                continue;

            if (property.GetCustomAttribute<DefaultValueAttribute>() is { } propertyDefault)
            {
                defaultValue = ConvertDefaultAttribute(propertyDefault, descriptor.PropertyType);
                return true;
            }
        }

        if (TryGetImplicitCoreDefault(descriptor.Name, descriptor.PropertyType, out defaultValue))
            return true;

        defaultValue = null;
        return false;
    }

    private static object? ConvertDefaultAttribute(DefaultValueAttribute attribute, Type propertyType)
    {
        var value = attribute.Value;
        if (value == null || propertyType.IsInstanceOfType(value))
            return value;

        if (value is string text)
        {
            var converter = TypeDescriptor.GetConverter(propertyType);
            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(text);
        }

        return value;
    }

    private static bool TryGetImplicitCoreDefault(string propertyName, Type propertyType, out object? defaultValue)
    {
        defaultValue = null;
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        switch (propertyName)
        {
            case "Anchor" when type == typeof(AnchorStyles):
                defaultValue = AnchorStyles.Top | AnchorStyles.Left;
                return true;
            case "Dock" when type == typeof(DockStyle):
                defaultValue = DockStyle.None;
                return true;
            case "Visible" when type == typeof(bool):
                defaultValue = true;
                return true;
            case "Enabled" when type == typeof(bool):
                defaultValue = true;
                return true;
            case "AutoSize" when type == typeof(bool):
                defaultValue = false;
                return true;
            case "Text" when type == typeof(string):
                defaultValue = string.Empty;
                return true;
        }

        if (type == typeof(string))
        {
            defaultValue = string.Empty;
            return true;
        }

        if (type == typeof(bool))
        {
            defaultValue = false;
            return true;
        }

        if (type == typeof(SKColor))
        {
            defaultValue = SKColors.Empty;
            return true;
        }

        if (type == typeof(Thickness))
        {
            defaultValue = Thickness.Empty;
            return true;
        }

        if (type == typeof(SKSize))
        {
            defaultValue = new SKSize(0, 0);
            return true;
        }

        if (type == typeof(SKPoint))
        {
            defaultValue = new SKPoint(0, 0);
            return true;
        }

        if (type.IsEnum)
        {
            defaultValue = Enum.ToObject(type, 0);
            return true;
        }

        if (type.IsValueType)
        {
            defaultValue = Activator.CreateInstance(type);
            return true;
        }

        defaultValue = null;
        return false;
    }

    public static bool TryFormat(object? value, Type propertyType, string? designerFilePath, out string expression) =>
        DesignSourceFormat.TryFormatExpression(value, propertyType, designerFilePath, out expression);

    public static bool ValuesEqual(object? a, object? b, Type propertyType)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;

        if (a is SKColor ca && b is SKColor cb)
            return ca.Red == cb.Red && ca.Green == cb.Green && ca.Blue == cb.Blue && ca.Alpha == cb.Alpha;

        if (a is SKSize sa && b is SKSize sb)
            return NearlyEqual(sa.Width, sb.Width) && NearlyEqual(sa.Height, sb.Height);

        if (a is SKPoint pa && b is SKPoint pb)
            return NearlyEqual(pa.X, pb.X) && NearlyEqual(pa.Y, pb.Y);

        if (a is Thickness ta && b is Thickness tb)
            return ta.Left == tb.Left && ta.Top == tb.Top && ta.Right == tb.Right && ta.Bottom == tb.Bottom;

        if (a is Radius ra && b is Radius rb)
            return NearlyEqual(ra.TopLeft, rb.TopLeft) && NearlyEqual(ra.TopRight, rb.TopRight)
                && NearlyEqual(ra.BottomLeft, rb.BottomLeft) && NearlyEqual(ra.BottomRight, rb.BottomRight);

        return Equals(a, b);
    }

    private static bool NearlyEqual(float left, float right) =>
        Math.Abs(left - right) < 0.51f;

    /// <summary>Maps <c>control.prop</c>, <c>this.prop</c>, or bare <c>ClientSize</c> on the form.</summary>
    public static bool TryParseAssignmentTarget(
        AssignmentExpressionSyntax assign,
        out string? controlName,
        out string propertyName)
    {
        controlName = null;
        propertyName = string.Empty;

        switch (assign.Left)
        {
            case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax prop, Expression: IdentifierNameSyntax id }:
                controlName = id.Identifier.Text;
                propertyName = prop.Identifier.Text;
                return true;
            case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax propThis, Expression: ThisExpressionSyntax }:
                controlName = string.Empty;
                propertyName = propThis.Identifier.Text;
                return true;
            case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax propNested, Expression: MemberAccessExpressionSyntax nested }:
                if (nested.Expression is ThisExpressionSyntax && nested.Name is IdentifierNameSyntax mid)
                {
                    controlName = mid.Identifier.Text;
                    propertyName = propNested.Identifier.Text;
                    return true;
                }

                return false;
            case IdentifierNameSyntax { Identifier.Text: var bare }:
                controlName = string.Empty;
                propertyName = bare;
                return true;
            default:
                return false;
        }
    }
}
