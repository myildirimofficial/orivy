using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace Orivy.Controls.Markdown;

/// <summary>
/// Resolves markdown image URLs (<c>![alt](url)</c>) into decoded <see cref="SKImage"/>s.
/// Implement this yourself to load from app resources, an authenticated endpoint, an asset
/// bundle, etc. -- <see cref="MarkdownViewer.ImageProvider"/> accepts any implementation.
/// </summary>
public interface IMarkdownImageProvider
{
    /// <summary>Returns a cached decoded image if already available, without starting a load.</summary>
    SKImage? TryGetCached(string url);

    /// <summary>Begins loading <paramref name="url"/> if not already cached/in-flight.
    /// Call <paramref name="onLoaded"/> when the image becomes available (or on failure, with null).
    /// May be invoked from any thread.</summary>
    void RequestLoad(string url, Action<string, SKImage?> onLoaded);
}

/// <summary>
/// Default <see cref="IMarkdownImageProvider"/>: downloads over HTTP(S) with an in-memory
/// decode cache.
///
/// Supported formats: JPEG, PNG, GIF, BMP, ICO, WebP, WBMP (via SkiaSharp built-in),
/// and SVG (via SkiaSharp.Extended.Svg when that assembly is present at runtime — no hard
/// dependency; falls back to a null/placeholder when the assembly is absent).
///
/// data: URIs are also decoded inline (base64 encoded PNG/JPEG/etc and SVG+xml).
///
/// THREADING: <paramref name="onLoaded"/> fires on a thread-pool thread. MarkdownViewer
/// only sets a dirty flag and calls Invalidate() from the callback which is safe, but if
/// your render loop is strictly single-threaded, wrap this provider and marshal through
/// your own dispatcher.
/// </summary>
public sealed class HttpMarkdownImageProvider : IMarkdownImageProvider
{
    private static readonly HttpClient SharedClient = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Orivy-MarkdownViewer/1.0");
        return client;
    }

    private readonly ConcurrentDictionary<string, SKImage>  _cache    = new();
    private readonly ConcurrentDictionary<string, byte>     _inFlight = new();

    public SKImage? TryGetCached(string url) =>
        !string.IsNullOrEmpty(url) && _cache.TryGetValue(url, out var img) ? img : null;

    public void RequestLoad(string url, Action<string, SKImage?> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (_cache.TryGetValue(url, out var hit)) { onLoaded(url, hit); return; }
        if (!_inFlight.TryAdd(url, 0)) return;

        _ = Task.Run(async () =>
        {
            SKImage? decoded = null;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    decoded = DecodeDataUri(url);
                }
                else
                {
                    byte[] bytes = await SharedClient
                        .GetByteArrayAsync(url, cts.Token)
                        .ConfigureAwait(false);
                    decoded = DecodeBytes(bytes, url);
                }
            }
            catch { decoded = null; }
            finally { _inFlight.TryRemove(url, out _); }

            if (decoded != null) _cache[url] = decoded;
            onLoaded(url, decoded);
        });
    }

    // ----------------------------------------------------------------
    // Decoding
    // ----------------------------------------------------------------

    /// <summary>
    /// Decode raw bytes to an SKImage.
    /// Tries SVG first (when the URL ends with .svg or bytes look like SVG),
    /// then falls back to SkiaSharp's built-in codec (PNG/JPEG/GIF/BMP/WebP/ICO/WBMP).
    /// </summary>
    private static SKImage? DecodeBytes(byte[] bytes, string urlHint)
    {
        bool looksLikeSvg = IsSvgBytes(bytes) ||
                            urlHint.EndsWith(".svg",  StringComparison.OrdinalIgnoreCase) ||
                            urlHint.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);

        if (looksLikeSvg)
        {
            var svgImg = TryDecodeSvg(bytes, urlHint);
            if (svgImg != null) return svgImg;
        }

        // Standard raster formats (JPEG, PNG, GIF, BMP, WebP, ICO, WBMP)
        using var data = SKData.CreateCopy(bytes);
        return SKImage.FromEncodedData(data);
    }

    /// <summary>Quick check: first non-whitespace bytes are &lt;? or &lt;s or PK (gzip svg)</summary>
    private static bool IsSvgBytes(byte[] b)
    {
        int i = 0;
        while (i < b.Length && b[i] <= 0x20) i++;
        if (i + 4 >= b.Length) return false;
        // Plain SVG starts with '<' ('<?xml' or '<svg')
        if (b[i] == (byte)'<') return true;
        // Gzip magic bytes 1F 8B (svgz)
        if (b[i] == 0x1F && i + 1 < b.Length && b[i + 1] == 0x8B) return true;
        return false;
    }

    // ----------------------------------------------------------------
    // SVG via AdvancedSvgRenderer (built-in, no external dependency)
    // ----------------------------------------------------------------

    private static SKImage? TryDecodeSvg(byte[] bytes, string urlHint) =>
        // targetScale must stay 1x here: the markdown layout treats the decoded SKImage's pixel
        // dimensions as the SVG's "natural" size (see MarkdownLayoutBuilder.MeasureImageAtom).
        // The previous default of 2x (meant for crisp supersampling) leaked into that natural-size
        // calculation, making every SVG display at exactly twice its intended/declared size.
        SvgRenderer.Render(bytes, targetScale: 1f);

    // ----------------------------------------------------------------
    // data: URI decoding
    // ----------------------------------------------------------------

    private static SKImage? DecodeDataUri(string uri)
    {
        // data:[<mediatype>][;base64],<data>
        int comma = uri.IndexOf(',');
        if (comma < 0) return null;

        string meta    = uri[5..comma];           // skip "data:"
        string payload = uri[(comma + 1)..];

        bool isBase64 = meta.Contains("base64", StringComparison.OrdinalIgnoreCase);
        bool isSvg    = meta.Contains("svg",    StringComparison.OrdinalIgnoreCase);

        byte[] bytes;
        if (isBase64)
        {
            try { bytes = Convert.FromBase64String(payload); }
            catch { return null; }
        }
        else if (isSvg)
        {
            // URL-encoded or plain SVG text
            string decoded = Uri.UnescapeDataString(payload);
            bytes = System.Text.Encoding.UTF8.GetBytes(decoded);
        }
        else
        {
            return null;
        }

        if (isSvg)
            return TryDecodeSvg(bytes, "data:image/svg+xml") ?? null;

        using var data = SKData.CreateCopy(bytes);
        return SKImage.FromEncodedData(data);
    }
}
