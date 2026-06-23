# Orivy RichText — Integration Guide

Bu doküman, RichTextBox'ı mevcut `Orivy.Controls.TextBox`'a entegre etmek için
yapmanız gereken **minimum** değişiklikleri listeler.

## v5.2 Kritik Düzeltmeler

Bu sürümde 4 gerçek sorun düzeltildi:

1. **`GetGlyphs` void API** — SkiaSharp 3.119'da `SKFont.GetGlyphs(span, span)`
   void döner. Eski kod `var glyphCount = ...GetGlyphs(...)` derlenmiyordu.
   Düzeltme: `CountGlyphs` ile sayı al, sonra void overload ile doldur.

2. **Internal tipler public yapıldı** — `TextBlobBatcher`, `RichTextLayoutPipeline`,
   `LineLayoutCache`, `AsyncLayoutEngine`, `MeasuredLine`, `RichTextLineLayout`,
   `RtfTokenReader` hepsi `internal` → `public` yapıldı. Farklı assembly'den
   erişim problemi giderildi.

3. **Override'lar internal** — `protected internal override` → `internal override`.
   Base class `internal override` kullandığı için biz de onunla eşleştik.

4. **Scroll artık çalışıyor** — `DrawRichTextContent` artık `canvas.Translate`
   ile scroll offset uyguluyor. Mevcut TextBox.OnPaint ile aynı pattern:
   ```csharp
   canvas.ClipRect(viewport);
   canvas.Translate(viewport.Left - scrollX, viewport.Top - scrollY);
   ```

## ⚠️ SCROLL ÇALIŞMASI İÇİN KRİTİK (v5.2)

`DrawRichTextContent` artık scroll offset'i uyguluyor, ANCAK bu offset'leri
`GetVerticalScrollSafe()` ve `GetHorizontalScrollSafe()` metotlarından alıyor.
Bu metotlar şu an **0 döner** çünkü base class'taki `_vScrollBar`/`_hScrollBar`
field'ları private.

**Yapmanız gereken (3 seçenekten biri):**

### Seçenek A: `_vScrollBar` ve `_hScrollBar` field'larını expose et

```csharp
// TextBox.cs içinde:
// MEVCUT (private):
private ScrollBar? _vScrollBar;
private ScrollBar? _hScrollBar;

// DEĞİŞTİR:
protected internal ScrollBar? _vScrollBar;
protected internal ScrollBar? _hScrollBar;
```

Sonra RichTextBox.cs içindeki `GetVerticalScrollSafe` ve `GetHorizontalScrollSafe`'i
aç:

```csharp
// RichTextBox.cs — şu satırları uncomment et:
private float GetVerticalScrollSafe()
{
    return _vScrollBar?.Visible == true ? _vScrollBar.DisplayValue : 0f;
}

private float GetHorizontalScrollSafe()
{
    return _hScrollBar?.Visible == true ? _hScrollBar.DisplayValue : 0f;
}
```

### Seçenek B: `GetVerticalScrollOffset` / `GetHorizontalScrollOffset` expose et

```csharp
// TextBox.cs içinde:
// MEVCUT (private):
private float GetVerticalScrollOffset() { ... }
private float GetHorizontalScrollOffset() { ... }

// DEĞİŞTİR:
protected internal float GetVerticalScrollOffset() { ... }
protected internal float GetHorizontalScrollOffset() { ... }
```

Sonra RichTextBox.cs içinde:

```csharp
private float GetVerticalScrollSafe() => base.GetVerticalScrollOffset();
private float GetHorizontalScrollSafe() => base.GetHorizontalScrollOffset();
```

### Seçenek C: `GetTextViewport` expose et

