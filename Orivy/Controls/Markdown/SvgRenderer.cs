using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

/// <summary>
/// Parses and rasterizes SVG documents to SKImage using SkiaSharp 3.x APIs.
/// Implements advanced typography (dominant-baseline, tspan layout, font-family fallback),
/// gradient bounding box mapping, rounded group clipPaths, proper SVG transforms,
/// and high-quality anti-aliased rendering.
/// </summary>
public static class SvgRenderer
{
    /// <summary>
    /// Renders SVG bytes to an SKImage. Returns null on failure.
    /// Defaults to 2x scale for high-DPI crisp rendering.
    /// </summary>
    public static SKImage? Render(byte[] svgBytes, float targetScale = 2f)
    {
        try
        {
            string xml = System.Text.Encoding.UTF8.GetString(svgBytes);
            var doc  = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return null;

            var el = new SvgEl(root);

            float vpW = el.Float("width",  0f);
            float vpH = el.Float("height", 0f);
            var   vb  = el.ViewBox();

            if (vb.HasValue)
            {
                if (vpW <= 0f) vpW = vb.Value.Width;
                if (vpH <= 0f) vpH = vb.Value.Height;
            }
            if (vpW <= 0f) vpW = 512f;
            if (vpH <= 0f) vpH = 512f;

            int rw = Math.Clamp((int)(vpW * targetScale), 1, 8192);
            int rh = Math.Clamp((int)(vpH * targetScale), 1, 8192);

            var info = new SKImageInfo(rw, rh, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null) return null;

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(targetScale);

            if (vb.HasValue && vb.Value.Width > 0 && vb.Value.Height > 0)
            {
                ApplyViewBoxMapping(canvas, vpW, vpH, vb.Value, el.Attr("preserveAspectRatio"));
            }

            var defs = new Dictionary<string, XElement>(StringComparer.Ordinal);
            CollectDefs(root, defs);

            DrawElement(canvas, root, SvgState.Default(), defs);
            canvas.Flush();
            return surface.Snapshot();
        }
        catch { return null; }
    }

    private static void ApplyViewBoxMapping(SKCanvas canvas, float vpW, float vpH, SKRect vb, string? preserveAspectRatio)
    {
        float sx = vpW / vb.Width;
        float sy = vpH / vb.Height;
        bool isNone = preserveAspectRatio?.Contains("none", StringComparison.OrdinalIgnoreCase) ?? false;

        if (isNone)
        {
            canvas.Scale(sx, sy);
            canvas.Translate(-vb.Left, -vb.Top);
        }
        else
        {
            float scale = Math.Min(sx, sy);
            float tx = (vpW - vb.Width * scale) / 2f;
            float ty = (vpH - vb.Height * scale) / 2f;
            canvas.Translate(tx, ty);
            canvas.Scale(scale, scale);
            canvas.Translate(-vb.Left, -vb.Top);
        }
    }

    private static void CollectDefs(XElement el, Dictionary<string, XElement> defs)
    {
        foreach (var child in el.Elements())
        {
            var id = child.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id) && !defs.ContainsKey(id))
                defs[id] = child;
            
