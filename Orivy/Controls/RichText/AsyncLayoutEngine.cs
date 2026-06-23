// SPDX-License-Identifier: MIT
// Orivy RichText — AsyncLayoutEngine
//
// Background-thread layout engine for large documents.
//
// PROBLEM: For documents >10K lines, the first layout pass (BuildTextLayout
// in the base TextBox) can take 100-500ms on the UI thread, freezing the app.
//
// SOLUTION: Run layout on a background Task. The engine:
//   1. Cancellation-aware — re-kicks on every text change.
//   2. Incremental — yields results in chunks of ~100 lines.
//   3. UI-thread marshaling — uses SynchronizationContext.
//   4. Graceful fallback — for small documents (< 1000 lines) runs synchronously.
//
// USAGE:
//   _layoutEngine = new AsyncLayoutEngine(this);
//   _layoutEngine.ScheduleLayout(text, runs, viewport);
//   _layoutEngine.OnLayoutChunk += (sender, e) => {
//       // e.Lines contains a chunk of TextLineLayout.
//       // Append to _lines and Invalidate().
//   };
//
// THREADING MODEL:
//   - The engine uses a single background Task; concurrent ScheduleLayout
//     calls cancel the in-flight work and start fresh.
//   - The background thread reads text and runs (immutable snapshots) —
//     no shared mutable state with the UI thread.
//   - Results are posted to the captured SynchronizationContext (UI thread).
//
// CAVEATS:
//   - SKFont is NOT thread-safe across instances. The background thread
//     creates its OWN FontCache + RunAwareMeasurer. The UI thread's caches
//     remain untouched.
//   - This means the first async layout duplicates the font cache (~100KB).
//   - Subsequent layouts reuse the background FontCache (cached typefaces).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace Orivy.Controls.RichText;

/// <summary>Event args for incremental layout completion.</summary>
public sealed class LayoutChunkEventArgs : EventArgs
{
    public LayoutChunkEventArgs(int startLine, int lineCount, IReadOnlyList<MeasuredLine> lines, long layoutVersion)
    {
        StartLine = startLine;
        LineCount = lineCount;
        Lines = lines;
        LayoutVersion = layoutVersion;
    }

    /// <summary>Index of the first line in this chunk.</summary>
    public int StartLine { get; }

    /// <summary>Number of lines in this chunk.</summary>
    public int LineCount { get; }

    /// <summary>Measured lines for this chunk.</summary>
    public IReadOnlyList<MeasuredLine> Lines { get; }

    /// <summary>Version counter — increments on each ScheduleLayout call.
    /// Receivers should ignore chunks from stale versions.</summary>
    public long LayoutVersion { get; }
}

/// <summary>Background-thread layout engine for large documents.
/// Owned by RichTextBox. Disposes its background FontCache on shutdown.</summary>
public sealed class AsyncLayoutEngine : IDisposable
{
    private readonly RunAwareMeasurer _bgMeasurer;
    private readonly SynchronizationContext _syncContext;
    private readonly int _chunkSize;

    // The pending layout request. Setting a new one cancels the previous.
    private CancellationTokenSource? _cts;
    private long _layoutVersion;

    // Snapshot of inputs (immutable; safe to read from background thread).
    private string? _pendingText;
    private IReadOnlyList<TextRun>? _pendingRuns;
    private TextStyle _pendingBaseStyle;
    private SKFont? _pendingBaseFont;
    private float _pendingViewportWidth;
    private List<(int start, int length)>? _pendingLines;  // line ranges to measure

    private bool _disposed;

    /// <summary>Fires on the UI thread when a chunk of lines is ready.
    /// Receivers MUST check e.LayoutVersion against the current expected
    /// version before applying changes (stale chunks should be ignored).</summary>
    public event EventHandler<LayoutChunkEventArgs>? OnLayoutChunk;

    /// <summary>Fires on the UI thread when the full layout pass is complete.</summary>
    public event EventHandler<long>? OnLayoutComplete;

    /// <summary>Threshold below which the engine runs synchronously on the
    /// calling thread (no background dispatch). Set to ~1000 lines.</summary>
    public const int SyncThresholdLines = 1000;

    public AsyncLayoutEngine(FontCache sharedFontCacheForMetrics, int chunkSize = 100)
    {
        // Create a SEPARATE FontCache for the background thread — SKFont is
        // not thread-safe across instances. We share the typeface cache
        // (typefaces ARE thread-safe in SkiaSharp) by reusing the same
        // family/size setup, but font instances are isolated.
        //
        // For simplicity we duplicate the cache config. If the shared cache
        // has the same BaseFamily/MonoFamily/BaseSize/ScaleFactor, the
        // typeface lookup will hit SkiaSharp's internal cache (cheap).
        var bgFontCache = new FontCache(
            sharedFontCacheForMetrics.BaseFamily,
            sharedFontCacheForMetrics.MonoFamily,
            sharedFontCacheForMetrics.BaseSize)
        {
            ScaleFactor = sharedFontCacheForMetrics.ScaleFactor
        };

        _bgMeasurer = new RunAwareMeasurer(bgFontCache);
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
        _chunkSize = chunkSize;
    }

