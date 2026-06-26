using Orivy;

namespace Orivy.Example;

internal partial class MainWindow
{
    public override void  Dispose(bool disposing)
    {
        if (disposing)
        {
            ColorScheme.ThemeChanged -= OnColorSchemeThemeChanged;

            if (_backgroundHero != null)
            {
                _backgroundHero.BackgroundImageChanged -= BackgroundHero_BackgroundImageChanged;
                _backgroundHero.BackgroundImageCaptionChanged -= BackgroundHero_BackgroundImageCaptionChanged;
            }

            for (var i = 0; i < _backgroundSlides.Count; i++)
                _backgroundSlides[i].Image.Dispose();
            _backgroundSlides.Clear();

            for (var i = 0; i < _exampleImages.Count; i++)
                _exampleImages[i].Dispose();
            _exampleImages.Clear();
        }

        base.Dispose(disposing);
    }
}