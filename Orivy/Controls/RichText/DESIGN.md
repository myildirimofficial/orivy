# Orivy RichText — Tasarım Dokümanı

## 1. Hedefler

1. **Cross-platform**: SkiaSharp ve Orivy UI kütüphanesi dışında bağımlılık yok.
   `System.Windows.Forms` **kullanılmaz** (proje cross-platform).
2. **Geriye dönük uyumluluk**: Plain modda mevcut `TextBox` davranışı birebir
   korunur — caret, selection, scroll, clipboard, animation, focus.
3. **4 mod desteği**: Plain / MarkdownSource / MarkdownPreview / Rtf
4. **Programmatic styling**: `SetStyle`, `GetStyleAt`, `ClearStyle`, Ctrl+B/I/U.
5. **Tüm markdown özellikleri**: H1-H6, bold, italic, strikethrough, inline code,
   code block, blockquote, list (ordered/unordered), task list, horizontal rule,
   link, image, table.
6. **RTF I/O**: Yükleme (`RtfText` setter) ve dışa aktarma (`RtfText` getter).

## 2. Karar: Char-Index Modeli

**Seçim**: Caret, selection, scroll — hepsi mevcut `Text` string'i üzerinde
char-index bazlı çalışmaya devam eder.

**Neden**: Mevcut `TextBox`'ın 2025 satırlık caret/selection/scroll/hit-test
kodunu yeniden yazmak riskli. Bunun yerine:
- `Text` = source text (markdown kaynak veya RTF'den gelen plain text)
- `StyledTextDocument.Runs` = paralel run listesi
- Edit işlemleri (insert/delete) hem `Text`'i hem `Runs`'u senkron günceller

**İstisna**: `MarkdownPreview` modunda kaynak markdown, hedef rendered text
farklıdır. Bu modda **caret disable** edilir (read-only); kullanıcı toggle ile
Source moduna döner ve source üzerinde düzenler. Bu, v1 için pratik bir trade-off
(Typora-style live WYSIWYG çok pahalıdır: AST↔source mapping gerektirir).

## 3. Run Modeli

### 3.1 TextStyle (immutable)

```csharp
public readonly struct TextStyle : IEquatable<TextStyle>
{
    public string? FontFamily { get; init; }   // null = inherit base font
    public float? FontSize { get; init; }       // null = inherit
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public bool? Underline { get; init; }
    public bool? Strikethrough { get; init; }
    public bool? Superscript { get; init; }
    public bool? Subscript { get; init; }
    public SKColor? ForeColor { get; init; }
    public SKColor? BackColor { get; init; }
    public string? Hyperlink { get; init; }
    public TextVerticalAlign VerticalAlign { get; init; }
}
```

- Nullable alanlar = "inherit base" anlamına gelir.
- `Merge(other)` — `other`'daki non-null değerler override eder.
- Default değer = base font ile aynı (Plain mode davranışı).

### 3.2 TextRun

```csharp
public readonly struct TextRun
{
    public int Start { get; }
    public int Length { get; }
    public TextStyle Style { get; }
    public int End => Start + Length;
}
```

### 3.3 StyledTextDocument

Run listesi şu invariantları korur:
1. **Sorted** by `Start`.
2. **Non-overlapping**.
3. **Contiguous coverage** — her char bir run'a aittir; gaps Default stilli
   run ile doldurulur.

#### Style Ops

- `SetStyle(start, length, style)`:
  1. Etkilenen run'ları split et (gerekirse)
  2. Etkilenen range'i tek run haline getir
  3. Komşu run'lar aynı stile sahipse merge et

- `ClearStyle(start, length)`: aynı mekanizma, Default stil ile.

- `OnTextInsert(index, inserted)`: index'ten sonraki tüm run'ların `Start`'ını
  `inserted.Length` kadar kaydır.
- `OnTextDelete(start, length)`: Silinen range'teki run'ları kaldır/shorten,
  kalanları kaydır.

## 4. Mod Pipeline'ları

### 4.1 Plain Mode

- `Runs` = tek Default run tüm metin üzerinden.
- Layout: mevcut `_layoutFont.MeasureText` ile.
- Draw: tek `TextRenderer.DrawText`.
- **Sıfır ek maliyet** — mevcut davranış.

### 4.2 MarkdownSource Mode

**Amaç**: Markdown source'unu syntax-highlighted olarak göster. Kullanıcı
markdown yazar, anlık olarak `**bold**` kelimesi bold font ile (ama `**`
karakterleri görünür şekilde) çizilir.

**Pipeline**:
1. Text change → `MarkdownSourceHighlighter.Highlight(text)` → `List<TextRun>`
2. Run'lar `Text` üzerinde char-index ile birebir (display = source).
3. Layout: `RunAwareMeasurer.MeasureLine(runs, lineStart, lineEnd)` →
   her segment için font cache'den font al, ölç, topla.
4. Draw: her segment için ayrı `DrawText` çağrısı, ilgili font ve paint ile.

**Incremental optimization**: Sadece değişen paragrafı re-tokenize et.
Paraagraflar `\n\n` ile ayrılır. Her paragrafın hash'i cache'lenir.

### 4.3 MarkdownPreview Mode

**Amaç**: Rendered markdown, read-only.

**Pipeline** (toggle'a basınca, debouncesuz):
1. `MarkdownParser.Parse(text)` → AST
2. `MarkdownPreviewRenderer.Render(ast)` → yeni `StyledTextDocument`
   (display text + runs)
3. Bu document `RichTextBox`'ın render document'ı olur
4. Caret disable, scroll aktif
5. Source ↔ preview scroll sync (paragraf bazlı, opsiyonel)

**Cache**: Source hash → rendered document cache. Aynı source tekrar
preview'a geçişte cache hit.

### 4.4 Rtf Mode

**Amaç**: RTF yüklenir, programmatic styling ile edit edilir, RTF olarak
dışa aktarılır.

**Pipeline**:
1. `RtfText` setter → `RtfReader.Parse(rtf)` → `StyledTextDocument`
2. Document'in `Text`'i = RTF'den çıkarılan plain text
3. Document'in `Runs`'ı = RTF'den çıkarılan stilli segmentler
4. Edit: kullanıcı metin yazınca `Runs` otomatik senkronize olur
   (StyledTextDocument.OnTextInsert/Delete)
5. Ctrl+B gibi op'lar `SetStyle(selection, bold)` çağırır
6. `RtfText` getter → `RtfWriter.Write(document)` → RTF string

## 5. FontCache

```csharp
public sealed class FontCache : IDisposable
{
    private readonly Dictionary<FontKey, SKFont> _cache = new();
    private readonly SKTypeface _baseTypeface;
    private readonly float _baseSize;
    private readonly float _scaleFactor;

    public SKFont GetFont(TextStyle style, SKFont baseFont);
    // → resolve (family, size, bold, italic)
    // → lookup cache; miss'te SKTypeface.CreateFromFamily + new SKFont
}
```

- **Key**: `(family, size, bold, italic)` — 4 alan. Diğer stil alanları
  (color, underline) paint üzerinden.
- **Lifecycle**: `RichTextBox.Dispose`'ta cache temizlenir.
- **DPI change**: cache temizlenir, rebuild.

## 6. RunAwareMeasurer

```csharp
public sealed class RunAwareMeasurer
{
    public float MeasureSegmentWidth(string text, int start, int length, SKFont font);
    // → cache lookup: (text_hash, font_key) → width

    public float MeasureLineWithRuns(string text, int lineStart, int lineLength,
                                     IReadOnlyList<TextRun> runs, FontCache fonts);
    // → run'ları line range ile intersect et
    // → her segmenti ilgili font ile ölç, topla
}
```

**Cache invalidation**:
- Font cache temizlenince segment cache de temizlenir.
- LRU max 4096 entry.

## 7. Performans Karşılaştırması

| Senaryo | Mevcut TextBox | Plain | MarkdownSource | MarkdownPreview | Rtf |
|---|---|---|---|---|---|
| Typing (1 char) | ~50µs | ~50µs | ~150µs* | N/A (read-only) | ~100µs |
| Layout rebuild | ~1ms (1K satır) | ~1ms | ~3ms | ~15ms (full parse) | ~2ms |
| First render | ~1ms | ~1ms | ~3ms | ~20ms | ~3ms (parse) |
| Scroll | ~200µs/frame | ~200µs | ~400µs | ~400µs | ~400µs |

*Paragraph incremental cache hit ile.

## 8. Caret & Selection Davranışı

| Mod | Caret | Selection | Klavye |
|---|---|---|---|
| Plain | ✅ aktif | ✅ | Mevcut |
| MarkdownSource | ✅ aktif | ✅ | Mevcut + Ctrl+B/I/K |
| MarkdownPreview | ❌ hidden | ❌ disabled | Sadece scroll (Page/Up/Down) |
| Rtf | ✅ aktif | ✅ | Mevcut + Ctrl+B/I/U |

## 9. Mod Geçişleri

```
Plain ↔ MarkdownSource    (Text korunur, runs rebuild)
MarkdownSource ↔ MarkdownPreview (Text korunur, layout rebuild, scroll sync)
Rtf ↔ Plain               (Text korunur, runs silinir)
Rtf ↔ MarkdownSource      (Text korunur, markdown olarak re-highlight)
```

Geçişler sırasında:
- `Text` her zaman korunur
- Caret position korunur (clamped)
- Selection korunur (clamped)
- Scroll position korunur (mümkün olduğunca)

## 10. Cross-Platform RTF Parser

WinForms `RichTextBox` kullanılamadığı için kendi RTF parser'ımız:

**Subset desteklenen RTF özellikleri**:
- `\b`, `\i`, `\ul`, `\strike`, `\sub`, `\super`, `\nosupersub`
- `\fsN` (font size, half-points)
- `\cfN`, `\cbN` (color table index)
- `\fN` (font table index)
- `\par`, `\line`, `\tab`
- `\{`, `\}`, `\\` escape
- `\fonttbl`, `\colortbl`
- `\rtf1`, `\ansi`, `\deff0`
- Unicode: `\uN`

**Desteklenmeyen (v1)**: resim (`\pict`), tablo, OLE objeleri.

Parser, token bazlıdır (single-pass, O(n)).

## 11. Markdown Subset

Tam CommonMark değil, pratik subset:

| Özellik | Syntax | Render |
|---|---|---|
| H1-H6 | `#`, `##`, ... | Bold + size scaling |
| Bold | `**text**` veya `__text__` | Bold |
| Italic | `*text*` veya `_text_` | Italic |
| Strikethrough | `~~text~~` | Strikethrough |
| Inline code | `` `code` `` | Monospace + bg |
| Code block | ` ``` ` fence | Monospace + bg + border |
| Blockquote | `> text` | Italic + indent + left border |
| Unordered list | `-`, `*`, `+` | Bullet + indent |
| Ordered list | `1.`, `2.` | Number + indent |
| Task list | `- [ ]`, `- [x]` | Checkbox + text |
| Horizontal rule | `---`, `***` | Separator line |
| Link | `[text](url)` | Hyperlink style |
| Image | `![alt](url)` | Placeholder + alt text |
| Table | `\| a \| b \|` | Border + cells |

Parser, satır bazlı (her satır tek geçişte classify edilir), nested inline
parser (bold içinde link vb.).

## 12. Sınırlar (v1)

- Preview mode read-only (live WYSIWYG yok)
- RTF image/table desteği yok
- AST ↔ source mapping yok (Typora-style caret mapping)
- Undo/redo runs'ı undo etmez (sadece text) — v2 için TODO
- Async rendering yok (v1 sync; v2'de background thread + invalidation)

## 13. Test Stratejisi

Manuel test senaryoları (entegrasyon sonrası):

1. Plain mode'da mevcut davranış birebir aynı (regression)
2. MarkdownSource'da `**bold**` yazınca "bold" kelimesi bold görünür,
   `**` karakterleri visible
3. MarkdownSource → Preview toggle'da scroll preserved
4. Rtf mode'da `RtfText = "{\\rtf1...}"` sonrası bold görünür
5. Ctrl+B selection'da bold toggle
6. Multi-line wrap'in run'lar ile doğru çalışması
7. DPI change sonrası font cache invalidate
8. 10K satır markdown'da scroll akıcı (<16ms/frame)