            CollectDefs(child, defs);
        }
    }

    private static void DrawElement(SKCanvas canvas, XElement xel, SvgState parent, Dictionary<string, XElement> defs)
    {
        var el   = new SvgEl(xel);
        string n = el.Name;

        if (n == "defs" || n == "title" || n == "desc" || n == "metadata") return;

        var state = el.ApplyState(parent, defs);
        bool display = (el.Attr("display") ?? state.Display) != "none";
        if (!display) return;

        string? visibility = el.Attr("visibility") ?? state.Visibility;
        bool visible = visibility != "hidden" && visibility != "collapse";

        if (n is "svg" or "g" or "a")
        {
            float opacity = state.Opacity;
            bool  useLayer = opacity < 1f && opacity >= 0f;

            int save;
            if (useLayer)
            {
                var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(opacity * 255)) };
                save = canvas.SaveLayer(paint);
                paint.Dispose();
            }
            else
            {
                save = canvas.Save();
            }

            ApplyTransform(canvas, el);
            ApplyClipPath(canvas, el, defs);

            foreach (var child in xel.Elements())
                DrawElement(canvas, child, state, defs);

            canvas.RestoreToCount(save);
            return;
        }

        if (n == "symbol" || n == "clipPath") return;

        if (n == "use")
        {
            DrawUse(canvas, el, xel, state, defs);
            return;
        }

        if (!visible) return;

        int shapeSave = canvas.Save();
        ApplyTransform(canvas, el);
        ApplyClipPath(canvas, el, defs);

        try
        {
            switch (n)
            {
                case "path":     DrawPath    (canvas, el, state); break;
                case "rect":     DrawRect    (canvas, el, state); break;
                case "circle":   DrawCircle  (canvas, el, state); break;
                case "ellipse":  DrawEllipse (canvas, el, state); break;
                case "line":     DrawLine    (canvas, el, state); break;
                case "polyline": DrawPolyline(canvas, el, state, close: false); break;
                case "polygon":  DrawPolyline(canvas, el, state, close: true);  break;
                case "text":     DrawText    (canvas, el, state, defs); break;
                case "image":    DrawImage   (canvas, el, state); break;
            }
        }
        finally { canvas.RestoreToCount(shapeSave); }
    }

    private static void DrawUse(SKCanvas canvas, SvgEl el, XElement xel, SvgState state, Dictionary<string, XElement> defs)
    {
        string? href = el.Attr("href") ?? el.Attr("xlink:href");
        if (string.IsNullOrEmpty(href) || !href.StartsWith('#')) return;
        if (!defs.TryGetValue(href[1..], out var target)) return;

        int save = canvas.Save();
        ApplyTransform(canvas, el);

        float x = el.Float("x", 0f);
        float y = el.Float("y", 0f);
        if (x != 0f || y != 0f) canvas.Translate(x, y);

        string tn = target.Name.LocalName;
        if (tn == "symbol")
        {
            var symEl = new SvgEl(target);
            var symVb = symEl.ViewBox();
            float sw  = el.Float("width",  symEl.Float("width",  0f));
            float sh  = el.Float("height", symEl.Float("height", 0f));

            if (symVb.HasValue && sw > 0 && sh > 0)
            {
                ApplyViewBoxMapping(canvas, sw, sh, symVb.Value, symEl.Attr("preserveAspectRatio") ?? el.Attr("preserveAspectRatio"));
            }

            foreach (var child in target.Elements())
                DrawElement(canvas, child, state, defs);
        }
        else
        {
            DrawElement(canvas, target, state, defs);
        }

        canvas.RestoreToCount(save);
    }

    private static void DrawPath(SKCanvas canvas, SvgEl el, SvgState state)
    {
        string? d = el.Attr("d");
        if (string.IsNullOrEmpty(d)) return;

        using var path = SKPath.ParseSvgPathData(d);
        if (path == null) return;

        path.FillType = state.FillRule;
        DrawFillStroke(canvas, state, path);
    }

    private static void DrawRect(SKCanvas canvas, SvgEl el, SvgState state)
    {
        float x  = el.Float("x",      0f);
        float y  = el.Float("y",      0f);
        float w  = el.Float("width",  0f);
        float h  = el.Float("height", 0f);
        if (w <= 0 || h <= 0) return;

        float rx = el.Float("rx", -1f);
        float ry = el.Float("ry", -1f);
        if (rx < 0 && ry < 0) { rx = 0; ry = 0; }
        else if (rx < 0) rx = ry;
        else if (ry < 0) ry = rx;

        var rect = new SKRect(x, y, x + w, y + h);

        if (rx > 0 || ry > 0)
        {
            using var path = new SKPath();
            path.AddRoundRect(rect, rx, ry);
            DrawFillStroke(canvas, state, path);
        }
        else
        {
            DrawFillStroke(canvas, state, rect);
        }
    }

    private static void DrawCircle(SKCanvas canvas, SvgEl el, SvgState state)
    {
        float cx = el.Float("cx", 0f);
        float cy = el.Float("cy", 0f);
        float r  = el.Float("r",  0f);
        if (r <= 0) return;

        using var path = new SKPath();
        path.AddCircle(cx, cy, r);
        DrawFillStroke(canvas, state, path);
    }

    private static void DrawEllipse(SKCanvas canvas, SvgEl el, SvgState state)
    {
        float cx = el.Float("cx", 0f);
        float cy = el.Float("cy", 0f);
        float rx = el.Float("rx", 0f);
        float ry = el.Float("ry", 0f);
        if (rx <= 0 || ry <= 0) return;

        using var path = new SKPath();
        path.AddOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry));
        DrawFillStroke(canvas, state, path);
    }

    private static void DrawLine(SKCanvas canvas, SvgEl el, SvgState state)
    {
        float x1 = el.Float("x1", 0f); float y1 = el.Float("y1", 0f);
        float x2 = el.Float("x2", 0f); float y2 = el.Float("y2", 0f);

        if (!state.HasStroke) return;
        using var paint = state.MakeStrokePaint(SKRect.Empty);
        if (paint != null) canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    private static void DrawPolyline(SKCanvas canvas, SvgEl el, SvgState state, bool close)
    {
        string? pts = el.Attr("points");
        if (string.IsNullOrEmpty(pts)) return;

        var nums = NumbersFromString(pts);
        if (nums.Count < 4) return;

        using var path = new SKPath();
        path.MoveTo(nums[0], nums[1]);
        for (int i = 2; i + 1 < nums.Count; i += 2)
            path.LineTo(nums[i], nums[i + 1]);
        if (close) path.Close();

        DrawFillStroke(canvas, state, path);
    }

    /// <summary>
    /// Advanced Text Engine: Handles dominant-baseline, tspan layout, font-family fallback, and textLength.
    /// Uses a two-pass measurement to correctly center text with mixed tspan sizes.
    /// </summary>
    private static void DrawText(SKCanvas canvas, SvgEl el, SvgState state, Dictionary<string, XElement> defs)
    {
        float x = el.Float("x", 0f);
        float y = el.Float("y", 0f);
        float fontSize = el.Float("font-size", state.FontSize > 0 ? state.FontSize : 16f);
        string? anchor = el.Attr("text-anchor") ?? state.TextAnchor;
        string? domBaseline = el.Attr("dominant-baseline") ?? el.Attr("alignment-baseline") ?? state.DominantBaseline;
        string? fontFamily = el.Attr("font-family") ?? state.FontFamily;
        string? fontWeight = el.Attr("font-weight") ?? state.FontWeight;
        string? fontStyle = el.Attr("font-style") ?? state.FontStyle;

        SKTypeface tf = GetTypeface(fontFamily, fontWeight, fontStyle);
        using var font = new SKFont(tf, fontSize)
        {
            // CRITICAL FIX: Enforce high quality font rendering to prevent "pixel pixel" jagged text
            Edging = SKFontEdging.Antialias,
            Hinting = SKFontHinting.Full,
            Subpixel = true
        };

        // textLength support
        float textLength = el.Float("textLength", -1f);
        float naturalTotalWidth = MeasureTextWidth(el.XNode, font);
        if (textLength > 0 && naturalTotalWidth > 0)
        {
            font.ScaleX = textLength / naturalTotalWidth;
        }

        float baselineY = GetBaselineY(y, domBaseline, font.Metrics);

        float currentX = x;
        // Pre-calculate anchor offset using accurate two-pass measurement
        float actualTotalWidth = MeasureTextWidth(el.XNode, font);
        if (anchor == "middle") currentX = x - actualTotalWidth / 2f;
        else if (anchor == "end") currentX = x - actualTotalWidth;

        DrawTextNodes(canvas, el.XNode, font, state, defs, ref currentX, ref baselineY);
    }

    private static float MeasureTextWidth(XContainer node, SKFont font)
    {
        float width = 0;
        foreach (var child in node.Nodes())
        {
            if (child is XText txt)
            {
                width += font.MeasureText(txt.Value);
            }
            else if (child is XElement elNode && elNode.Name.LocalName == "tspan")
            {
                var tspanEl = new SvgEl(elNode);
                float tspanFontSize = tspanEl.Float("font-size", font.Size);
                using var tspanFont = new SKFont(font.Typeface, tspanFontSize)
                {
                    Edging = SKFontEdging.Antialias,
                    Hinting = SKFontHinting.Full,
                    Subpixel = true
                };
                tspanFont.ScaleX = font.ScaleX;
                width += MeasureTextWidth(elNode, tspanFont);
            }
        }
        return width;
    }

    private static void DrawTextNodes(SKCanvas canvas, XContainer node, SKFont font, SvgState parentState, Dictionary<string, XElement> defs, ref float currentX, ref float currentY)
    {
        foreach (var child in node.Nodes())
        {
            if (child is XText txt)
            {
                string text = txt.Value;
                if (string.IsNullOrEmpty(text)) continue;

                if (parentState.HasFill)
                {
                    SKRect bounds = SKRect.Empty;
                    font.MeasureText(text, out bounds);
                    bounds.Offset(currentX, currentY);

                    using var paint = parentState.MakeFillPaint(bounds);
                    if (paint != null)
                    {
                        canvas.DrawText(text, currentX, currentY, SKTextAlign.Left, font, paint);
                    }
                }
                currentX += font.MeasureText(text);
            }
            else if (child is XElement elNode && elNode.Name.LocalName == "tspan")
            {
                var tspanEl = new SvgEl(elNode);
                var tspanState = tspanEl.ApplyState(parentState, defs);

                float dx = tspanEl.Float("dx", 0f);
                float dy = tspanEl.Float("dy", 0f);

                if (tspanEl.Attr("x") != null) currentX = tspanEl.Float("x", currentX);
                if (tspanEl.Attr("y") != null)
                {
                    float newY = tspanEl.Float("y", currentY);
                    string? domBaseline = tspanEl.Attr("dominant-baseline") ?? tspanState.DominantBaseline;
                    currentY = GetBaselineY(newY, domBaseline, font.Metrics);
                }

                currentX += dx;
                currentY += dy;

                float tspanFontSize = tspanEl.Float("font-size", tspanState.FontSize);
                if (tspanFontSize != font.Size)
                {
                    using var tspanFont = new SKFont(font.Typeface, tspanFontSize)
                    {
                        Edging = SKFontEdging.Antialias,
                        Hinting = SKFontHinting.Full,
                        Subpixel = true
                    };
                    tspanFont.ScaleX = font.ScaleX;
                    DrawTextNodes(canvas, elNode, tspanFont, tspanState, defs, ref currentX, ref currentY);
                }
                else
                {
                    DrawTextNodes(canvas, elNode, font, tspanState, defs, ref currentX, ref currentY);
                }
            }
        }
    }

    private static float GetBaselineY(float y, string? dominantBaseline, SKFontMetrics metrics)
    {
        if (string.IsNullOrEmpty(dominantBaseline)) return y;

        float textHeight = metrics.Descent - metrics.Ascent;
        float baselineOffset = (textHeight / 2f) - metrics.Descent;

        return dominantBaseline.ToLowerInvariant() switch
        {
            "middle" or "central" => y + baselineOffset,
            "hanging" or "text-before-edge" or "text-top" => y - metrics.Ascent,
            "text-after-edge" or "text-bottom" => y - metrics.Descent,
            _ => y
        };
    }

    private static SKTypeface GetTypeface(string? fontFamily, string? fontWeight, string? fontStyle)
    {
        SKFontStyleWeight weight = SKFontStyleWeight.Normal;
        if (!string.IsNullOrEmpty(fontWeight))
        {
            if (fontWeight == "bold" || (float.TryParse(fontWeight, out float w) && w >= 600))
                weight = SKFontStyleWeight.Bold;
        }

        SKFontStyleSlant slant = SKFontStyleSlant.Upright;
        if (!string.IsNullOrEmpty(fontStyle))
        {
            if (fontStyle == "italic" || fontStyle == "oblique")
                slant = SKFontStyleSlant.Italic;
        }

        if (!string.IsNullOrEmpty(fontFamily))
        {
            var fonts = fontFamily.Split(',');
            foreach (var f in fonts)
            {
                string name = f.Trim().Trim('\'', '"').ToLowerInvariant();
                string actualName = name switch
                {
                    "sans-serif" => "Arial",
                    "serif" => "Times New Roman",
                    "monospace" => "Courier New",
                    _ => f.Trim().Trim('\'', '"')
                };
                
                var tf = SKTypeface.FromFamilyName(actualName, weight, SKFontStyleWidth.Normal, slant);
                if (tf != null) return tf;
            }
        }
        
        return SKTypeface.FromFamilyName("Arial", weight, SKFontStyleWidth.Normal, slant) ?? SKTypeface.Default;
    }

    private static void DrawImage(SKCanvas canvas, SvgEl el, SvgState state)
    {
        string? href = el.Attr("href") ?? el.Attr("xlink:href");
        if (string.IsNullOrEmpty(href)) return;

        float x = el.Float("x", 0f);
        float y = el.Float("y", 0f);
        float w = el.Float("width",  0f);
        float h = el.Float("height", 0f);

        SKImage? img = null;
        if (href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int comma = href.IndexOf(',');
            if (comma >= 0 && href.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(href[(comma + 1)..]);
                    using var data = SKData.CreateCopy(bytes);
                    img = SKImage.FromEncodedData(data);
                }
                catch { }
            }
        }

        if (img == null) return;
        using (img)
        {
            var dest = w > 0 && h > 0
                ? new SKRect(x, y, x + w, y + h)
                : new SKRect(x, y, x + img.Width, y + img.Height);

            using var paint = new SKPaint
            {
                IsAntialias = true
            };

            var sampling = new SKSamplingOptions(SKFilterMode.Linear);

            canvas.DrawImage(img, dest, sampling, paint);
        }
    }

    private static void DrawFillStroke(SKCanvas canvas, SvgState state, SKPath path)
    {
        if (state.HasFill)
        {
            using var p = state.MakeFillPaint(path.Bounds);
            if (p != null) canvas.DrawPath(path, p);
        }
        if (state.HasStroke)
        {
            using var p = state.MakeStrokePaint(path.Bounds);
            if (p != null) canvas.DrawPath(path, p);
        }
    }

    private static void DrawFillStroke(SKCanvas canvas, SvgState state, SKRect rect)
    {
        if (state.HasFill)
        {
            using var p = state.MakeFillPaint(rect);
            if (p != null) canvas.DrawRect(rect, p);
        }
        if (state.HasStroke)
        {
            using var p = state.MakeStrokePaint(rect);
            if (p != null) canvas.DrawRect(rect, p);
        }
    }

    private static void ApplyTransform(SKCanvas canvas, SvgEl el)
    {
        string? t = el.Attr("transform");
        if (!string.IsNullOrEmpty(t))
        {
            var m = SvgTransformParser.Parse(t);
            canvas.Concat(m);
        }
    }

    private static void ApplyClipPath(SKCanvas canvas, SvgEl el, Dictionary<string, XElement> defs)
    {
        string? cp = el.Attr("clip-path");
        if (string.IsNullOrEmpty(cp)) return;
        var m = Regex.Match(cp, @"url\(#([^)]+)\)");
        if (!m.Success) return;
        if (!defs.TryGetValue(m.Groups[1].Value, out var cpEl)) return;

        using var clipPath = new SKPath();
        foreach (var child in cpEl.Elements())
        {
            var cel = new SvgEl(child);
            string cn = cel.Name;
            switch (cn)
            {
                case "rect":
                {
                    float x = cel.Float("x", 0); float y = cel.Float("y", 0);
                    float w = cel.Float("width", 0); float h = cel.Float("height", 0);
                    float rx = cel.Float("rx", 0); float ry = cel.Float("ry", 0);
                    
                    if (rx > 0 && ry == 0) ry = rx;
                    if (ry > 0 && rx == 0) rx = ry;
                    
                    if (w > 0 && h > 0) 
                    {
                        using var p = new SKPath();
                        if (rx > 0 || ry > 0)
                            p.AddRoundRect(new SKRect(x, y, x + w, y + h), rx, ry);
                        else
                            p.AddRect(new SKRect(x, y, x + w, y + h));

                        string? t = cel.Attr("transform");
                        if (!string.IsNullOrEmpty(t)) p.Transform(SvgTransformParser.Parse(t));
                        clipPath.AddPath(p);
                    }
                    break;
                }
                case "circle":
                {
                    float cx = cel.Float("cx", 0); float cy = cel.Float("cy", 0);
                    float r  = cel.Float("r", 0);
                    if (r > 0) 
                    {
                        using var p = new SKPath();
                        p.AddCircle(cx, cy, r);
                        string? t = cel.Attr("transform");
                        if (!string.IsNullOrEmpty(t)) p.Transform(SvgTransformParser.Parse(t));
                        clipPath.AddPath(p);
                    }
                    break;
                }
                case "path":
                {
                    string? d = cel.Attr("d");
                    if (!string.IsNullOrEmpty(d))
                    {
                        using var p = SKPath.ParseSvgPathData(d);
                        if (p != null) 
                        {
                            string? t = cel.Attr("transform");
                            if (!string.IsNullOrEmpty(t)) p.Transform(SvgTransformParser.Parse(t));
                            clipPath.AddPath(p);
                        }
                    }
                    break;
                }
            }
        }
        canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
    }

    private static readonly Regex _numRx =
        new(@"-?\d*\.?\d+(?:[eE][+-]?\d+)?", RegexOptions.Compiled);

    private static List<float> NumbersFromString(string s)
    {
        var list = new List<float>();
        foreach (Match m in _numRx.Matches(s))
            if (float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                list.Add(f);
        return list;
    }

    internal static float ParseFloat(string? value, float def = 0f)
    {
        if (string.IsNullOrWhiteSpace(value)) return def;
        value = value.Trim();

        foreach (string unit in new[] { "px", "pt", "pc", "mm", "cm", "in", "em", "rem", "%" })
            if (value.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            { value = value[..^unit.Length]; break; }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : def;
    }
}

internal readonly struct SvgEl
{
    public readonly XElement XNode;
    public string Name => XNode.Name.LocalName;

    public SvgEl(XElement el) => XNode = el;

    public string? Attr(string name)
    {
        var a = XNode.Attribute(name);
        if (a != null) return string.IsNullOrEmpty(a.Value) ? null : a.Value;

        if (name.StartsWith("xlink:"))
        {
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            a = XNode.Attribute(xlink + name[6..]);
            if (a != null) return string.IsNullOrEmpty(a.Value) ? null : a.Value;
        }
        return null;
    }

    public float Float(string name, float def = 0f) =>
        SvgRenderer.ParseFloat(Attr(name), def);

    public SKRect? ViewBox()
    {
        string? vb = Attr("viewBox");
        if (string.IsNullOrEmpty(vb)) return null;
        var p = vb.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length != 4) return null;
        float x = SvgRenderer.ParseFloat(p[0]);
        float y = SvgRenderer.ParseFloat(p[1]);
        float w = SvgRenderer.ParseFloat(p[2]);
        float h = SvgRenderer.ParseFloat(p[3]);
        return new SKRect(x, y, x + w, y + h);
    }

    public SvgState ApplyState(SvgState parent, Dictionary<string, XElement> defs)
    {
        var s = parent.Clone();

        string? style = Attr("style");
        if (!string.IsNullOrEmpty(style))
        {
            foreach (string decl in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = decl.IndexOf(':');
                if (colon < 0) continue;
                ApplyProp(s, decl[..colon].Trim(), decl[(colon + 1)..].Trim(), defs);
            }
        }

        ApplyPropOpt(s, "fill",             defs);
        ApplyPropOpt(s, "fill-opacity",     defs);
        ApplyPropOpt(s, "fill-rule",        defs);
        ApplyPropOpt(s, "stroke",           defs);
        ApplyPropOpt(s, "stroke-opacity",   defs);
        ApplyPropOpt(s, "stroke-width",     defs);
        ApplyPropOpt(s, "stroke-linecap",   defs);
        ApplyPropOpt(s, "stroke-linejoin",  defs);
        ApplyPropOpt(s, "stroke-dasharray", defs);
        ApplyPropOpt(s, "opacity",          defs);
        ApplyPropOpt(s, "font-size",        defs);
        ApplyPropOpt(s, "font-family",      defs);
        ApplyPropOpt(s, "font-weight",      defs);
        ApplyPropOpt(s, "font-style",       defs);
        ApplyPropOpt(s, "text-anchor",      defs);
        ApplyPropOpt(s, "dominant-baseline",defs);
        ApplyPropOpt(s, "display",          defs);
        ApplyPropOpt(s, "visibility",       defs);

        return s;
    }

    private void ApplyPropOpt(SvgState s, string key, Dictionary<string, XElement> defs)
    {
        string? v = Attr(key);
        if (v != null) ApplyProp(s, key, v, defs);
    }

    private static void ApplyProp(SvgState s, string key, string val, Dictionary<string, XElement> defs)
    {
        if (val == "inherit") return;

        switch (key)
        {
            case "fill":
                if (val == "none") { s.Fill = null; s.FillShader = null; }
                else if (val.StartsWith("url(#", StringComparison.Ordinal))
                {
                    string id = val[5..^1];
                    if (defs.TryGetValue(id, out var g))
                    {
                        bool isBB = (g.Attribute("gradientUnits")?.Value ?? "objectBoundingBox").Equals("objectBoundingBox", StringComparison.OrdinalIgnoreCase);
                        s.FillShader = SvgGradientParser.ParseGradient(g, isBB);
                        s.FillShaderIsObjectBoundingBox = isBB;
                    }
                    else s.FillShader = null;
                    if (s.FillShader != null) s.Fill = null;
                }
                else { s.Fill = SvgColorParser.Parse(val); s.FillShader = null; }
                break;

            case "fill-opacity":
                s.FillOpacity = Clamp01(val);
                break;

            case "fill-rule":
                s.FillRule = val == "evenodd" ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
                break;

            case "stroke":
                if (val == "none") { s.Stroke = null; s.StrokeShader = null; }
                else if (val.StartsWith("url(#", StringComparison.Ordinal))
                {
                    string id = val[5..^1];
                    if (defs.TryGetValue(id, out var g))
                    {
                        bool isBB = (g.Attribute("gradientUnits")?.Value ?? "objectBoundingBox").Equals("objectBoundingBox", StringComparison.OrdinalIgnoreCase);
                        s.StrokeShader = SvgGradientParser.ParseGradient(g, isBB);
                        s.StrokeShaderIsObjectBoundingBox = isBB;
                    }
                    else s.StrokeShader = null;
                    if (s.StrokeShader != null) s.Stroke = null;
                }
                else { s.Stroke = SvgColorParser.Parse(val); s.StrokeShader = null; }
                break;

            case "stroke-opacity":
                s.StrokeOpacity = Clamp01(val);
                break;

            case "stroke-width":
                s.StrokeWidth = Math.Max(0f, SvgRenderer.ParseFloat(val));
                break;

            case "stroke-linecap":
                s.StrokeCap = val switch
                {
                    "round"  => SKStrokeCap.Round,
                    "square" => SKStrokeCap.Square,
                    _        => SKStrokeCap.Butt
                };
                break;

            case "stroke-linejoin":
                s.StrokeJoin = val switch
                {
                    "round" => SKStrokeJoin.Round,
                    "bevel" => SKStrokeJoin.Bevel,
                    _       => SKStrokeJoin.Miter
                };
                break;

            case "stroke-dasharray":
                if (val == "none") { s.DashArray = null; }
                else
                {
                    var nums = new List<float>();
                    foreach (Match m in Regex.Matches(val, @"-?\d*\.?\d+(?:[eE][+-]?\d+)?"))
                    {
                        if (float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) nums.Add(f);
                    }
                    s.DashArray = nums.Count > 0 ? nums.ToArray() : null;
                }
                break;

            case "opacity":
                s.Opacity = Clamp01(val);
                break;

            case "font-size":
                s.FontSize = SvgRenderer.ParseFloat(val, s.FontSize);
                break;

            case "font-family":
                s.FontFamily = val;
                break;

            case "font-weight":
                s.FontWeight = val;
                break;

            case "font-style":
                s.FontStyle = val;
                break;

            case "text-anchor":
                s.TextAnchor = val;
                break;

            case "dominant-baseline":
            case "alignment-baseline":
                s.DominantBaseline = val;
                break;

            case "display":
                s.Display = val;
                break;

            case "visibility":
                s.Visibility = val;
                break;
        }
    }

    private static float Clamp01(string v) =>
        Math.Clamp(SvgRenderer.ParseFloat(v, 1f), 0f, 1f);
}