    /// <summary>Schedule a layout pass. Cancels any in-flight pass.</summary>
    /// <param name="text">Full text snapshot.</param>
    /// <param name="runs">Run list snapshot.</param>
    /// <param name="lineRanges">List of (start, length) tuples per line.</param>
    /// <param name="baseStyle">Base style to inherit.</param>
    /// <param name="baseFont">Base font.</param>
    /// <param name="viewportWidth">Viewport width for wrapping.</param>
    public void ScheduleLayout(
        string text,
        IReadOnlyList<TextRun> runs,
        List<(int start, int length)> lineRanges,
        TextStyle baseStyle,
        SKFont baseFont,
        float viewportWidth)
    {
        if (_disposed)
            return;

        // Cancel any in-flight work.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var version = Interlocked.Increment(ref _layoutVersion);

        // Snapshot inputs (callers should pass immutable snapshots; we
        // also defensively copy the line ranges list).
        _pendingText = text;
        _pendingRuns = runs;
        _pendingLines = new List<(int, int)>(lineRanges);
        _pendingBaseStyle = baseStyle;
        _pendingBaseFont = baseFont;
        _pendingViewportWidth = viewportWidth;

        // Small documents → run synchronously on the UI thread.
        if (lineRanges.Count < SyncThresholdLines)
        {
            RunLayoutSync(token, version);
            return;
        }

        // Large documents → dispatch to background.
        Task.Run(() => RunLayoutAsync(token, version), token);
    }

    private void RunLayoutSync(CancellationToken token, long version)
    {
        var text = _pendingText!;
        var runs = _pendingRuns!;
        var lines = _pendingLines!;
        var baseStyle = _pendingBaseStyle;
        var baseFont = _pendingBaseFont!;
        var viewportWidth = _pendingViewportWidth;

        for (var i = 0; i < lines.Count; i += _chunkSize)
        {
            if (token.IsCancellationRequested)
                return;

            var chunkEnd = Math.Min(i + _chunkSize, lines.Count);
            var chunkLines = new List<MeasuredLine>(chunkEnd - i);
            for (var j = i; j < chunkEnd; j++)
            {
                var (start, length) = lines[j];
                var measured = _bgMeasurer.MeasureLine(
                    text, start, length, runs, baseStyle, baseFont,
                    cache: null, lineIndex: j);
                chunkLines.Add(measured);
            }

            OnLayoutChunk?.Invoke(this, new LayoutChunkEventArgs(i, chunkLines.Count, chunkLines, version));
        }

        OnLayoutComplete?.Invoke(this, version);
    }

    private void RunLayoutAsync(CancellationToken token, long version)
    {
        try
        {
            var text = _pendingText!;
            var runs = _pendingRuns!;
            var lines = _pendingLines!;
            var baseStyle = _pendingBaseStyle;
            var baseFont = _pendingBaseFont!;
            var viewportWidth = _pendingViewportWidth;

            for (var i = 0; i < lines.Count; i += _chunkSize)
            {
                if (token.IsCancellationRequested)
                    return;

                var chunkEnd = Math.Min(i + _chunkSize, lines.Count);
                var chunkLines = new List<MeasuredLine>(chunkEnd - i);
                for (var j = i; j < chunkEnd; j++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var (start, length) = lines[j];
                    var measured = _bgMeasurer.MeasureLine(
                        text, start, length, runs, baseStyle, baseFont,
                        cache: null, lineIndex: j);
                    chunkLines.Add(measured);
                }

                // Post chunk to UI thread.
                var chunkIndex = i;
                var chunkCount = chunkLines.Count;
                var chunkCopy = chunkLines;  // already a fresh list
                _syncContext.Post(_ =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    OnLayoutChunk?.Invoke(this, new LayoutChunkEventArgs(chunkIndex, chunkCount, chunkCopy, version));
                }, null);
            }

            // Post completion.
            _syncContext.Post(_ =>
            {
                if (!token.IsCancellationRequested)
                    OnLayoutComplete?.Invoke(this, version);
            }, null);
        }
        catch (OperationCanceledException)
        {
            // Expected — silently ignore.
        }
        catch (Exception ex)
        {
            // Post exception to UI thread for diagnostics.
            _syncContext.Post(_ =>
            {
                System.Diagnostics.Debug.WriteLine($"AsyncLayoutEngine error: {ex}");
            }, null);
        }
    }

    /// <summary>Cancel any in-flight layout pass. Does not dispose the engine.</summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>Current layout version. Incremented on each ScheduleLayout.</summary>
    public long CurrentVersion => Interlocked.Read(ref _layoutVersion);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _bgMeasurer.Dispose();
    }
}
