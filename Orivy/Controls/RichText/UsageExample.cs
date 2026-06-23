// SPDX-License-Identifier: MIT
// Orivy RichText — Usage Example
//
// Demonstrates the four modes and the styling API. This is illustrative;
// adjust to your app's wiring (form creation, layout, etc.).

using System;
using Orivy.Controls.RichText;
using Orivy.Controls.RichText.Markdown;
using SkiaSharp;

namespace Orivy.Controls.RichText.Demo;

public static class UsageExample
{
    public static void DemoPlain(RichTextBox rtb)
    {
        // Plain mode = backward-compatible with TextBox. Zero overhead.
        rtb.Mode = RichTextMode.Plain;
        rtb.Text = "Hello, world!";

        // You can still apply programmatic styling — it just renders rich.
        rtb.Select(0, 5);
        rtb.ToggleBold();  // "Hello" now bold.
    }

    public static void DemoMarkdownSource(RichTextBox rtb)
    {
        // Markdown source mode: edit markdown with live syntax highlighting.
        rtb.Mode = RichTextMode.MarkdownSource;
        rtb.Text = @"# Hello

This is **bold** and *italic* and `code`.

- Item 1
- Item 2

```csharp
var x = 42;
```

[Click here](https://example.com)
";
        // Caret, selection, scroll, clipboard all work as in Plain mode.
        // Typing **auto-apples** will style the word "apples" bold in real time.
    }

    public static void DemoMarkdownPreview(RichTextBox rtb)
    {
        rtb.Mode = RichTextMode.MarkdownSource;
        rtb.Text = "# Hello\n\nThis is **bold**.\n";

        // Toggle to preview: caret disabled, scroll enabled, rendered output shown.
        rtb.Mode = RichTextMode.MarkdownPreview;

        // Hyperlink clicks fire the event.
        rtb.HyperlinkClicked += (sender, e) =>
        {
            Console.WriteLine($"User clicked: {e.Url}");
            // Open the URL using your platform's launcher.
        };
    }

    public static void DemoRtfLoad(RichTextBox rtb, string rtfSource)
    {
        // Load RTF: parses into StyledTextDocument, switches to Rtf mode.
        rtb.RtfText = rtfSource;

        // Now the user can edit. Ctrl+B toggles bold on the selection.
        // Programmatic styling also works:
        rtb.Select(0, 10);
        rtb.ToggleItalic();
        rtb.ApplyStyle(new TextStyle { ForeColor = new SKColor(0xE4, 0x5B, 0x4B) });

        // Export back to RTF:
        var exportedRtf = rtb.RtfText;
        Console.WriteLine($"Exported {exportedRtf.Length} chars of RTF.");
    }

    public static void DemoTheming(RichTextBox rtb)
    {
        // Markdown source highlighter palette is themeable:
        var md = rtb.Document;  // access the document

        // For source-mode colors, you'd modify the MarkdownSourceHighlighter
        // instance. Since it's owned by RichTextBox, expose a property on
        // RichTextBox to reach it (omitted for brevity).
        // Example: rtb.MarkdownHighlighter.HeadingColor = SKColors.Navy;

        // For preview-mode colors, similarly:
        // rtb.MarkdownPreviewRenderer.HeadingColor = SKColors.Navy;
    }

    public static void DemoModeToggle(RichTextBox rtb)
    {
        // Source ⇄ Preview toggle with state preservation.
        rtb.Mode = RichTextMode.MarkdownSource;
        rtb.Text = "# Title\n\n**bold** text";
        rtb.Select(2, 3);  // select "Tit"

        // Switch to preview — caret/selection cleared (read-only), scroll preserved.
        rtb.Mode = RichTextMode.MarkdownPreview;

        // Switch back — Text preserved, caret reset to 0.
        rtb.Mode = RichTextMode.MarkdownSource;
    }
}

// ── Integration checklist (for the developer wiring this into Orivy) ──
//
// 1. Make sure the base Orivy.Controls.TextBox exposes (as protected internal):
//      - OnPaint, OnKeyDown, OnTextChanged, OnFontChanged, OnDpiChanged
//      - ProcessTextEscapeSequences, ShouldRenderDefaultText
//      - SelectionStart, SelectionLength, Select, SelectAll, CaretIndex
//      - InvalidateTextLayout, InvalidateMeasure, Invalidate
//      - Font, ForeColor, BackColor, ScaleFactor, Focused, Enabled, Visible
//      - DisplayRectangle, GetTextViewport, GetHorizontalScrollOffset, GetVerticalScrollOffset
//      - AutoScrollMinSize, UpdateScrollBars
//      - _vScrollBar, _hScrollBar fields (or expose a property)
//      - The internal TextLineLayout struct and _lines list (or expose accessor)
//
// 2. Copy DrawTextContent / BuildTextLayout / AddParagraphLines / MeasureTextWidth
//    from the base TextBox into RichTextBox as overrides (rename to
//    BuildRichTextLayout / DrawRichTextContent). Replace single-font calls
//    with the run-aware pattern shown in RichTextBox.DrawLineWithRuns.
//
// 3. In the base TextBox, add a virtual hook to suppress text drawing:
//        protected virtual bool ShouldDrawTextContent => true;
//    Override in RichTextBox:
//        protected override bool ShouldDrawTextContent => _mode != RichTextMode.Plain;
//
// 4. Wire up hyperlink click detection in TryGetHyperlinkAtPoint using
//    the same hit-test logic as GetTextIndexFromPoint.
//
// 5. (Optional) Add a "MarkdownLivePreview" mode that uses a debounced
//    background thread to rebuild the preview AST as the user types in
//    source mode. Not included in v1.