internal sealed class SvgState
{
    public SKColor?   Fill        = SKColors.Black;
    public SKShader?  FillShader  = null;
    public bool       FillShaderIsObjectBoundingBox = false;
    public float      FillOpacity = 1f;
    public SKPathFillType FillRule = SKPathFillType.Winding;

    public SKColor?   Stroke        = null;
    public SKShader?  StrokeShader  = null;
    public bool       StrokeShaderIsObjectBoundingBox = false;
    public float      StrokeOpacity = 1f;
    public float      StrokeWidth   = 1f;
    public SKStrokeCap  StrokeCap   = SKStrokeCap.Butt;
    public SKStrokeJoin StrokeJoin  = SKStrokeJoin.Miter;
    public float[]?   DashArray     = null;

    public float  Opacity    = 1f;
    public float  FontSize   = 16f;
    public string? FontFamily = null;
    public string? FontWeight = null;
    public string? FontStyle  = null;
    public string TextAnchor = "start";
    public string? DominantBaseline = null;
    public string Display    = "inline";
    public string Visibility = "visible";

    public bool HasFill   => Fill.HasValue   || FillShader   != null;
    public bool HasStroke => Stroke.HasValue || StrokeShader != null;

    public static SvgState Default() => new();

    public SvgState Clone() => new()
    {
        Fill          = Fill,
        FillShader    = FillShader,
        FillShaderIsObjectBoundingBox = FillShaderIsObjectBoundingBox,
        FillOpacity   = FillOpacity,
        FillRule      = FillRule,
        Stroke        = Stroke,
        StrokeShader  = StrokeShader,
        StrokeShaderIsObjectBoundingBox = StrokeShaderIsObjectBoundingBox,
        StrokeOpacity = StrokeOpacity,
        StrokeWidth   = StrokeWidth,
        StrokeCap     = StrokeCap,
        StrokeJoin    = StrokeJoin,
        DashArray     = DashArray,
        Opacity       = Opacity,
        FontSize      = FontSize,
        FontFamily    = FontFamily,
        FontWeight    = FontWeight,
        FontStyle     = FontStyle,
        TextAnchor    = TextAnchor,
        DominantBaseline = DominantBaseline,
        Display       = Display,
        Visibility    = Visibility,
    };

