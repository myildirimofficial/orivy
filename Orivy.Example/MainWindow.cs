using Orivy;
using Orivy.Controls;

namespace Orivy.Example
{
    internal partial class MainWindow : Window
    {
        internal MainWindow()
        {
            InitializeComponent();
            _embeddedTabsPage.ShowTabStripResizer = true;
            ColorScheme.ThemeChanged += OnColorSchemeThemeChanged;
            InitializeBackgroundImageShowcase();
            InitializeBackgroundImageMenu();
            InitializeWindowBackgroundMenu();
        }
    }
}
