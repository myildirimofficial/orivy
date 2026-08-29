using Orivy;
using Orivy.Controls;
using Orivy.Helpers;
using Orivy.Studio.Persistence;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Linq;

namespace Orivy.Studio.Panels;

/// <summary>
/// The Start Screen: a full-window scrim + centered card shown when Studio launches, instead of the
/// app just dropping straight into a blank throwaway document. Offers New (a blank canvas) and Open
/// Folder — there is no separate "project file" concept — plus a list of recently opened
/// folders/files (see <see cref="RecentProjects"/>). Dismisses itself once the user picks something.
///
/// Every piece of text at a non-default size (title, section header, action rows, recent rows) is
/// drawn directly in an OnPaint override with its own <see cref="SKFont"/> instance, rather than
/// relying on <see cref="ElementBase.Text"/> auto-rendering — the same convention every other rich
/// row/label in Orivy.Studio already follows (see <c>ToolboxList</c>, <c>NotificationToast</c>,
/// <c>ToolbarButton</c>). Two real bugs came from deviating from that convention on the first pass:
/// reassigning <c>ElementBase.Font</c> after construction to get a custom size left the element's
/// auto-computed height stuck at zero, and nesting plain <see cref="Element"/> children inside a
/// <see cref="Button"/> for a title+description layout never painted their text at all — a
/// <see cref="Button"/> in this framework is a leaf control that draws its own content, not a
/// generic container. Custom OnPaint sidesteps both: there is exactly one code path for this text
/// (as opposed to auto-render normally, custom-paint only when the framework's default breaks), and
/// it is the same path every other polished row in this codebase already relies on.
/// </summary>
public sealed class StartScreen : Element
{
    private readonly Card _card;

    public StartScreen()
    {
        Dock = DockStyle.Fill;
        Border = new Thickness(0);
        Radius = new Radius(0);
        ZOrder = 2_000_000; // above the drag layer and everything else
        ConfigureVisualStyles(styles => styles.Base(b => b.Background(SKColors.Black.WithAlpha(90))));

        _card = new Card();
        _card.NewRequested += () => NewRequested?.Invoke();
        _card.OpenFolderRequested += () => OpenFolderRequested?.Invoke();
        _card.RecentSelected += (path, isFolder) => RecentSelected?.Invoke(path, isFolder);

        Controls.Add(_card);
        SizeChanged += (_, _) => CenterCard();
        CenterCard();
    }

    public event Action? NewRequested;
    public event Action? OpenFolderRequested;
    public event Action<string, bool>? RecentSelected; // path, isFolder

    private void CenterCard() =>
        _card.Location = new SKPoint((Width - _card.Width) / 2f, (Height - _card.Height) / 2f);

    /// <summary>
    /// Builds an <see cref="SKFont"/> at a specific size with the same rendering settings the
    /// framework's own text paths apply (<c>Application.CreateUiFont</c>/<c>ApplyPreferredFontRendering</c>),
    /// which this project can't call directly since they're internal to the Orivy assembly.
    /// </summary>
    private static SKFont CreateUiFont(float size) => new(Application.DefaultFont.Typeface, size)
    {
        Subpixel = true,
        Edging = SKFontEdging.SubpixelAntialias,
        Hinting = SKFontHinting.Full,
        LinearMetrics = true,
    };

    /// <summary>The white card itself: draws its own title/subtitle/section-header text, and hosts
    /// the action buttons + recent list as real child controls.</summary>
    private sealed class Card : Element
    {
        private const float CardPadding = 28f;
        private const float TitleHeight = 30f;
        private const float SubtitleHeight = 36f;
        private const float SubtitleGap = 16f;
        private const float SectionHeaderHeight = 22f;

        private readonly SKFont _titleFont;
        private readonly SKFont _subtitleFont;
        private readonly SKFont _sectionHeaderFont;
        private readonly SKPaint _textPaint = new() { IsAntialias = true };

        private readonly Element _actionsHost;
        private readonly Element _recentList;

        public event Action? NewRequested;
        public event Action? OpenFolderRequested;
        public event Action<string, bool>? RecentSelected;