    public SKPaint? MakeFillPaint(SKRect bounds)
    {
        if (!HasFill) return null;
        float alpha = Opacity * FillOpacity;
        var p = new SKPaint { 
            IsAntialias = true, 
            Style = SKPaintStyle.Fill
        };

        if (FillShader != null)
        {
            var shader = FillShader;
            if (FillShaderIsObjectBoundingBox)
            {
                var bbMatrix = SKMatrix.CreateTranslation(bounds.Left, bounds.Top)
                    .PreConcat(SKMatrix.CreateScale(bounds.Width, bounds.Height));
                shader = shader.WithLocalMatrix(bbMatrix);
            }
            p.Shader = shader;
            p.Color  = SKColors.White.WithAlpha((byte)(alpha * 255f + 0.5f));
        }
        else
        {
            var c = Fill!.Value;
            p.Color = c.WithAlpha((byte)(c.Alpha * alpha + 0.5f));
        }
        return p;
    }

    public SKPaint? MakeStrokePaint(SKRect bounds)
    {
        if (!HasStroke || StrokeWidth <= 0) return null;
        float alpha = Opacity * StrokeOpacity;
        var p = new SKPaint
        {
            IsAntialias = true,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = StrokeWidth,
            StrokeCap   = StrokeCap,
            StrokeJoin  = StrokeJoin
        };

        if (DashArray != null && DashArray.Length > 0)
            p.PathEffect = SKPathEffect.CreateDash(DashArray, 0f);

        if (StrokeShader != null)
        {
            var shader = StrokeShader;
            if (StrokeShaderIsObjectBoundingBox)
            {
                var bbMatrix = SKMatrix.CreateTranslation(bounds.Left, bounds.Top)
                    .PreConcat(SKMatrix.CreateScale(bounds.Width, bounds.Height));
                shader = shader.WithLocalMatrix(bbMatrix);
            }
            p.Shader = shader;
            p.Color  = SKColors.White.WithAlpha((byte)(alpha * 255f + 0.5f));
        }
        else
        {
            var c = Stroke!.Value;
            p.Color = c.WithAlpha((byte)(c.Alpha * alpha + 0.5f));
        }
        return p;
    }
}