Viewport doğru hesaplanmazsa (scrollbar inset'leri dahil değilse), scroll
çalışır ama metin scrollbar altında kalabilir:

```csharp
// TextBox.cs içinde:
protected internal SKRect GetTextViewport() { ... }
```

Sonra RichTextBox.cs içinde:

```csharp
private SKRect GetTextViewportSafe() => base.GetTextViewport();
```

**Önerilen**: Seçenek A (en az kod değişikliği, en doğrudan erişim).

## Mevcut TextBox'ta Yapılacak Diğer Değişiklikler

### 1. Alanlar (Fields)

```csharp
// Mevcut (private) → protected internal:
protected internal readonly List<TextLineLayout> _lines = new();
protected internal float _lineHeight;
protected internal float _baselineOffset;
protected internal float _contentWidth;
protected internal float _contentHeight;
protected internal bool _layoutDirty = true;
protected internal string _placeholderText = string.Empty;
protected internal bool _multiline;
protected internal TextWrap _wrapMode = TextWrap.WordWrap;
protected internal ScrollBar? _vScrollBar;  // scroll için KRİTİK
protected internal ScrollBar? _hScrollBar;  // scroll için KRİTİK
```

### 2. Paint nesneleri

```csharp
// protected internal yap:
protected internal readonly SKPaint _textPaint = ...;
protected internal readonly SKPaint _placeholderPaint = ...;
protected internal readonly SKPaint _selectionPaint = ...;
protected internal readonly SKPaint _caretPaint = ...;
protected internal readonly SKPaint _caretFillPaint = ...;
```

### 3. Layout metotları (opsiyonel — v5.2 pipeline bunları kullanmıyor)

Eğer RichTextBox'ın kendi pipeline'ını kullanmasını istiyorsanız (önerilen),
bu metotları expose etmenize gerek yok. Ama expose ederseniz RichTextBox
onları da kullanabilir:

```csharp
protected internal virtual void EnsureTextLayout() { ... }
protected internal virtual void BuildTextLayout(float viewportWidth) { ... }
// ... diğerleri
```

### 4. Yeni Hook: ShouldDrawTextContent (KRİTİK)

Bu hook olmadan, hem base class hem RichTextBox text çizer — overlap.

```csharp
// TextBox.cs'e ekle:
protected virtual bool ShouldDrawTextContent => true;
```

`OnPaint` içinde, text çiziminden önce kontrol et:

```csharp
public override void OnPaint(SKCanvas canvas)
{
    base.OnPaint(canvas);  // ElementBase — background, border
    EnsureTextLayout();
    var viewport = GetTextViewport();
    if (viewport.Width <= 0f || viewport.Height <= 0f) return;
    UpdatePaintResources();
    var scrollX = GetHorizontalScrollOffset();
    var scrollY = GetVerticalScrollOffset();
    var saveCount = canvas.Save();
    canvas.ClipRect(viewport);
    canvas.Translate(viewport.Left - scrollX, viewport.Top - scrollY);
    if (ShouldDrawTextContent)  // ← BU KONTROL EKLE
    {
        DrawSelection(canvas);
        DrawTextContent(canvas);
        DrawCaret(canvas);
    }
    canvas.RestoreToCount(saveCount);
}
```

RichTextBox'ta override et:

```csharp
// RichTextBox.cs:
protected override bool ShouldDrawTextContent => _mode == RichTextMode.Plain && !HasMultipleCursors;
```

### 5. Placeholder Fix (multiline için)

Mevcut `TextBox.DrawTextContent`'te placeholder çizimi düzelt:

```csharp
// MEVCUT (multiline'da yanlış):
if (text.Length == 0)
{
    if (!string.IsNullOrEmpty(_placeholderText))
        DrawLineText(canvas, _placeholderText, 0f, _baselineOffset, _placeholderPaint);
    return;
}

// DÜZELTILMIŞ:
if (text.Length == 0)
{
    if (!string.IsNullOrEmpty(_placeholderText))
    {
        var viewport = GetTextViewport();
        var topInset = GetContentTopInset(viewport);
        DrawLineText(canvas, _placeholderText, 0f, topInset + _baselineOffset, _placeholderPaint);
    }
    return;
}
```

## Derleme Checklist

- [ ] Tüm `private` → `protected internal` değişiklikleri yapıldı
- [ ] **`_vScrollBar` ve `_hScrollBar` protected internal** (scroll için KRİTİK)
- [ ] `ShouldDrawTextContent` virtual hook eklendi
- [ ] `OnPaint` içinde `if (ShouldDrawTextContent)` kontrolü eklendi
- [ ] Placeholder çizimine `topInset` eklendi
- [ ] RichTextBox.cs içindeki `GetVerticalScrollSafe` / `GetHorizontalScrollSafe`
      uncomment edildi (Seçenek A) veya alternatif dolduruldu (Seçenek B/C)
- [ ] Solution derleniyor
- [ ] Plain mode regression test — mevcut davranış birebir aynı
- [ ] **Scroll test**: multiline modda scrollbar kaydır → metin kaymalı
- [ ] MarkdownSource mode'da `**bold**` yazınca bold render görünüyor
- [ ] Multiline mode'da text boşken placeholder görünüyor

## Çalışma Testi

```csharp
// Test 1: Plain mode + scroll
var rtb = new RichTextBox {
    Text = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line {i}")),
    Multiline = true,
    Size = new SKSize(300, 200)
};
// Beklenen: scrollbar görünür, kaydırınca metin kayar

// Test 2: MarkdownSource mode
rtb.Mode = RichTextMode.MarkdownSource;
rtb.Text = "**bold** text";
// Beklenen: "bold" kelimesi bold, "text" normal

// Test 3: Multiline placeholder
rtb.Text = "";
rtb.PlaceholderText = "Type something...";
// Beklenen: placeholder doğru pozisyonda

// Test 4: Rtf mode
rtb.RtfText = @"{\rtf1\ansi {\b Hello} world}";
// Beklenen: "Hello" bold
```

## Sorun Giderme

### "Scroll hala çalışmıyor (metin sabit)"
- `GetVerticalScrollSafe` ve `GetHorizontalScrollSafe` hâlâ 0 döndürüyorsa,
  base class üyeleri expose edilmemiş demektir. Yukarıdaki Seçenek A/B/C'den
  birini uygulayın.
- Kontrol: breakpoint koy, `GetVerticalScrollSafe` çağrıldığında scroll
  değerini döndürüyor mu?

### "Hala hiçbir şey gözükmüyor"
- `ShouldDrawTextContent` false döndü mü? Plain mode + tek cursor ise true
  olmalı (base class çizer).
- Modlu modda `_pipeline.EnsureLayout()` çalıştı mı? `_lines` dolu mu?

### "Metin çift çiziliyor / overlap"
- `ShouldDrawTextContent` false dönmüyor. Base class da text çiziyor.
- `OnPaint` içinde `if (ShouldDrawTextContent)` kontrolü eksik.

### "Fontmetrics yanlış"
- `EnsureFontMetrics` base font kullanıyor.
- DPI scaling doğru mu? `_fontCache.ScaleFactor`.

## v5.2 Özeti

| Sorun | Çözüm | Dosya |
|---|---|---|
| GetGlyphs void return | CountGlyphs + void overload | `TextBlobBatcher.cs` |
| Internal tipler erişilemiyor | Tüm internal → public | Tüm dosyalar |
| Override visibility uyumsuz | `protected internal override` → `internal override` | `RichTextBox.cs` |
| Scroll çalışmıyor | `canvas.Translate(-scrollX, -scrollY)` + base class expose | `RichTextBox.cs`, `INTEGRATION.md` |

