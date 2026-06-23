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

    /// <summary>Begins loading <paramref name="url"/> if not already cached/in-flight. Fire-and-forget
    /// is fine; call <paramref name="onLoaded"/> when the image becomes available (or failed,
    /// in which case it may be called with <c>null</c>). May be invoked from any thread.</summary>
    void RequestLoad(string url, Action<string, SKImage?> onLoaded);
}

/// <summary>
/// Default <see cref="IMarkdownImageProvider"/>: downloads over HTTP(S) with an in-memory
/// decode cache. A single static <see cref="HttpClient"/> is shared across instances.
///
/// THREADING NOTE: <paramref name="onLoaded"/> callbacks fire on a background thread pool
/// thread (via <c>Task.Run</c>), not marshaled onto any particular UI thread -- because
/// <c>ElementBase.BeginInvoke</c> in this version of the framework executes synchronously
/// rather than marshaling across threads, there is no safe generic hand-off point to use
/// here. <see cref="MarkdownViewer"/> only flips a couple of plain fields and calls
/// <c>Invalidate()</c> from this callback, which appears safe given how <c>Invalidate()</c>
/// is implemented (simple dirty-flag sets), but if your render loop assumes single-threaded
/// access you should marshal <paramref name="onLoaded"/> onto your UI thread/dispatcher
/// before it reaches MarkdownViewer (e.g. wrap this provider and post through your own
/// scheduler).
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

    private readonly ConcurrentDictionary<string, SKImage> _cache = new();
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public SKImage? TryGetCached(string url) =>
        !string.IsNullOrEmpty(url) && _cache.TryGetValue(url, out var img) ? img : null;

    public void RequestLoad(string url, Action<string, SKImage?> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (_cache.ContainsKey(url)) { onLoaded(url, _cache[url]); return; }
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
                    byte[] bytes = await SharedClient.GetByteArrayAsync(url, cts.Token).ConfigureAwait(false);
                    using var data = SKData.CreateCopy(bytes);
                    decoded = SKImage.FromEncodedData(data);
                }
            }
            catch
            {
                decoded = null;
            }
            finally
            {
                _inFlight.TryRemove(url, out _);
            }

            if (decoded != null) _cache[url] = decoded;
            onLoaded(url, decoded);
        });
    }

    private static SKImage? DecodeDataUri(string uri)
    {
        int comma = uri.IndexOf(',');
        if (comma < 0) return null;
        string meta = uri[5..comma];
        string payload = uri[(comma + 1)..];
        if (!meta.Contains("base64", StringComparison.OrdinalIgnoreCase)) return null;
        byte[] bytes = Convert.FromBase64String(payload);
        using var data = SKData.CreateCopy(bytes);
        return SKImage.FromEncodedData(data);
    }
}