internal static class SvgTransformParser
{
    private static readonly Regex _txRx =
        new(@"(\w+)\s*\(([^)]*)\)", RegexOptions.Compiled);

    public static SKMatrix Parse(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform)) return SKMatrix.Identity;

        var result = SKMatrix.Identity;

        foreach (Match m in _txRx.Matches(transform))
        {
            string type = m.Groups[1].Value.ToLowerInvariant();
            var args = m.Groups[2].Value
                .Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            float F(int i, float d = 0f) =>
                i < args.Length ? SvgRenderer.ParseFloat(args[i], d) : d;

            SKMatrix parsed = type switch
            {
                "matrix" when args.Length >= 6 =>
                    new SKMatrix(
                        scaleX: F(0), skewX:  F(2), transX: F(4),
                        skewY:  F(1), scaleY: F(3), transY: F(5),
                        persp0: 0f,   persp1: 0f,   persp2: 1f),

                "translate" =>
                    SKMatrix.CreateTranslation(F(0), F(1)),

                "scale" =>
                    SKMatrix.CreateScale(F(0, 1f), args.Length > 1 ? F(1, 1f) : F(0, 1f)),

                "rotate" when args.Length >= 3 =>
                    SKMatrix.CreateTranslation(F(1), F(2))
                        .PreConcat(SKMatrix.CreateRotationDegrees(F(0)))
                        .PreConcat(SKMatrix.CreateTranslation(-F(1), -F(2))),

                "rotate" =>
                    SKMatrix.CreateRotationDegrees(F(0)),

                "skewx" =>
                    SKMatrix.CreateSkew((float)Math.Tan(F(0) * Math.PI / 180.0), 0f),

                "skewy" =>
                    SKMatrix.CreateSkew(0f, (float)Math.Tan(F(0) * Math.PI / 180.0)),

                _ => SKMatrix.Identity
            };

            result = result.PreConcat(parsed);
        }

        return result;
    }
}

