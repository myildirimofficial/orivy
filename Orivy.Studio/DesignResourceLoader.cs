using System;
using System.Drawing;
using System.IO;
using System.Xml.Linq;

namespace Orivy.Studio;

/// <summary>
/// Loads values referenced by <c>resources.GetObject("…")</c> in imported Designer code from the
/// sibling <c>.resx</c> file (WinForms / RSBot style).
/// </summary>
internal static class DesignResourceLoader
{
    public static object? TryGetObject(string? designerFilePath, string resourceKey, Type targetType)
    {
        var resxPath = ResolveResxPath(designerFilePath);
        if (resxPath == null)
            return null;

        try
        {
            var doc = XDocument.Load(resxPath);
            if (doc.Root == null)
                return null;

            foreach (var data in doc.Root.Elements("data"))
            {
                if (!string.Equals(data.Attribute("name")?.Value, resourceKey, StringComparison.Ordinal))
                    continue;

                var mime = data.Attribute("mimetype")?.Value;
                if (mime != null)
                    continue;

                var typeName = data.Attribute("type")?.Value;
                var valueText = data.Element("value")?.Value;
                if (string.IsNullOrWhiteSpace(valueText))
                    return null;

                if (typeName != null && typeName.Contains("Icon", StringComparison.Ordinal)
                    && (targetType == typeof(Icon) || targetType == typeof(object)))
                {
                    var bytes = Convert.FromBase64String(valueText.Trim());
                    using var stream = new MemoryStream(bytes);
                    return new Icon(stream);
                }

                if (typeName != null && typeName.Contains("Bitmap", StringComparison.Ordinal)
                    && (targetType == typeof(Bitmap) || targetType == typeof(object)))
                {
                    var bytes = Convert.FromBase64String(valueText.Trim());
                    using var stream = new MemoryStream(bytes);
                    return new Bitmap(stream);
                }

                if (targetType == typeof(string))
                    return valueText;

                return null;
            }
        }
        catch
        {
            // Missing or malformed .resx — leave property at default.
        }

        return null;
    }

    private static string? ResolveResxPath(string? designerFilePath)
    {
        if (string.IsNullOrEmpty(designerFilePath))
            return null;

        var directory = Path.GetDirectoryName(designerFilePath);
        if (string.IsNullOrEmpty(directory))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(designerFilePath);
        if (baseName.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
            baseName = baseName[..^".Designer".Length];

        var candidate = Path.Combine(directory, baseName + ".resx");
        return File.Exists(candidate) ? candidate : null;
    }
}
