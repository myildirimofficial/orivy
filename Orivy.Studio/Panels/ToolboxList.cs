using Orivy;
using Orivy.Controls;
using Orivy.Helpers;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// A polished, owner-drawn toolbox list. It is intentionally NOT a GridList: it draws category
/// headers and entry rows itself (glyph badge + name, hover/selection states) and — crucially —
/// overrides the mouse handlers directly, so drag-to-canvas works reliably (GridList does not raise
/// the public MouseMove event, and WM_MOUSEMOVE reports no button, which broke event-based DnD).
/// </summary>
public sealed class ToolboxList : Element
{
    private const float HeaderHeight = 22f;
    private const float RowHeight = 32f;
    private const float DragThreshold = 5f;

    private abstract record Row(float Height);
    private sealed record HeaderRow(string Category) : Row(HeaderHeight);
    private sealed record EntryRow(ControlEntry Entry) : Row(RowHeight);

    private readonly List<Row> _rows = new();
    private IReadOnlyList<ControlEntry> _source = Array.Empty<ControlEntry>();
    private float _scroll;
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;

    private bool _armed;
    private ControlEntry? _armedEntry;
    private SKPoint _armedStart;

    private readonly SKPaint _fill = new() { IsAntialias = true };
    private readonly SKPaint _text = new() { IsAntialias = true };
    private readonly SKFont _font = Application.DefaultFont;
    private readonly float _baseFontSize;

    public ToolboxList()
    {
        Border = new Thickness(1);
        Radius = new Radius(12);
        Padding = new Thickness(0);
        _baseFontSize = _font.Size;

        // Reactive tint (re-evaluated on every theme change) instead of a frozen BackColor snapshot.
        ConfigureVisualStyles(styles => styles.Base(b => b
            .Background(ColorScheme.Surface)
            .BorderColor(ColorScheme.Outline.WithAlpha(60))));
    }

    /// <summary>
    /// This list manages its own scroll offset (<see cref="_scroll"/>) instead of the built-in
    /// scrollbar-backed AutoScroll path, so it must opt in explicitly — otherwise
    /// <c>ElementBase</c>'s wheel routing never considers it a wheel target at all.
    /// </summary>
    protected override bool HandlesMouseWheelInput => true;

    public event Action<ControlEntry>? PlaceRequested;
    public event Action<ControlEntry, SKPoint>? DragStarted;

    public ControlEntry? SelectedEntry =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count && _rows[_selectedIndex] is EntryRow e ? e.Entry : null;

    public void SetEntries(IReadOnlyList<ControlEntry> entries)
    {
        _source = entries;
        Rebuild(null);
    }

    public void Filter(string? query)
    {
        Rebuild(string.IsNullOrWhiteSpace(query) ? null : query.Trim());
    }

    private void Rebuild(string? filter)
    {
        _rows.Clear();
        _selectedIndex = -1;

        var matches = _source.Where(e => filter == null ||
            e.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            e.Category.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var group in matches.GroupBy(e => e.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            _rows.Add(new HeaderRow(group.Key));
            foreach (var entry in group.OrderBy(e => e.DisplayName, StringComparer.Ordinal))
                _rows.Add(new EntryRow(entry));
        }

        _scroll = 0f;
        Invalidate();
    }

    private float ContentHeight => _rows.Sum(r => r.Height) + 8f;
    private float Viewport => Height - 2f;
    private float MaxScroll => Math.Max(0f, ContentHeight - Viewport);

    // ── Painting ──

    public override void OnPaint(SKCanvas canvas)
    {
        base.OnPaint(canvas);

        var saved = canvas.Save();
        canvas.ClipRect(new SKRect(1f, 1f, Width - 1f, Height - 1f));

        var y = 4f - _scroll;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var rowRect = new SKRect(6f, y, Width - 6f, y + row.Height);
            if (rowRect.Bottom >= 0 && rowRect.Top <= Height)
                PaintRow(canvas, i, row, rowRect);
            y += row.Height;
        }

        canvas.RestoreToCount(saved);

        // Scrollbar hint.
        if (MaxScroll > 0f)
        {
            var trackH = Viewport;
            var thumbH = Math.Max(28f, trackH * (Viewport / ContentHeight));
            var thumbY = (trackH - thumbH) * (_scroll / MaxScroll);
            _fill.Color = ColorScheme.Outline.WithAlpha(90);
            canvas.DrawRoundRect(new SKRect(Width - 5f, thumbY + 1f, Width - 2f, thumbY + thumbH), 2f, 2f, _fill);
        }
    }

    // Every other control renders text via CreateRenderFont, which converts the nominal point size
    // to actual pixels through the framework's Topx() (× 1.333 × DPI ScaleFactor) — this custom-drawn
    // list never went through that path, so its text rendered ~25% smaller than everywhere else in
    // the app for the exact same nominal Font.Size. Applying Topx() here matches it for real instead
    // of just nudging the point size up.
    private float Px(float pointSize) => pointSize.Topx(this);

