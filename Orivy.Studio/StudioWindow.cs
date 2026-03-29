using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Studio;

public sealed class StudioWindow : Window
{
    private const int PageIndexWelcome = 0;
    private const int PageIndexDesigner = 1;
    private const int PageIndexCodeGenerator = 2;

    private readonly WindowPageControl _pageControl;
    private readonly StudioProject _project;
    private readonly List<ICodeGenerator> _fileGenerators;

    public StudioWindow()
    {
        Name = "OrivyStudio";
        Text = "Orivy Studio";
        Width = 1440;
        Height = 900;
        MinimumSize = new SKSize(1280, 780);
        WindowThemeType = WindowThemeType.Mica;
        RenderBackend = Rendering.RenderBackend.Software;

        _project = new StudioProject("MyStudioProject", "Figma-style design project using Orivy.");

        _fileGenerators = new List<ICodeGenerator>
        {
            new UIComponentGenerator(),
            new LayoutCodeGenerator()
        };

        SuspendLayout();

        _pageControl = CreatePageController();

        Controls.Add(CreateToolbar());
        Controls.Add(_pageControl);

        ResumeLayout(false);

        _pageControl.SelectedIndex = PageIndexWelcome;
    }

    private Element CreateToolbar()
    {
        var toolbar = new Element
        {
            Name = "studioToolbar",
            Dock = DockStyle.Top,
            Height = 62,
            BackColor = ColorScheme.SurfaceContainerHigh,
            Padding = new Thickness(12),
            Border = new Thickness(0, 0, 0, 1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.35f * 255f))
        };

        var homeButton = new Button
        {
            Name = "homeButton",
            Text = "Home",
            Width = 86,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
            Image = CreateIconHomeImage(),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        homeButton.Click += (_, _) => _pageControl.SelectedIndex = PageIndexWelcome;

        var designerButton = new Button
        {
            Name = "designerButton",
            Text = "Designer",
            Width = 110,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        designerButton.Click += (_, _) => _pageControl.SelectedIndex = PageIndexDesigner;

        var codeGenButton = new Button
        {
            Name = "codeGenButton",
            Text = "Code Generator",
            Width = 140,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };

        codeGenButton.Click += (_, _) => _pageControl.SelectedIndex = PageIndexCodeGenerator;

        toolbar.Controls.Add(homeButton);
        toolbar.Controls.Add(designerButton);
        toolbar.Controls.Add(codeGenButton);

        return toolbar;
    }

    private WindowPageControl CreatePageController()
    {
        var pageControl = new WindowPageControl
        {
            Name = "studioPageControl",
            Dock = DockStyle.Fill,
            EnableTransitions = false,
            TransitionEffect = WindowPageTransitionEffect.None,
            LockInputDuringTransition = true,
            Margin = new Thickness(0)
        };

        var welcomePage = CreatePage("welcomePage", "Welcome to Orivy Studio", "Home");
        var designerPage = CreatePage("designerPage", "Designer", "Design your window flows.");
        var codeGenPage = CreatePage("codeGenPage", "Code Generator", "Generate files for each target.");

        BuildWelcomePage(welcomePage);
        BuildDesignerPage(designerPage);
        BuildCodeGeneratorPage(codeGenPage);

        pageControl.Controls.Add(welcomePage);
        pageControl.Controls.Add(designerPage);
        pageControl.Controls.Add(codeGenPage);

        return pageControl;
    }

    private Container CreatePage(string name, string title, string description)
    {
        var page = new Container
        {
            Name = name,
            Dock = DockStyle.Fill,
            Padding = new Thickness(16),
            BackColor = ColorScheme.Surface,
            AutoScroll = true,
            Radius = new Radius(14),
        };

        var header = new Element
        {
            Name = name + "Header",
            Dock = DockStyle.Top,
            Height = 80,
            Text = title + "\n" + description,
            Padding = new Thickness(12),
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            TextAlign = ContentAlignment.MiddleLeft,
            Radius = new Radius(10),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.5f * 255f))
        };

        page.Controls.Add(header);
        return page;
    }

    private void BuildWelcomePage(Container page)
    {
        page.Controls.Add(new Element
        {
            Name = "welcomeInfo",
            Dock = DockStyle.Top,
            Height = 170,
            Text = "Orivy Studio\n\nBu sayfa her zaman sabit home (ilk sayfa) olarak kalir.\nOrivy ile tasarim yeteneklerinizi hizlandirin.",
            Padding = new Thickness(12),
            BackColor = ColorScheme.Primary.WithAlpha((byte)(0.22f * 255f)),
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13f),
            Radius = new Radius(10)
        });
    }

    private void BuildDesignerPage(Container page)
    {
        page.Controls.Add(new Element
        {
            Name = "designerCanvas",
            Dock = DockStyle.Fill,
            Margin = new Thickness(0, 16, 0, 0),
            Text = "Designer canvas (bura disaridan layout edilebilir).",
            BackColor = SKColors.White,
            Border = new Thickness(2),
            BorderColor = ColorScheme.Primary.WithAlpha((byte)(0.45f * 255f)),
            Radius = new Radius(14)
        });
    }

    private void BuildCodeGeneratorPage(Container page)
    {
        var codeGenContainer = new Container
        {
            Name = "codeGeneratorContainer",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Thickness(12)
        };

        for (var i = 0; i < _fileGenerators.Count; i++)
        {
            var g = _fileGenerators[i];
            var item = new Button
            {
                Name = "generatorButton" + i,
                Text = $"{g.Name}: {g.Description}",
                Height = 44,
                Dock = DockStyle.Top,
                Margin = new Thickness(0, 0, 0, 8)
            };

            item.Click += (_, _) => ShowGeneratedCode(g);
            codeGenContainer.Controls.Add(item);
        }

        page.Controls.Add(codeGenContainer);
    }

    private void ShowGeneratedCode(ICodeGenerator generator)
    {
        var generated = generator.GenerateFile(_project);

        var resultWindow = new Window
        {
            Name = "generatedCodePreview",
            Text = $"{generator.Name} Output",
            Width = 900,
            Height = 640,
            MinimumSize = new SKSize(840, 500),
            DwmMargin = 1000
        };

        var textArea = new Element
        {
            Name = "codePreviewArea",
            Dock = DockStyle.Fill,
            BackColor = SKColors.Black,
            ForeColor = SKColors.LimeGreen,
            Text = generated,
            Padding = new Thickness(12),
            TextAlign = ContentAlignment.TopLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas"), 12f)
        };

        resultWindow.Controls.Add(textArea);
        resultWindow.Show();
    }

    private SKImage CreateIconHomeImage()
    {
        using var surface = SKSurface.Create(new SKImageInfo(24, 24));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var backgroundPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = ColorScheme.Primary };
        using var strokePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.9f, Color = SKColors.White };

        canvas.DrawRect(new SKRect(2, 9, 22, 20), backgroundPaint);
        var path = new SKPath();
        path.MoveTo(2, 10);
        path.LineTo(12, 2);
        path.LineTo(22, 10);
        path.Close();
        canvas.DrawPath(path, strokePaint);

        return surface.Snapshot();
    }
}