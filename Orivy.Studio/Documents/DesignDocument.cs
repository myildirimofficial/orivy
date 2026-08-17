using Orivy;
using Orivy.Controls;
using Orivy.Controls.RichText;
using Orivy.Studio.Panels;
using SkiaSharp;

namespace Orivy.Studio.Documents;

/// <summary>
/// One open design document (a "Window" being designed). It is a <see cref="Container"/> so it can
/// live as a page inside the shell's <see cref="TabView"/>, and it hosts a single
/// <see cref="DesignSurface"/> filling the page. The tab title is this container's <c>Text</c>.
///
/// A small centered, icon-only pill (Design / Code) switches the page between the live canvas and
/// a read-only, live-updating view of the generated Designer code for the current layout.
/// </summary>
public sealed class DesignDocument : Container
{
    private readonly ToolbarButton _designTab;
    private readonly ToolbarButton _codeTab;
    private readonly Element _switcherPill;
    private readonly RichTextBox _codeView;
    private bool _showingCode;

    public DesignDocument(string title)
    {
        Text = title;
        Surface = new DesignSurface { Dock = DockStyle.Fill };

        _codeView = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Visible = false,
            Margin = new Thickness(16),
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, 10.5f),
        };

        _designTab = new ToolbarButton("design-view", "Design", 26f) { CheckOnClick = true, Checked = true, Margin = new Thickness(0) };
        _codeTab = new ToolbarButton("code-view", "Code (live)", 26f) { CheckOnClick = true, Margin = new Thickness(0) };
        _designTab.CheckedChanged += (_, _) => { if (_designTab.Checked) ShowDesign(); };
        _codeTab.CheckedChanged += (_, _) => { if (_codeTab.Checked) ShowCode(); };

        _switcherPill = new Element
        {
            Size = new SKSize(26f * 2 + 6f, 32f),
            Radius = new Radius(9),
            Border = new Thickness(0),
            Padding = new Thickness(3),
        };
        _switcherPill.ConfigureVisualStyles(styles => styles.Base(b => b.Background(ColorScheme.SurfaceContainerHigh)));
        _switcherPill.Controls.Add(_codeTab);
        _switcherPill.Controls.Add(_designTab);

        var switcherHost = new Element
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
        };
        switcherHost.Controls.Add(_switcherPill);
        switcherHost.SizeChanged += (_, _) => CenterSwitcher(switcherHost);
        CenterSwitcher(switcherHost);

        Controls.Add(Surface);
        Controls.Add(_codeView);
        Controls.Add(switcherHost);

        // "Live" code view: while it's the visible tab, any structural or bounds change re-generates
        // it immediately. While the design canvas is showing, regeneration is skipped entirely — no
        // point re-running the generator on every drag frame for a view nobody's looking at.
        Surface.StructureChanged += RefreshCodeIfVisible;
        Surface.SelectionBoundsChanged += RefreshCodeIfVisible;
    }

    public DesignSurface Surface { get; }

    /// <summary>Backing project file path, or null if never saved.</summary>
    public string? FilePath { get; set; }

    private void CenterSwitcher(Element host)
    {
        _switcherPill.Location = new SKPoint(
            (host.Width - _switcherPill.Width) / 2f,
            (host.Height - _switcherPill.Height) / 2f);
    }

    private void ShowDesign()
    {
        _showingCode = false;
        _codeTab.Checked = false;
        Surface.Visible = true;
        _codeView.Visible = false;
    }

    private void ShowCode()
    {
        _showingCode = true;
        _designTab.Checked = false;
        RefreshCode();
        Surface.Visible = false;
        _codeView.Visible = true;
    }

    private void RefreshCodeIfVisible()
    {
        if (_showingCode)
            RefreshCode();
    }

    private void RefreshCode()
    {
        var className = string.IsNullOrWhiteSpace(Text) ? "MyWindow" : Text;
        _codeView.Text = CodeGenerator.Generate(Surface, className);
    }
}