        public Card()
        {
            Size = new SKSize(440, 500);
            Radius = new Radius(16);
            Border = new Thickness(1);
            Padding = new Thickness((int)CardPadding);

            ConfigureVisualStyles(styles => styles.Base(b => b
                .Background(ColorScheme.Surface)
                .BorderColor(ColorScheme.Outline.WithAlpha(70))
                .Shadow(new BoxShadow(0f, 24f, 64f, 0, ColorScheme.ShadowColor.WithAlpha(90)))));

            _titleFont = CreateUiFont(20f);
            _subtitleFont = CreateUiFont(11f);
            _sectionHeaderFont = CreateUiFont(10f);

            var headerHeight = (int)(TitleHeight + SubtitleHeight + SubtitleGap);

            _actionsHost = new Element
            {
                Dock = DockStyle.Top, Border = new Thickness(0), Radius = new Radius(0),
                Height = 60 * 2 + 8 * 1,
                Margin = new Thickness(0, headerHeight, 0, 18),
            };
            // Same-edge Dock siblings in this framework lay out in REVERSE insertion order — the
            // last-added control ends up closest to the true edge (confirmed against the working
            // Layers/Layout/Properties panel stack in StudioWindow, which relies on the same
            // behavior). Add bottom-to-top so "New" — the one we want first — ends up on top.
            _actionsHost.Controls.Add(new ActionButton("folder", "Open Folder…", "Browse a folder — its files and subfolders show up in Explorer", () => OpenFolderRequested?.Invoke()));
            _actionsHost.Controls.Add(new ActionButton("new-doc", "New", "Blank canvas", () => NewRequested?.Invoke()));

            _recentList = new Element
            {
                Dock = DockStyle.Fill, Border = new Thickness(0), Radius = new Radius(0),
                AutoScroll = true, Margin = new Thickness(0, (int)SectionHeaderHeight + 4, 0, 0),
            };

            Controls.Add(_recentList);
            Controls.Add(_actionsHost);

            RefreshRecent();
        }

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);

            var content = DisplayRectangle;
            _textPaint.Color = ColorScheme.ForeColor;
            TextRenderer.DrawText(canvas, "Orivy Studio", content.Left, content.Top + TitleHeight - 8f, _titleFont, _textPaint);

            _textPaint.Color = ColorScheme.ForeColor.WithAlpha(150);
            TextRenderer.DrawText(
                canvas, "Start a new design, open an existing one, or pick up where you left off.",
                content.Left, content.Top + TitleHeight + 14f, SKTextAlign.Left, _subtitleFont, _textPaint,
                new TextRenderOptions { Wrap = TextWrap.WordWrap, MaxWidth = content.Width, MaxHeight = SubtitleHeight, LineSpacing = 1.3f });

