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
            "Aligned equations (align*):",
            "$$",
            "\\begin{align*}",
            "E &= mc^2 \\\\",
            "F &= ma",
            "\\end{align*}",
            "$$",
            "",
            "Chi-squared test:",
            "$$\\chi^2 = \\sum \\frac{(O_i - E_i)^2}{E_i}$$",
            "",
            "Maxwell's equations:",
            "$$",
            "\\nabla \\cdot \\mathbf{E} = \\frac{\\rho}{\\varepsilon_0} \\qquad",
            "\\nabla \\cdot \\mathbf{B} = 0",
            "$$",
            "",
            "$$",
            "\\nabla \\times \\mathbf{E} = -\\frac{\\partial \\mathbf{B}}{\\partial t} \\qquad",
            "\\nabla \\times \\mathbf{B} = \\mu_0\\mathbf{J} + \\mu_0\\varepsilon_0\\frac{\\partial \\mathbf{E}}{\\partial t}",
            "$$",
            "",
            "### Mermaid Diagram",
            "```mermaid",
            "flowchart TD",
            "    A[Start] --> B{Is it valid?}",
            "    B -->|Yes| C[Process data]",
            "    B -->|No| D[Show error]",
            "    C --> E((Done))",
            "    D --> E",
            "```",
            "",
            "### Mark Highlight",
            "==Highlighted text== using double equals.",
            "<mark>HTML mark tag</mark>.",
            "",
            "### HTML Elements (Details, Div, KBD)",
            "<details>",
            "<summary>Click to expand</summary>",
            "",
            "- Markdown inside",
            "- HTML container",
            "- **Mixed** formatting",
            "",
            "</details>",
            "",
            "<div align=\"center\">",
            "",
            "# Centered Heading",
            "Centered paragraph",
            "",
            "</div>",
            "",
            "### Keyboard & HTML Inline Tags",
            "<kbd>Ctrl</kbd> + <kbd>C</kbd>",
            "",
            "<mark>Highlighted text</mark>",
            "",
            "<sup>Superscript</sup> and <sub>subscript</sub>",
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