internal static class SvgColorParser
{
    public static SKColor Parse(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return SKColors.Black;
        color = color.Trim();

        if (color.Equals("none",         StringComparison.OrdinalIgnoreCase)) return SKColors.Transparent;
        if (color.Equals("transparent",  StringComparison.OrdinalIgnoreCase)) return SKColors.Transparent;
        if (color.Equals("currentcolor", StringComparison.OrdinalIgnoreCase)) return SKColors.Black;

        if (color.StartsWith('#'))
        {
            try { return SKColor.Parse(color); } catch { return SKColors.Black; }
        }

        var rgbM = Regex.Match(color, @"rgba?\(\s*([^)]+)\)", RegexOptions.IgnoreCase);
        if (rgbM.Success)
        {
            var parts = rgbM.Groups[1].Value.Split(',');
            if (parts.Length >= 3)
            {
                byte R = Component(parts[0]);
                byte G = Component(parts[1]);
                byte B = Component(parts[2]);
                byte A = parts.Length >= 4
                    ? (byte)(float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float a) ? a * 255f : 255f)
                    : (byte)255;
                return new SKColor(R, G, B, A);
            }
        }

        var hslM = Regex.Match(color, @"hsla?\(\s*([^)]+)\)", RegexOptions.IgnoreCase);
        if (hslM.Success)
        {
            var parts = hslM.Groups[1].Value.Split(',');
            if (parts.Length >= 3)
            {
                float h = SvgRenderer.ParseFloat(parts[0]) / 360f;
                float s = SvgRenderer.ParseFloat(parts[1].TrimEnd('%')) / 100f;
                float l = SvgRenderer.ParseFloat(parts[2].TrimEnd('%')) / 100f;
                float a = parts.Length >= 4 ? SvgRenderer.ParseFloat(parts[3]) : 1f;
                return HslToRgb(h, s, l, a);
            }
        }

