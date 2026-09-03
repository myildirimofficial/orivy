using Orivy;
using Orivy.Controls;
using Orivy.Controls.RichText;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.IO;

namespace Orivy.Studio.Documents;

/// <summary>
/// One open design document (a "Window" being designed). It is a <see cref="Container"/> so it can
/// live as a page inside the shell's title-bar <see cref="TabView"/>, and it hosts a single
/// <see cref="DesignSurface"/> filling the page. The tab's visible <c>Text</c> is derived from
/// <see cref="DocumentName"/> plus a dirty-state suffix — kept separate because
/// <see cref="DocumentName"/> also doubles as the generated-code class name, which can't carry a
/// "• unsaved" marker.
///
/// The Design/Code switch is itself a small embedded <see cref="TabView"/> (icon-only, centered,
/// pill design) rather than a hand-rolled toggle pair — the same control the shell uses for its
/// document tabs, just in <see cref="TabViewMode.Embedded"/> instead of <see cref="TabViewMode.TitleBar"/>,
/// so there is exactly one tab-switching implementation in the whole app.
/// </summary>
public sealed class DesignDocument : Container, IStudioDocument
{
    // TabView draws embedded-mode tab icons at 24 (logical) px — rasterizing at that exact size (×2
    // for a crisp render at any pixel-snapping) instead of an arbitrary smaller size avoids the
    // upscale blur that comes from stretching a smaller bitmap up to fill the icon slot.
    private const float SwitcherIconSize = 24f;

    private readonly TabView _switcher;
    private readonly Container _designPage;
    private readonly Container _codePage;
    private readonly RichTextBox _codeView;
    private bool _showingCode;
    private string _documentName;
    private bool _dirty;
    private readonly EventHandler _themeChangedHandler;

    public DesignDocument(string title)
    {
        _documentName = title;
        _themeChangedHandler = (_, _) => RefreshSwitcherIcons();
        Surface = new DesignSurface { Dock = DockStyle.Fill };
        Surface.Commands.Changed += () =>
        {
            // Loading a file resets/clears the command stack (no undo entries recorded), while a
            // genuine edit executes or pushes one — CanUndo is what tells the two apart, since
            // CommandStack.Changed itself fires for both Execute/Push AND Clear.
            if (!_dirty && Surface.Commands.CanUndo)
            {
                _dirty = true;
                UpdateTabText();
                DirtyChanged?.Invoke();
            }
        };

        _codeView = new RichTextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Margin = new Thickness(16),
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas") ?? SKTypeface.Default, 10.5f),
        };

        // TabView only recognizes Container-typed children as pages (see TabView.IsTabViewPage) and
        // reads each page's own Text/Image for its tab label/icon — wrap the surface and the
        // (content-bearing) code view in dedicated pages instead of adding them directly, so
        // RichTextBox.Text stays free to hold the actual generated source instead of colliding with
        // the tab label.
        _designPage = new Container { Dock = DockStyle.Fill, Border = new Thickness(0), Radius = new Radius(0) };
        _designPage.Controls.Add(Surface);
        _codePage = new Container { Dock = DockStyle.Fill, Border = new Thickness(0), Radius = new Radius(0) };
        _codePage.Controls.Add(_codeView);

        _switcher = new TabView
        {
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            Border = new Thickness(0),
            Radius = new Radius(0),
            TabMode = TabViewMode.Embedded,
            TabDesignMode = TabViewDesignMode.MacOS,
            TabAlignment = TabViewAlignment.Center,
            TabLayoutMode = TabViewLayoutMode.Top,
            TabStripHeight = 44f,
            DrawTabIcons = true,
            // A page-switch transition captures a snapshot of the incoming page synchronously as part
            // of *starting* the switch — before SelectedIndexChanged (and therefore RefreshCode() below)
            // ever gets a chance to populate it. A fade between a design canvas and a text view was
            // never a transition worth keeping anyway, so it's simplest to just skip the whole snapshot
            // dance rather than teach it to wait for RefreshCode() first.
            EnableTransitions = false,
        };
        _switcher.Controls.Add(_designPage);
        _switcher.Controls.Add(_codePage);
        _switcher.SelectedIndexChanged += (_, _) =>
        {
            // The event argument is the *previous* selected index, not the new one — reading it as
            // "the tab we just switched to" (as this used to) meant _showingCode was always one click
            // behind: switching to Code left it false (so RefreshCode() never ran, showing an empty
            // page), and switching back to Design set it true (running RefreshCode() pointlessly while
            // the code page wasn't even visible). The *next* switch back to Code then looked fine only
            // because that stale, wrongly-timed refresh had already populated it. _switcher.SelectedIndex
            // itself is the authoritative current selection — always read that instead.
            _showingCode = _switcher.SelectedIndex == 1;
            if (_showingCode)
                RefreshCode();
        };