    private void PaintRow(SKCanvas canvas, int index, Row row, SKRect rect)
    {
        if (row is HeaderRow header)
        {
            _font.Size = Px(_baseFontSize - 1f);
            _text.Color = ColorScheme.ForeColor.WithAlpha(130);
            TextRenderer.DrawText(canvas, header.Category.ToUpperInvariant(),
                rect.Left + 8f, rect.MidY + _font.Size * 0.35f, _font, _text);
            _font.Size = Px(_baseFontSize);
            return;
        }

        var entry = ((EntryRow)row).Entry;
        var selected = index == _selectedIndex;
        var hovered = index == _hoverIndex;

        if (selected)
        {
            _fill.Color = ColorScheme.Primary.WithAlpha(38);
            canvas.DrawRoundRect(rect, 8f, 8f, _fill);
            _fill.Color = ColorScheme.Primary;
            canvas.DrawRoundRect(new SKRect(rect.Left, rect.MidY - 8f, rect.Left + 3f, rect.MidY + 8f), 2f, 2f, _fill);
        }
        else if (hovered)
        {
            _fill.Color = ColorScheme.ForeColor.WithAlpha(14);
            canvas.DrawRoundRect(rect, 8f, 8f, _fill);
        }

        // Glyph badge.
        var badge = new SKRect(rect.Left + 8f, rect.MidY - 11f, rect.Left + 30f, rect.MidY + 11f);
        _fill.Color = CategoryColor(entry.Category).WithAlpha((byte)(selected ? 235 : 210));
        canvas.DrawRoundRect(badge, 6f, 6f, _fill);

        _font.Size = Px(_baseFontSize - 1f);
        _text.Color = SKColors.White;
        var glyph = entry.DisplayName[..1].ToUpperInvariant();
        var gw = _font.MeasureText(glyph);
        TextRenderer.DrawText(canvas, glyph, badge.MidX - gw / 2f, badge.MidY + _font.Size * 0.35f, _font, _text);
        _font.Size = Px(_baseFontSize);

        // Name — a touch larger than the base UI font so entries read clearly in a scannable list.
        _font.Size = Px(_baseFontSize + 1f);
        _text.Color = selected ? ColorScheme.Primary : ColorScheme.ForeColor;
        TextRenderer.DrawText(canvas, entry.DisplayName, badge.Right + 10f, rect.MidY + _font.Size * 0.35f, _font, _text);
        _font.Size = Px(_baseFontSize);
    }

    private static SKColor CategoryColor(string category) => category switch
    {
        "Buttons" => new SKColor(59, 130, 246),
        "Inputs" => new SKColor(139, 92, 246),
        "Data" => new SKColor(16, 185, 129),
        "Layout" => new SKColor(245, 158, 11),
        "Display" => new SKColor(236, 72, 153),
        _ => new SKColor(100, 116, 139),
    };

    // ── Input ──

    public override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        var index = RowAt(e.Location.Y);
        if (index >= 0 && _rows[index] is EntryRow entry)
        {
            _selectedIndex = index;
            _armed = true;
            _armedEntry = entry.Entry;
            _armedStart = PointToScreen(e.Location);
            GetParentWindow()?.SetMouseCapture(this);
            Invalidate();
        }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_armed && _armedEntry != null)
        {
            var screen = PointToScreen(e.Location);
            if (Math.Abs(screen.X - _armedStart.X) + Math.Abs(screen.Y - _armedStart.Y) >= DragThreshold)
            {
                var entry = _armedEntry;
                Disarm();
                GetParentWindow()?.ReleaseMouseCapture(this);
                DragStarted?.Invoke(entry, screen);
            }
            return;
        }

        var index = RowAt(e.Location.Y);
        var hover = index >= 0 && _rows[index] is EntryRow ? index : -1;
        if (hover != _hoverIndex)
        {
            _hoverIndex = hover;
            Invalidate();
        }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_armed)
        {
            Disarm();
            GetParentWindow()?.ReleaseMouseCapture(this);
        }
    }

    public override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var index = RowAt(e.Location.Y);
        if (index >= 0 && _rows[index] is EntryRow entry)
            PlaceRequested?.Invoke(entry.Entry);
    }

    public override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    public override void OnMouseWheel(MouseEventArgs e)
    {
        var next = Math.Clamp(_scroll - e.Delta, 0f, MaxScroll);
        if (Math.Abs(next - _scroll) > 0.01f)
        {
            _scroll = next;
            Invalidate();
        }
        e.Handled = true;
    }

    private int RowAt(float localY)
    {
        var y = 4f - _scroll;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (localY >= y && localY < y + _rows[i].Height)
                return i;
            y += _rows[i].Height;
        }

        return -1;
    }

    private void Disarm()
    {
        _armed = false;
        _armedEntry = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fill.Dispose();
            _text.Dispose();
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
