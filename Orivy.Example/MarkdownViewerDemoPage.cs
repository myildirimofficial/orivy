using Orivy.Controls;
using Orivy.Controls.Markdown;
using Orivy.Controls.RichText;
using SkiaSharp;
using System;

namespace Orivy.Example;

internal sealed partial class MarkdownViewerDemoPage : Container
{
    private MarkdownViewer viewer = null!;
    private TextBox editor = null!;

    public MarkdownViewerDemoPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Markdown Viewer";
        Name = "markdownViewerDemoPage";
        Dock = DockStyle.Fill;
        Padding = new(3);
        Radius = new(0);
        Border = new(0);

        editor = new()
        {
            Name = "markdownEditor",
            Dock = DockStyle.Fill,
            Margin = new(0, 12, 0, 0),
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            PlaceholderText = "Write markdown here...",
        };
        editor.Text = string.Join("\n", new[]
        {
            "# Markdown Viewer Demo",
            "",
            "## Extended Features",
            "",
            "### HTML Span Styling",
            "<span style=\"color: red\">Red text</span> and <span style=\"background-color: yellow\">Yellow bg</span>.",
            "<span style=\"color: blue; background-color: cyan\">Blue on cyan</span>",
            "",
            "### Greek Letters (HTML Entities)",
            "- Alpha: &alpha; Beta: &beta; Gamma: &gamma;",
            "- Delta: &Delta; Omega: &Omega; Pi: &pi;",
            "",
            "### Math Formulas",
            "Inline formula: $E = mc^2$",
            "",
            "Block formula:",
            "$$",
            "\\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}",
            "$$",
            "",
            "Multiple equations:",
            "$$",
            "\\begin{cases}",
            "x + y = 10 \\\\",
            "2x - y = 5",
            "\\end{cases}",
            "$$",
            "",
            "### Mark Highlight",
            "==Highlighted text== using double equals.",
            "<mark>HTML mark tag</mark>.",
            "",
            "### Other Extended Features",
            "- <ins>Underline text</ins>",
            "- ++Underline++ using double plus",
            "- ^Superscript^",
            "- ~Subscript~",
            "- ~~Strikethrough~~",
        });
        editor.TextChanged += (_, _) => viewer.Text = editor.Text;

        viewer = new()
        {
            Name = "markdownViewer",
            Dock = DockStyle.Fill,
            Margin = new(0),
            Text = editor.Text,
            AutoScroll = true,
            //AutoScrollMargin = new(0, 24),
        };

        var splitter = new SplitContainer()
        {
            Name = "markdownSplitter",
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Height = 24,
            Margin = new(0, 12, 0, 0),
        };
        splitter.Panel1.Controls.Add(viewer);
        splitter.Panel2.Controls.Add(editor);

        Controls.Add(splitter);
    }
}