            // Read _recentList's own post-layout position rather than re-deriving it from
            // _actionsHost's — the first attempt computed this independently and drifted below
            // where the list's own top margin actually put it, printing "RECENT" under the list
            // instead of above it.
            _textPaint.Color = ColorScheme.ForeColor.WithAlpha(120);
            TextRenderer.DrawText(canvas, "RECENT", content.Left, _recentList.Location.Y - 8f, _sectionHeaderFont, _textPaint);
        }

        private void RefreshRecent()
        {
            _recentList.Controls.Clear();
            var entries = RecentProjects.Load();

            if (entries.Count == 0)
            {
                var empty = new Element
                {
                    Text = "No recent projects yet.", Dock = DockStyle.Top, Height = 28,
                    Border = new Thickness(0), Radius = new Radius(0), TextAlign = ContentAlignment.MiddleLeft,
                };
                empty.ConfigureVisualStyles(styles => styles.Base(b => b.Foreground(ColorScheme.ForeColor.WithAlpha(110))));
                _recentList.Controls.Add(empty);
                return;
            }

            // Same reverse-insertion-order rule as the action buttons: add oldest-of-the-six first
            // so the most recent entry — added last — ends up on top.
            foreach (var entry in entries.Take(6).Reverse())
            {
                var path = entry.Path;
                var isFolder = entry.IsFolder;
                _recentList.Controls.Add(new RecentRow(path, isFolder, () => RecentSelected?.Invoke(path, isFolder)));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subtitleFont.Dispose();
                _sectionHeaderFont.Dispose();
                _textPaint.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>One "New Project" / "Open Project…" / "Open Folder…" row. A <see cref="Button"/>
    /// subclass (for its click/focus/hover plumbing) that paints its own icon + title + description
    /// instead of hosting them as child controls — see the class remarks on <see cref="StartScreen"/>
    /// for why that matters here.</summary>
    private sealed class ActionButton : Button
    {
        private readonly string _icon;
        private readonly string _title;
        private readonly string _description;
        private readonly SKFont _titleFont;
        private readonly SKFont _descriptionFont;
        private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeWidth = 1.6f };
        private readonly SKPaint _textPaint = new() { IsAntialias = true };

        public ActionButton(string icon, string title, string description, Action onClick)
        {
            _icon = icon;
            _title = title;
            _description = description;
            _titleFont = CreateUiFont(13f);
            _descriptionFont = CreateUiFont(10f);

            Text = string.Empty;
            Dock = DockStyle.Top;
            Height = 60;
            Margin = new Thickness(0, 0, 0, 8);
            Padding = new Thickness(14, 0, 14, 0);
            Radius = new Radius(10);

            ConfigureVisualStyles(styles => styles
                .Base(b => b.Background(ColorScheme.SurfaceContainerHigh).Border(1).BorderColor(ColorScheme.Outline.WithAlpha(60)).Shadow(BoxShadow.None))
                .OnHover(r => r.Background(ColorScheme.Primary.WithAlpha(20)).BorderColor(ColorScheme.Primary.WithAlpha(120)))
                .OnPressed(r => r.Scale(0.99f))
                .OnFocused(r => r.BorderColor(ColorScheme.Primary.WithAlpha(180)).Border(2)),
                clearExisting: true);

            Click += (_, _) => onClick();
        }

        protected override bool ShouldRenderDefaultText => false;

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);

            var content = DisplayRectangle;
            var iconRect = new SKRect(content.Left, content.MidY - 11f, content.Left + 22f, content.MidY + 11f);
            _iconPaint.Color = ColorScheme.ForeColor.WithAlpha(200);
            ToolbarIcons.Draw(canvas, _icon, iconRect, _iconPaint);

            var textLeft = iconRect.Right + 12f;
            _textPaint.Color = ColorScheme.ForeColor;
            TextRenderer.DrawText(canvas, _title, textLeft, content.MidY - 4f, _titleFont, _textPaint);
            _textPaint.Color = ColorScheme.ForeColor.WithAlpha(130);
            TextRenderer.DrawText(canvas, _description, textLeft, content.MidY + 15f, _descriptionFont, _textPaint);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _descriptionFont.Dispose();
                _iconPaint.Dispose();
                _textPaint.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>One row in the recent-projects list — same custom-paint approach as
    /// <see cref="ActionButton"/> and for the same reason.</summary>
    private sealed class RecentRow : Button
    {
        private readonly bool _isFolder;
        private readonly string _name;
        private readonly string _path;
        private readonly SKFont _nameFont;
        private readonly SKFont _pathFont;
        private readonly SKPaint _iconPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, StrokeWidth = 1.5f };
        private readonly SKPaint _textPaint = new() { IsAntialias = true };

        public RecentRow(string path, bool isFolder, Action onClick)
        {
            _path = path;
            _isFolder = isFolder;
            _name = isFolder
                ? System.IO.Path.GetFileName(path.TrimEnd('\\', '/'))
                : System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetFileNameWithoutExtension(path));
            _nameFont = CreateUiFont(11.5f);
            _pathFont = CreateUiFont(9f);

            Text = string.Empty;
            Dock = DockStyle.Top;
            Height = 42;
            Margin = new Thickness(0, 0, 0, 4);
            Padding = new Thickness(10, 0, 10, 0);
            Radius = new Radius(8);

            ConfigureVisualStyles(styles => styles
                .Base(b => b.Background(SKColors.Transparent).Border(0).Shadow(BoxShadow.None))
                .OnHover(r => r.Background(ColorScheme.ForeColor.WithAlpha(16)))
                .OnPressed(r => r.Background(ColorScheme.ForeColor.WithAlpha(28)))
                .OnFocused(r => r.Border(2).BorderColor(ColorScheme.Primary.WithAlpha(180))),
                clearExisting: true);

            Click += (_, _) => onClick();
        }

        protected override bool ShouldRenderDefaultText => false;

        public override void OnPaint(SKCanvas canvas)
        {
            base.OnPaint(canvas);

            var content = DisplayRectangle;
            var iconRect = new SKRect(content.Left, content.MidY - 9f, content.Left + 18f, content.MidY + 9f);
            _iconPaint.Color = ColorScheme.ForeColor.WithAlpha(170);
            ToolbarIcons.Draw(canvas, _isFolder ? "folder" : "file", iconRect, _iconPaint);

            var textLeft = iconRect.Right + 10f;
            _textPaint.Color = ColorScheme.ForeColor.WithAlpha(220);
            TextRenderer.DrawText(canvas, _name, textLeft, content.MidY - 4f, _nameFont, _textPaint);
            _textPaint.Color = ColorScheme.ForeColor.WithAlpha(110);
            TextRenderer.DrawText(canvas, TruncatePath(_path), textLeft, content.MidY + 11f, _pathFont, _textPaint);
        }

        private static string TruncatePath(string path) =>
            path.Length <= 52 ? path : "…" + path[^51..];

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _nameFont.Dispose();
                _pathFont.Dispose();
                _iconPaint.Dispose();
                _textPaint.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