        return color.ToLowerInvariant() switch
        {
            "aliceblue"            => new SKColor(240,248,255),
            "antiquewhite"         => new SKColor(250,235,215),
            "aqua"                 => new SKColor(  0,255,255),
            "aquamarine"           => new SKColor(127,255,212),
            "azure"                => new SKColor(240,255,255),
            "beige"                => new SKColor(245,245,220),
            "bisque"               => new SKColor(255,228,196),
            "black"                => SKColors.Black,
            "blanchedalmond"       => new SKColor(255,235,205),
            "blue"                 => SKColors.Blue,
            "blueviolet"           => new SKColor(138, 43,226),
            "brown"                => new SKColor(165, 42, 42),
            "burlywood"            => new SKColor(222,184,135),
            "cadetblue"            => new SKColor( 95,158,160),
            "chartreuse"           => new SKColor(127,255,  0),
            "chocolate"            => new SKColor(210,105, 30),
            "coral"                => new SKColor(255,127, 80),
            "cornflowerblue"       => new SKColor(100,149,237),
            "cornsilk"             => new SKColor(255,248,220),
            "crimson"              => new SKColor(220, 20, 60),
            "cyan"                 => SKColors.Cyan,
            "darkblue"             => new SKColor(  0,  0,139),
            "darkcyan"             => new SKColor(  0,139,139),
            "darkgoldenrod"        => new SKColor(184,134, 11),
            "darkgray"             => new SKColor(169,169,169),
            "darkgreen"            => new SKColor(  0,100,  0),
            "darkgrey"             => new SKColor(169,169,169),
            "darkkhaki"            => new SKColor(189,183,107),
            "darkmagenta"          => new SKColor(139,  0,139),
            "darkolivegreen"       => new SKColor( 85,107, 47),
            "darkorange"           => new SKColor(255,140,  0),
            "darkorchid"           => new SKColor(153, 50,204),
            "darkred"              => new SKColor(139,  0,  0),
            "darksalmon"           => new SKColor(233,150,122),
            "darkseagreen"         => new SKColor(143,188,143),
            "darkslateblue"        => new SKColor( 72, 61,139),
            "darkslategray"        => new SKColor( 47, 79, 79),
            "darkslategrey"        => new SKColor( 47, 79, 79),
            "darkturquoise"        => new SKColor(  0,206,209),
            "darkviolet"           => new SKColor(148,  0,211),
            "deeppink"             => new SKColor(255, 20,147),
            "deepskyblue"          => new SKColor(  0,191,255),
            "dimgray"              => new SKColor(105,105,105),
            "dimgrey"              => new SKColor(105,105,105),
            "dodgerblue"           => new SKColor( 30,144,255),
            "firebrick"            => new SKColor(178, 34, 34),
            "floralwhite"          => new SKColor(255,250,240),
            "forestgreen"          => new SKColor( 34,139, 34),
            "fuchsia"              => new SKColor(255,  0,255),
            "gainsboro"            => new SKColor(220,220,220),
            "ghostwhite"           => new SKColor(248,248,255),
            "gold"                 => new SKColor(255,215,  0),
            "goldenrod"            => new SKColor(218,165, 32),
            "gray"                 => SKColors.Gray,
            "green"                => SKColors.Green,
            "greenyellow"          => new SKColor(173,255, 47),
            "grey"                 => SKColors.Gray,
            "honeydew"             => new SKColor(240,255,240),
            "hotpink"              => new SKColor(255,105,180),
            "indianred"            => new SKColor(205, 92, 92),
            "indigo"               => new SKColor( 75,  0,130),
            "ivory"                => new SKColor(255,255,240),
            "khaki"                => new SKColor(240,230,140),
            "lavender"             => new SKColor(230,230,250),
            "lavenderblush"        => new SKColor(255,240,245),
            "lawngreen"            => new SKColor(124,252,  0),
            "lemonchiffon"         => new SKColor(255,250,205),
            "lightblue"            => new SKColor(173,216,230),
            "lightcoral"           => new SKColor(240,128,128),
            "lightcyan"            => new SKColor(224,255,255),
            "lightgoldenrodyellow" => new SKColor(250,250,210),
            "lightgray"            => new SKColor(211,211,211),
            "lightgreen"           => new SKColor(144,238,144),
            "lightgrey"            => new SKColor(211,211,211),
            "lightpink"            => new SKColor(255,182,193),
            "lightsalmon"          => new SKColor(255,160,122),
            "lightseagreen"        => new SKColor( 32,178,170),
            "lightskyblue"         => new SKColor(135,206,250),
            "lightslategray"       => new SKColor(119,136,153),
            "lightslategrey"       => new SKColor(119,136,153),
            "lightsteelblue"       => new SKColor(176,196,222),
            "lightyellow"          => new SKColor(255,255,224),
            "lime"                 => new SKColor(  0,255,  0),
            "limegreen"            => new SKColor( 50,205, 50),
            "linen"                => new SKColor(250,240,230),
            "magenta"              => SKColors.Magenta,
            "maroon"               => new SKColor(128,  0,  0),
            "mediumaquamarine"     => new SKColor(102,205,170),
            "mediumblue"           => new SKColor(  0,  0,205),
            "mediumorchid"         => new SKColor(186, 85,211),
            "mediumpurple"         => new SKColor(147,112,219),
            "mediumseagreen"       => new SKColor( 60,179,113),
            "mediumslateblue"      => new SKColor(123,104,238),
            "mediumspringgreen"    => new SKColor(  0,250,154),
            "mediumturquoise"      => new SKColor( 72,209,204),
            "mediumvioletred"      => new SKColor(199, 21,133),
            "midnightblue"         => new SKColor( 25, 25,112),
            "mintcream"            => new SKColor(245,255,250),
            "mistyrose"            => new SKColor(255,228,225),
            "moccasin"             => new SKColor(255,228,181),
            "navajowhite"          => new SKColor(255,222,173),
            "navy"                 => new SKColor(  0,  0,128),
            "oldlace"              => new SKColor(253,245,230),
            "olive"                => new SKColor(128,128,  0),
            "olivedrab"            => new SKColor(107,142, 35),
            "orange"               => SKColors.Orange,
            "orangered"            => new SKColor(255, 69,  0),
            "orchid"               => new SKColor(218,112,214),
            "palegoldenrod"        => new SKColor(238,232,170),
            "palegreen"            => new SKColor(152,251,152),
            "paleturquoise"        => new SKColor(175,238,238),
            "palevioletred"        => new SKColor(219,112,147),
            "papayawhip"           => new SKColor(255,239,213),
            "peachpuff"            => new SKColor(255,218,185),
            "peru"                 => new SKColor(205,133, 63),
            "pink"                 => SKColors.Pink,
            "plum"                 => new SKColor(221,160,221),
            "powderblue"           => new SKColor(176,224,230),
            "purple"               => SKColors.Purple,
            "rebeccapurple"        => new SKColor(102, 51,153),
            "red"                  => SKColors.Red,
            "rosybrown"            => new SKColor(188,143,143),
            "royalblue"            => new SKColor( 65,105,225),
            "saddlebrown"          => new SKColor(139, 69, 19),
            "salmon"               => new SKColor(250,128,114),
            "sandybrown"           => new SKColor(244,164, 96),
            "seagreen"             => new SKColor( 46,139, 87),
            "seashell"             => new SKColor(255,245,238),
            "sienna"               => new SKColor(160, 82, 45),
            "silver"               => new SKColor(192,192,192),
            "skyblue"              => new SKColor(135,206,235),
            "slateblue"            => new SKColor(106, 90,205),
            "slategray"            => new SKColor(112,128,144),
            "slategrey"            => new SKColor(112,128,144),
            "snow"                 => new SKColor(255,250,250),
            "springgreen"          => new SKColor(  0,255,127),
            "steelblue"            => new SKColor( 70,130,180),
            "tan"                  => new SKColor(210,180,140),
            "teal"                 => new SKColor(  0,128,128),
            "thistle"              => new SKColor(216,191,216),
            "tomato"               => new SKColor(255, 99, 71),
            "turquoise"            => new SKColor( 64,224,208),
            "violet"               => new SKColor(238,130,238),
            "wheat"                => new SKColor(245,222,179),
            "white"                => SKColors.White,
            "whitesmoke"           => new SKColor(245,245,245),
            "yellow"               => SKColors.Yellow,
            "yellowgreen"          => new SKColor(154,205, 50),
            _                      => SKColors.Black,
        };
    }

    private static byte Component(string s)
    {
        s = s.Trim();
        if (s.EndsWith('%'))
        {
            float pct = SvgRenderer.ParseFloat(s[..^1]);
            return (byte)Math.Clamp(pct / 100f * 255f, 0f, 255f);
        }
        return byte.TryParse(s, out byte b) ? b : (byte)0;
    }

    private static SKColor HslToRgb(float h, float s, float l, float a)
    {
        float R, G, B;
        if (s == 0f) { R = G = B = l; }
        else
        {
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            R = Hue(p, q, h + 1f / 3f);
            G = Hue(p, q, h);
            B = Hue(p, q, h - 1f / 3f);
        }
        return new SKColor((byte)(R*255), (byte)(G*255), (byte)(B*255), (byte)(a*255));

        static float Hue(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f/6f) return p + (q-p)*6f*t;
            if (t < 1f/2f) return q;
            if (t < 2f/3f) return p + (q-p)*(2f/3f-t)*6f;
            return p;
        }
    }
}

