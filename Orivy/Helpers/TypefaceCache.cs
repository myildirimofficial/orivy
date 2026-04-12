using SkiaSharp;
using System;
using System.Collections.Concurrent;

namespace Orivy.Helpers;

internal sealed class TypefaceCache : IDisposable
{
    private readonly ConcurrentDictionary<int, SKTypeface> _cache = new();
    private bool _disposed;

    public SKTypeface GetOrAdd(int codepoint, Func<SKTypeface> factory)
    {
        if (_cache.TryGetValue(codepoint, out var cached))
            return cached;

        var created = factory();
        if (_cache.TryAdd(codepoint, created))
            return created;

        if (_cache.TryGetValue(codepoint, out cached))
        {
            // SKTypeface instances can remain referenced by live SKFont objects after
            // being handed out by the cache. Disposing an evicted/shared typeface can
            // invalidate those fonts inside Skia and lead to native access violations.
            if (!ReferenceEquals(created, cached) && !ReferenceEquals(created, SKTypeface.Default))
                created.Dispose();

            return cached;
        }

        return created;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cache.Clear();
        _disposed = true;
    }
}