        RefreshSwitcherIcons();
        ColorScheme.ThemeChanged += _themeChangedHandler;

        Controls.Add(_switcher);
        UpdateTabText();

        // "Live" code view: while it's the visible tab, any structural or bounds change re-generates
        // it immediately. While the design canvas is showing, regeneration is skipped entirely — no
        // point re-running the generator on every drag frame for a view nobody's looking at.
        Surface.StructureChanged += RefreshCodeIfVisible;
        Surface.SelectionBoundsChanged += RefreshCodeIfVisible;
    }

    public DesignSurface Surface { get; }

    /// <summary>Backing project file path, or null if never saved.</summary>
    public string? FilePath { get; set; }

    /// <summary>The exact text this document was loaded from (or last saved as) — null for a document
    /// that was never backed by a file. <see cref="RefreshCode"/> shows this verbatim while the
    /// document is clean, rather than <see cref="CodeGenerator"/>'s regenerated output: the generator
    /// only knows about what the visual model tracks, so re-running it on a file nobody has touched
    /// yet — just to show the Code tab — could visibly differ from (or silently discard parts of) the
    /// actual file on disk, e.g. custom hand-written code or constructs the importer doesn't model.</summary>
    public string? OriginalSourceText { get; set; }

    public bool IsDirty => _dirty;

    public event Action? DirtyChanged;

    /// <summary>The design's display name — also used as the generated code's class name, so it
    /// stays free of the tab's dirty-state suffix (see <see cref="Text"/>, which carries that).</summary>
    public string DocumentName
    {
        get => _documentName;
        set
        {
            _documentName = value;
            UpdateTabText();
        }
    }

    public void Save()
    {
        if (FilePath == null)
            throw new InvalidOperationException("This document has no file path to save to yet.");

        // A design has no proprietary save format of its own — it saves as the exact same Designer
        // C# code Export produces, so the file on disk is always a plain, valid, hand-editable .cs
        // file rather than a hidden project format only Orivy.Studio understands.
        var code = CodeGenerator.Generate(Surface, DocumentName);
        File.WriteAllText(FilePath, code);
        OriginalSourceText = code;
        MarkClean();
    }

    public void MarkClean()
    {
        if (!_dirty)
            return;

        _dirty = false;
        UpdateTabText();
        DirtyChanged?.Invoke();
    }

    private void UpdateTabText() => Text = _dirty ? $"{_documentName} •" : _documentName;

    private void RefreshSwitcherIcons()
    {
        var color = ColorScheme.ForeColor.WithAlpha(215);
        var oldDesignImage = _designPage.Image;
        var oldCodeImage = _codePage.Image;
        _designPage.Image = ToolbarIcons.CreateImage("design-view", SwitcherIconSize * Surface.ScaleFactor * 2f, color);
        _codePage.Image = ToolbarIcons.CreateImage("code-view", SwitcherIconSize * Surface.ScaleFactor * 2f, color);
        oldDesignImage?.Dispose();
        oldCodeImage?.Dispose();
    }

    private void RefreshCodeIfVisible()
    {
        if (_showingCode)
            RefreshCode();
    }

    private void RefreshCode()
    {
        if (!_dirty && OriginalSourceText != null)
        {
            _codeView.Text = OriginalSourceText;
            return;
        }

        var className = string.IsNullOrWhiteSpace(DocumentName) ? "MyWindow" : DocumentName;
        _codeView.Text = CodeGenerator.Generate(Surface, className);
    }

    /// <summary>
    /// Closing a tab only removes it from the shell's <c>TabView</c> — nothing disposes it
    /// automatically (this framework's <c>Controls.Remove</c> doesn't cascade dispose to children).
    /// Without this override, every closed design document would leak its native Skia paints/fonts
    /// (via <see cref="Surface"/>) indefinitely and stay pinned in memory forever via the static
    /// <see cref="ColorScheme.ThemeChanged"/> subscription below.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= _themeChangedHandler;
            _designPage.Image?.Dispose();
            _codePage.Image?.Dispose();
            Surface.Dispose();
            _codeView.Dispose();
            _designPage.Dispose();
            _codePage.Dispose();
            _switcher.Dispose();
        }

        base.Dispose(disposing);
    }
}