internal static class SvgGradientParser
{
    public static SKShader? ParseGradient(XElement? el, bool isObjectBoundingBox)
    {
        if (el == null) return null;

        var colors    = new List<SKColor>();
        var positions = new List<float>();

        foreach (var stop in el.Descendants())
        {
            if (stop.Name.LocalName != "stop") continue;

            string? offStr = stop.Attribute("offset")?.Value?.Trim() ?? "0";
            float off = offStr.EndsWith('%')
                ? SvgRenderer.ParseFloat(offStr[..^1]) / 100f
                : SvgRenderer.ParseFloat(offStr);
            off = Math.Clamp(off, 0f, 1f);

            string? colorStr = null;
            string? styleStr = stop.Attribute("style")?.Value;
            if (!string.IsNullOrEmpty(styleStr))
            {
                var m = Regex.Match(styleStr, @"stop-color\s*:\s*([^;]+)");
                if (m.Success) colorStr = m.Groups[1].Value.Trim();
            }
            colorStr ??= stop.Attribute("stop-color")?.Value;

            float stopOpacity = 1f;
            string? opStr = null;
            if (!string.IsNullOrEmpty(styleStr))
            {
                var m = Regex.Match(styleStr, @"stop-opacity\s*:\s*([^;]+)");
                if (m.Success) opStr = m.Groups[1].Value.Trim();
            }
            opStr ??= stop.Attribute("stop-opacity")?.Value;
            if (!string.IsNullOrEmpty(opStr))
                stopOpacity = Math.Clamp(SvgRenderer.ParseFloat(opStr), 0f, 1f);

            SKColor c = SvgColorParser.Parse(colorStr ?? "black");
            if (stopOpacity < 1f)
                c = c.WithAlpha((byte)(c.Alpha * stopOpacity));

            colors.Add(c);
            positions.Add(off);
        }

        if (colors.Count == 0) return null;
        if (colors.Count == 1)
        { colors.Add(colors[0]); positions.Add(1f); }

        string? gtAttr = el.Attribute("gradientTransform")?.Value;
        var matrix = string.IsNullOrEmpty(gtAttr)
            ? SKMatrix.Identity
            : SvgTransformParser.Parse(gtAttr);

        var colorsArr    = colors.ToArray();
        var positionsArr = positions.ToArray();

        string localName = el.Name.LocalName;

        if (localName == "linearGradient")
        {
            float x1 = Float(el, "x1", 0f);
            float y1 = Float(el, "y1", 0f);
            float x2 = Float(el, "x2", isObjectBoundingBox ? 1f : 0f);
            float y2 = Float(el, "y2", 0f);

            return SKShader.CreateLinearGradient(
                new SKPoint(x1, y1), new SKPoint(x2, y2),
                colorsArr, positionsArr,
                SKShaderTileMode.Clamp,
                matrix);
        }

        if (localName == "radialGradient")
        {
            float cx = Float(el, "cx", isObjectBoundingBox ? 0.5f : 0f);
            float cy = Float(el, "cy", isObjectBoundingBox ? 0.5f : 0f);
            float r  = Float(el, "r",  isObjectBoundingBox ? 0.5f : 0f);
            float fx = Float(el, "fx", cx);
            float fy = Float(el, "fy", cy);

            return SKShader.CreateTwoPointConicalGradient(
                new SKPoint(fx, fy), 0f,
                new SKPoint(cx, cy), r,
                colorsArr, positionsArr,
                SKShaderTileMode.Clamp,
                matrix);
        }

        return null;
    }

    private static float Float(XElement el, string name, float def) =>
        SvgRenderer.ParseFloat(el.Attribute(name)?.Value, def);
}