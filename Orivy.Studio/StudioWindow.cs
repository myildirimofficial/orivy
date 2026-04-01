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

    // Designer-specific state
    private DesignerSurface? _designerSurface;
    private Element? _designerPropertyDetails;

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

        // Window page tab system (Figma-style sekmeler) için bağlıyoruz.
        WindowPageControl = _pageControl;
        DrawTabIcons = true;
        TabCloseButton = false;

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
        welcomePage.Image = CreateIconHomeImage();
        welcomePage.ImageAlign = ContentAlignment.MiddleLeft;

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
            Text = title,
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
        var heroCard = new Container
        {
            Name = "welcomeHero",
            Dock = DockStyle.Top,
            Height = 180,
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(16),
            BackColor = ColorScheme.SurfaceContainerHigh,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.35f * 255f)),
            Radius = new Radius(12),
            Image = CreateIconHomeImage(),
            ImageAlign = ContentAlignment.MiddleLeft,
            BackgroundImageLayout = ImageLayout.Center
        };

        heroCard.Controls.Add(new Element
        {
            Name = "welcomeHeroText",
            Dock = DockStyle.Fill,
            Text = "Orivy Studio'ya hosgeldiniz!\n\nHome sekmesi sabit kalir ve buradan proje acabilir, designer sekmesine gecebilirsiniz.",
            Padding = new Thickness(4),
            ForeColor = ColorScheme.ForeColor,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 14f),
            TextAlign = ContentAlignment.MiddleLeft
        });

        page.Controls.Add(heroCard);

        page.Controls.Add(new Element
        {
            Name = "welcomeInfo",
            Dock = DockStyle.Top,
            Height = 130,
            Text = "Bu sayfa her zaman sabit home (ilk sayfa) olarak kalir.\nOrivy ile tasarim yeteneklerinizi hizlandirin.",
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
            Name = "designerWelcome",
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Thickness(10),
            BackColor = ColorScheme.SurfaceContainerHigh,
            Text = "Designer modu: şekil ekle, taşı, seçili objeyi düzenle.",
            ForeColor = ColorScheme.ForeColor,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12f),
            Radius = new Radius(10)
        });

        var layoutRoot = new Container
        {
            Name = "designerLayoutRoot",
            Dock = DockStyle.Fill,
            BackColor = ColorScheme.Surface,
            Padding = new Thickness(8)
        };

        page.Controls.Add(layoutRoot);

        var leftTools = new Container
        {
            Name = "designerLeftTools",
            Dock = DockStyle.Left,
            Width = 230,
            Padding = new Thickness(10),
            BackColor = ColorScheme.SurfaceContainerHigh,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.25f * 255f)),
            Radius = new Radius(10)
        };

        var rightProps = new Container
        {
            Name = "designerRightProperties",
            Dock = DockStyle.Right,
            Width = 260,
            Padding = new Thickness(10),
            BackColor = ColorScheme.SurfaceContainerHigh,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.25f * 255f)),
            Radius = new Radius(10)
        };

        _designerSurface = new DesignerSurface
        {
            Name = "designerSurface",
            Dock = DockStyle.Fill,
            BackColor = ColorScheme.Surface,
            Radius = new Radius(12),
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline.WithAlpha((byte)(0.25f * 255f)),
            AutoScroll = true
        };

        layoutRoot.Controls.Add(leftTools);
        layoutRoot.Controls.Add(rightProps);
        layoutRoot.Controls.Add(_designerSurface);

        // Tool panel
        var addRect = new Button
        {
            Name = "addRectButton",
            Text = "Add Rectangle",
            Dock = DockStyle.Top,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 8)
        };

        addRect.Click += (_, _) => AddDesignElement("Rectangle", new SKPoint(40, 40));

        var addText = new Button
        {
            Name = "addTextButton",
            Text = "Add Text",
            Dock = DockStyle.Top,
            Height = 38,
            Margin = new Thickness(0, 0, 0, 8)
        };

        addText.Click += (_, _) => AddDesignElement("Text", new SKPoint(40, 160));

        leftTools.Controls.Add(addText);
        leftTools.Controls.Add(addRect);

        // Property panel
        _designerPropertyDetails = new Element
        {
            Name = "designerPropertyDetails",
            Dock = DockStyle.Fill,
            BackColor = SKColors.Transparent,
            ForeColor = ColorScheme.ForeColor,
            Text = "No object selected.",
            Padding = new Thickness(6),
            TextAlign = ContentAlignment.TopLeft,
            Font = new SKFont(SKTypeface.FromFamilyName("Consolas"), 11f)
        };

        rightProps.Controls.Add(_designerPropertyDetails);

        // Designer surface events
        if (_designerSurface != null)
        {
            _designerSurface.SelectedElementChanged += (_, element) => UpdateDesignerPropertyPanel(element);
            AddDesignElement("WelcomeCard", new SKPoint(46, 40), "Welcome card", 200, 90);
            AddDesignElement("SampleForm", new SKPoint(220, 150), "Compile-ready component", 180, 75);
        }
    }

    private void AddDesignElement(string name, SKPoint location, string text = "Design Item", float width = 140, float height = 80)
    {
        if (_designerSurface == null)
            return;

        var element = new Element
        {
            Name = $"designerItem_{name}_{Guid.NewGuid():N}",
            Text = text,
            Size = new SKSize(width, height),
            Location = location,
            BackColor = ColorScheme.SurfaceContainerHigh,
            ForeColor = ColorScheme.ForeColor,
            Border = new Thickness(1),
            BorderColor = ColorScheme.Outline,
            Radius = new Radius(8),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11f),
            Padding = new Thickness(8),
            Cursor = Cursors.Hand
        };

        _designerSurface.AddDesignItem(element);
        _designerSurface.SelectedItem = element; // select new item
        UpdateDesignerPropertyPanel(element);
    }

    private void UpdateDesignerPropertyPanel(ElementBase? element)
    {
        if (_designerPropertyDetails == null)
            return;

        if (element == null)
        {
            _designerPropertyDetails.Text = "No object selected.";
            return;
        }

        _designerPropertyDetails.Text = $"Name: {element.Name}\n" +
                                       $"Type: {element.GetType().Name}\n" +
                                       $"Text: {element.Text}\n" +
                                       $"Position: {element.Location.X:#0},{element.Location.Y:#0}\n" +
                                       $"Size: {element.Size.Width:#0} x {element.Size.Height:#0}";
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

internal sealed class DesignerSurface : Container
{
    private ElementBase? _selectedElement;
    private bool _isDragging;
    private SKPoint _dragStartSurface;
    private SKPoint _dragStartElementLocation;

    public event EventHandler<ElementBase?>? SelectedElementChanged;

    public ElementBase? SelectedItem
    {
        get => _selectedElement;
        set
        {
            if (_selectedElement == value)
                return;

            if (_selectedElement != null)
            {
                _selectedElement.BorderColor = ColorScheme.Outline;
                _selectedElement.Border = new Thickness(1);
            }

            _selectedElement = value;

            if (_selectedElement != null)
            {
                _selectedElement.BorderColor = ColorScheme.Primary;
                _selectedElement.Border = new Thickness(2);
            }

            SelectedElementChanged?.Invoke(this, _selectedElement);
            Invalidate();
        }
    }

    public DesignerSurface()
    {
        Paint += HandlePaint;
        MouseDown += HandleMouseDown;
        MouseMove += HandleMouseMove;
        MouseUp += HandleMouseUp;
    }

    public void AddDesignItem(Element item)
    {
        item.MouseDown += OnItemMouseDown;
        item.MouseMove += OnItemMouseMove;
        item.MouseUp += OnItemMouseUp;

        item.Border = new Thickness(1);
        item.BorderColor = ColorScheme.Outline;
        item.BackColor = ColorScheme.SurfaceContainerHigh;

        Controls.Add(item);
    }

    private void OnItemMouseDown(object? sender, MouseEventArgs e)
    {
        if (sender is not ElementBase item || e.Button != MouseButtons.Left)
            return;

        SelectedItem = item;

        _dragStartSurface = new SKPoint(item.Location.X + e.X, item.Location.Y + e.Y);
        _dragStartElementLocation = item.Location;
        _isDragging = true;

        e.Handled = true;
    }

    private void OnItemMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging || SelectedItem == null || e.Button != MouseButtons.Left)
            return;

        // local coordinates are relative to the item; compute surface coordinates.
        var currentSurface = new SKPoint(SelectedItem.Location.X + e.X, SelectedItem.Location.Y + e.Y);
        var delta = new SKPoint(currentSurface.X - _dragStartSurface.X, currentSurface.Y - _dragStartSurface.Y);
        SelectedItem.Location = new SKPoint(Math.Max(0, _dragStartElementLocation.X + delta.X), Math.Max(0, _dragStartElementLocation.Y + delta.Y));

        Invalidate();
        e.Handled = true;
    }

    private void OnItemMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isDragging || e.Button != MouseButtons.Left)
            return;

        _isDragging = false;
        e.Handled = true;
    }

    private void HandlePaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        const float spacing = 24;
        using var gridPaint = new SKPaint
        {
            Color = ColorScheme.Outline.WithAlpha((byte)(0.15f * 255f)),
            StrokeWidth = 1,
            IsAntialias = false
        };

        for (var x = 0f; x < Width; x += spacing)
            canvas.DrawLine(x, 0, x, Height, gridPaint);

        for (var y = 0f; y < Height; y += spacing)
            canvas.DrawLine(0, y, Width, y, gridPaint);

        if (SelectedItem != null)
        {
            var bounds = SelectedItem.Bounds;
            using var outlinePaint = new SKPaint
            {
                Color = ColorScheme.Primary.WithAlpha((byte)(0.65f * 255f)),
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            canvas.DrawRect(bounds, outlinePaint);
        }
    }

    private void HandleMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        // Deselect when clicking on empty canvas
        if (e.X < 0 || e.Y < 0)
            return;

        SelectedItem = null;
    }

    private void HandleMouseMove(object? sender, MouseEventArgs e)
    {
        // no-op; element drag handled by each item.
    }

    private void HandleMouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }
}
