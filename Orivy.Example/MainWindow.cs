using Orivy;
using Orivy.Animation;
using Orivy.Binding;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Orivy.Example
{
    internal partial class MainWindow : Window
    {
        private const int BackgroundAssetMaxWidth = 1600;
        private const int BackgroundAssetMaxHeight = 900;

        private enum GridListIconKind
        {
            Healthy,
            Warning,
            Locked,
            Pulse
        }

        private enum WindowBackgroundMode
        {
            Normal,
            Slide
        }

        private readonly Dictionary<WindowPageTransitionEffect, List<MenuItem>> _transitionMenuItems = new();
        private readonly Dictionary<AnimationType, List<MenuItem>> _transitionEasingMenuItems = new();
        private readonly Dictionary<int, List<MenuItem>> _transitionSpeedMenuItems = new();
        private readonly Dictionary<ImageLayout, List<MenuItem>> _backgroundLayoutMenuItems = new();
        private readonly Dictionary<BackgroundImageTransitionEffect, List<MenuItem>> _backgroundEffectMenuItems = new();
        private readonly Dictionary<BackgroundImageCaptionDesignMode, List<MenuItem>> _backgroundCaptionDesignMenuItems = new();
        private readonly Dictionary<int, List<MenuItem>> _backgroundDurationMenuItems = new();
        private readonly Dictionary<int, List<MenuItem>> _backgroundIntervalMenuItems = new();
        private readonly Dictionary<WindowBackgroundMode, List<MenuItem>> _windowBackgroundModeMenuItems = new();
        private readonly Dictionary<int, List<MenuItem>> _windowBackgroundBlurAmountMenuItems = new();
        private readonly Dictionary<BackgroundImageBlurMode, List<MenuItem>> _windowBackgroundBlurModeMenuItems = new();
        private readonly Dictionary<WindowThemeType, List<MenuItem>> _windowThemeMenuItems = new();
        private readonly Dictionary<bool, List<MenuItem>> _windowThemeModeMenuItems = new();
        private readonly List<MenuItem> _titleBarMenuPlacementItems = new();
        private readonly List<MenuItem> _embeddedTabStripResizerItems = new();
        private readonly BindingDemoViewModel _bindingDemoViewModel = new();
        private readonly List<SKImage> _gridListImages = new();
        private readonly List<BackgroundImageFrame> _backgroundSlides = new();
        private Container? _bindingPanel;
        private Container? _backgroundPanel;
        private Container? _backgroundHero;
        private Element? _backgroundHeroCaption;
        private Element? _backgroundStatusCard;
        private Button? _backgroundPlayPauseButton;
        private bool _dangerModeEnabled;
        private int _transitionDurationPreset = 350;
        private int _backgroundTransitionDurationPreset = 420;
        private int _backgroundIntervalPreset = 2600;
        private int _windowBackgroundBlurAmountPreset;
        private bool _windowBackgroundEnabled = false;
        private WindowBackgroundMode _windowBackgroundMode = WindowBackgroundMode.Normal;
        private bool _windowThemeModePreset = ColorScheme.IsDarkMode;
        private bool _windowBackgroundSlideInitialized;
        private bool _notificationStackModeEnabled;
        private SKImage? _windowBackgroundNormalImage;
        private NotificationHandle? _manualProgressToast;
        private MenuItem? _backgroundSlideshowMenuItem;
        private MenuItem? _backgroundRepeatMenuItem;
        private MenuItem? _windowBackgroundEnabledMenuItem;
        private WindowPageControl? _embeddedPageControl;

        internal MainWindow()
        {
            InitializeComponent();
            SetEmbeddedTabStripResizerVisible(true);
            ColorScheme.ThemeChanged += OnColorSchemeThemeChanged;
            RefreshNotificationStackModeButton();
            InitializeBackgroundImageShowcase();
            InitializeBackgroundImageMenu();
            InitializeWindowBackgroundMenu();
        }

        private void InitializeTransitionMenu(MenuItem rootItem)
        {
            var effects = (WindowPageTransitionEffect[])Enum.GetValues(typeof(WindowPageTransitionEffect));
            foreach (var effect in effects)
            {
                var menuItem = rootItem.AddMenuItem(effect.ToString(), (_, _) => SetTransitionEffect(effect));
                RegisterEffectItem(menuItem, effect);
            }

            rootItem.AddSeparator();

            var animationType = (AnimationType[])Enum.GetValues(typeof(AnimationType));

            // ── Easing ─────────────────────────────────────────────────────────────
            var easingMenu = rootItem.AddMenuItem("Easing");
            
            foreach (var type in animationType)
            {
                var menuItem = easingMenu.AddMenuItem(type.ToString(), (_, _) => SetTransitionAnimationType(type));
                RegisterEasingItem(menuItem, type);
            }

            rootItem.AddSeparator();

            // ── Speed ──────────────────────────────────────────────────────────────
            var speedMenu = rootItem.AddMenuItem("Speed");
            RegisterSpeedItem(speedMenu.AddMenuItem("Fast  (100 ms)", (_, _) => SetTransitionDuration(100)), 100);
            RegisterSpeedItem(speedMenu.AddMenuItem("Normal  (250 ms)", (_, _) => SetTransitionDuration(250)), 250);
            RegisterSpeedItem(speedMenu.AddMenuItem("Comfortable  (350 ms)", (_, _) => SetTransitionDuration(350)), 350);
            RegisterSpeedItem(speedMenu.AddMenuItem("Relaxed  (500 ms)", (_, _) => SetTransitionDuration(500)), 500);
            RegisterSpeedItem(speedMenu.AddMenuItem("Cinematic  (1 s)", (_, _) => SetTransitionDuration(1000)), 1000);

            RefreshTransitionMenuChecks();
            RefreshTransitionEasingMenuChecks();
            RefreshTransitionSpeedMenuChecks();
        }

        private void RegisterEffectItem(MenuItem item, WindowPageTransitionEffect effect)
        {
            item.CheckOnClick = false;
            if (!_transitionMenuItems.TryGetValue(effect, out var list)) { list = new List<MenuItem>(); _transitionMenuItems[effect] = list; }
            list.Add(item);
        }

        private void RegisterEasingItem(MenuItem item, AnimationType type)
        {
            item.CheckOnClick = false;
            if (!_transitionEasingMenuItems.TryGetValue(type, out var list)) { list = new List<MenuItem>(); _transitionEasingMenuItems[type] = list; }
            list.Add(item);
        }

        private void RegisterSpeedItem(MenuItem item, int ms)
        {
            item.CheckOnClick = false;
            if (!_transitionSpeedMenuItems.TryGetValue(ms, out var list)) { list = new List<MenuItem>(); _transitionSpeedMenuItems[ms] = list; }
            list.Add(item);
        }

        internal void SetTransitionEffect(WindowPageTransitionEffect effect)
        {
            windowPageControl.TransitionEffect = effect;
            RefreshTransitionMenuChecks();
        }

        internal void SetTransitionAnimationType(AnimationType type)
        {
            windowPageControl.TransitionAnimationType = type;
            RefreshTransitionEasingMenuChecks();
        }

        internal void SetTransitionDuration(int ms)
        {
            _transitionDurationPreset = ms;
            windowPageControl.TransitionDurationMs = ms;
            RefreshTransitionSpeedMenuChecks();
        }

        private void RefreshTransitionMenuChecks()
        {
            foreach (var item in _transitionMenuItems)
            {
                var isSelected = item.Key == windowPageControl.TransitionEffect;
                for (var i = 0; i < item.Value.Count; i++)
                    item.Value[i].Checked = isSelected;
            }
        }

        private void RefreshTransitionEasingMenuChecks()
        {
            var current = windowPageControl.TransitionAnimationType;
            foreach (var pair in _transitionEasingMenuItems)
            {
                var isSelected = pair.Key == current;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }
        }

        private void RefreshTransitionSpeedMenuChecks()
        {
            foreach (var pair in _transitionSpeedMenuItems)
            {
                var isSelected = pair.Key == _transitionDurationPreset;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }
        }

        private void InitializeWindowThemeMenu(MenuItem rootItem)
        {
            RegisterWindowThemeItem(rootItem.AddMenuItem("None", (_, _) => SetWindowThemePreset(WindowThemeType.None)), WindowThemeType.None);
            RegisterWindowThemeItem(rootItem.AddMenuItem("Mica", (_, _) => SetWindowThemePreset(WindowThemeType.Mica)), WindowThemeType.Mica);
            RegisterWindowThemeItem(rootItem.AddMenuItem("Acrylic", (_, _) => SetWindowThemePreset(WindowThemeType.Acrylic)), WindowThemeType.Acrylic);
            RegisterWindowThemeItem(rootItem.AddMenuItem("Tabbed", (_, _) => SetWindowThemePreset(WindowThemeType.Tabbed)), WindowThemeType.Tabbed);

            RefreshWindowThemeMenuChecks();
        }

        private void InitializeWindowThemeModeMenu(MenuItem rootItem)
        {
            RegisterWindowThemeModeItem(rootItem.AddMenuItem("Light", (_, _) => SetWindowThemeModePreset(false)), false);
            RegisterWindowThemeModeItem(rootItem.AddMenuItem("Dark", (_, _) => SetWindowThemeModePreset(true)), true);

            RefreshWindowThemeModeMenuChecks();
        }

        private void RegisterWindowThemeItem(MenuItem item, WindowThemeType themeType)
        {
            item.CheckOnClick = false;
            if (!_windowThemeMenuItems.TryGetValue(themeType, out var list))
            {
                list = new List<MenuItem>();
                _windowThemeMenuItems[themeType] = list;
            }

            list.Add(item);
        }

        private void SetWindowThemePreset(WindowThemeType themeType)
        {
            WindowThemeType = themeType;
            RefreshWindowThemeMenuChecks();
        }

        private void RegisterWindowThemeModeItem(MenuItem item, bool isDark)
        {
            item.CheckOnClick = false;
            if (!_windowThemeModeMenuItems.TryGetValue(isDark, out var list))
            {
                list = new List<MenuItem>();
                _windowThemeModeMenuItems[isDark] = list;
            }

            list.Add(item);
        }

        private void SetWindowThemeModePreset(bool isDark)
        {
            _windowThemeModePreset = isDark;
            ColorScheme.IsDarkMode = isDark;
            RefreshWindowThemeModeMenuChecks();
        }

        private void OnColorSchemeThemeChanged(object? sender, EventArgs e)
        {
            RefreshWindowThemeMenuChecks();
            RefreshWindowThemeModeMenuChecks();
        }

        private void RefreshWindowThemeMenuChecks()
        {
            foreach (var pair in _windowThemeMenuItems)
            {
                var isSelected = pair.Key == WindowThemeType;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }
        }

        private void RefreshWindowThemeModeMenuChecks()
        {
            foreach (var pair in _windowThemeModeMenuItems)
            {
                var isSelected = pair.Key == _windowThemeModePreset;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }
        }

        private void InitializeWindowMenu(MenuItem rootItem)
        {
            InitializeWindowThemeMenu(rootItem.AddMenuItem("Theme Type"));
            InitializeWindowThemeModeMenu(rootItem.AddMenuItem("Theme Mode"));
            rootItem.AddSeparator();

            var embedMenuItem = rootItem.AddMenuItem(
                "Embed Menu In Title Bar",
                (_, _) => SetMenuStripEmbeddedInTitleBar(!ReferenceEquals(TitleBarMenuStrip, menuStrip)));
            embedMenuItem.CheckOnClick = false;
            _titleBarMenuPlacementItems.Add(embedMenuItem);

            var resizerMenuItem = rootItem.AddMenuItem(
                "Show Embedded Tab Resizer",
                (_, _) => SetEmbeddedTabStripResizerVisible(!IsEmbeddedTabStripResizerVisible()));
            resizerMenuItem.CheckOnClick = false;
            _embeddedTabStripResizerItems.Add(resizerMenuItem);

            RefreshTitleBarMenuPlacementChecks();
            RefreshEmbeddedTabStripResizerChecks();
        }

        private void SetMenuStripEmbeddedInTitleBar(bool embedded)
        {
            if (menuStrip == null)
                return;

            TitleBarMenuStrip = embedded ? menuStrip : null;
            RefreshTitleBarMenuPlacementChecks();
        }

        private void RefreshTitleBarMenuPlacementChecks()
        {
            var isEmbedded = ReferenceEquals(TitleBarMenuStrip, menuStrip);
            for (var i = 0; i < _titleBarMenuPlacementItems.Count; i++)
                _titleBarMenuPlacementItems[i].Checked = isEmbedded;
        }

        private void SetEmbeddedTabStripResizerVisible(bool visible)
        {
            if (windowPageControl != null)
                windowPageControl.ShowTabStripResizer = visible;

            if (_embeddedPageControl != null)
                _embeddedPageControl.ShowTabStripResizer = visible;

            RefreshEmbeddedTabStripResizerChecks();
        }

        private bool IsEmbeddedTabStripResizerVisible()
        {
            if (_embeddedPageControl != null)
                return _embeddedPageControl.ShowTabStripResizer;

            return windowPageControl != null && windowPageControl.ShowTabStripResizer;
        }

        private void RefreshEmbeddedTabStripResizerChecks()
        {
            var isVisible = IsEmbeddedTabStripResizerVisible();
            for (var i = 0; i < _embeddedTabStripResizerItems.Count; i++)
                _embeddedTabStripResizerItems[i].Checked = isVisible;
        }

        private void InitializeBackgroundImageMenu()
        {
            if (menuStrip == null || _backgroundHero == null)
                return;

            var backgroundsMenu = menuStrip.AddMenuItem("Backgrounds");

            _backgroundSlideshowMenuItem = backgroundsMenu.AddMenuItem("Slideshow Active");
            _backgroundSlideshowMenuItem.CheckOnClick = true;
            _backgroundSlideshowMenuItem.Checked = _backgroundHero.BackgroundImageSlideshowEnabled;
            _backgroundSlideshowMenuItem.CheckedChanged += (_, _) => SetBackgroundSlideshowEnabled(_backgroundSlideshowMenuItem.Checked);

            _backgroundRepeatMenuItem = backgroundsMenu.AddMenuItem("Repeat Active");
            _backgroundRepeatMenuItem.CheckOnClick = true;
            _backgroundRepeatMenuItem.Checked = _backgroundHero.BackgroundImageSlideshowRepeat;
            _backgroundRepeatMenuItem.CheckedChanged += (_, _) => SetBackgroundRepeatEnabled(_backgroundRepeatMenuItem.Checked);

            backgroundsMenu.AddSeparator();

            var layoutMenu = backgroundsMenu.AddMenuItem("Layout");
            foreach (ImageLayout layout in Enum.GetValues(typeof(ImageLayout)))
            {
                var item = layoutMenu.AddMenuItem(layout.ToString(), (_, _) => SetBackgroundLayout(layout));
                RegisterBackgroundLayoutItem(item, layout);
            }

            var effectMenu = backgroundsMenu.AddMenuItem("Effect");
            foreach (BackgroundImageTransitionEffect effect in Enum.GetValues(typeof(BackgroundImageTransitionEffect)))
            {
                var item = effectMenu.AddMenuItem(effect.ToString(), (_, _) => SetBackgroundEffect(effect));
                RegisterBackgroundEffectItem(item, effect);
            }

            var captionMenu = backgroundsMenu.AddMenuItem("Caption");
            var captionDesignMenu = captionMenu.AddMenuItem("Design");
            foreach (BackgroundImageCaptionDesignMode mode in Enum.GetValues(typeof(BackgroundImageCaptionDesignMode)))
            {
                var item = captionDesignMenu.AddMenuItem(mode.ToString(), (_, _) => SetBackgroundCaptionDesignMode(mode));
                RegisterBackgroundCaptionDesignItem(item, mode);
            }

            var durationMenu = backgroundsMenu.AddMenuItem("Duration");
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Instant  (0 ms)", (_, _) => SetBackgroundTransitionDuration(0)), 0);
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Fast  (180 ms)", (_, _) => SetBackgroundTransitionDuration(180)), 180);
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Balanced  (420 ms)", (_, _) => SetBackgroundTransitionDuration(420)), 420);
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Slow  (700 ms)", (_, _) => SetBackgroundTransitionDuration(700)), 700);
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Very Slow  (1400 ms)", (_, _) => SetBackgroundTransitionDuration(1400)), 1400);
            RegisterBackgroundDurationItem(durationMenu.AddMenuItem("Extremely Slow  (2800 ms)", (_, _) => SetBackgroundTransitionDuration(2800)), 2800);

            var intervalMenu = backgroundsMenu.AddMenuItem("Interval");
            RegisterBackgroundIntervalItem(intervalMenu.AddMenuItem("Fast  (1.6 s)", (_, _) => SetBackgroundInterval(1600)), 1600);
            RegisterBackgroundIntervalItem(intervalMenu.AddMenuItem("Normal  (2.6 s)", (_, _) => SetBackgroundInterval(2600)), 2600);
            RegisterBackgroundIntervalItem(intervalMenu.AddMenuItem("Slow  (4 s)", (_, _) => SetBackgroundInterval(4000)), 4000);

            RefreshBackgroundMenuChecks();
        }

        private void RegisterBackgroundLayoutItem(MenuItem item, ImageLayout layout)
        {
            item.CheckOnClick = false;
            if (!_backgroundLayoutMenuItems.TryGetValue(layout, out var list))
            {
                list = new List<MenuItem>();
                _backgroundLayoutMenuItems[layout] = list;
            }

            list.Add(item);
        }

        private void RegisterBackgroundEffectItem(MenuItem item, BackgroundImageTransitionEffect effect)
        {
            item.CheckOnClick = false;
            if (!_backgroundEffectMenuItems.TryGetValue(effect, out var list))
            {
                list = new List<MenuItem>();
                _backgroundEffectMenuItems[effect] = list;
            }

            list.Add(item);
        }

        private void RegisterBackgroundCaptionDesignItem(MenuItem item, BackgroundImageCaptionDesignMode mode)
        {
            item.CheckOnClick = false;
            if (!_backgroundCaptionDesignMenuItems.TryGetValue(mode, out var list))
            {
                list = new List<MenuItem>();
                _backgroundCaptionDesignMenuItems[mode] = list;
            }

            list.Add(item);
        }

        private void RegisterBackgroundDurationItem(MenuItem item, int durationMs)
        {
            item.CheckOnClick = false;
            if (!_backgroundDurationMenuItems.TryGetValue(durationMs, out var list))
            {
                list = new List<MenuItem>();
                _backgroundDurationMenuItems[durationMs] = list;
            }

            list.Add(item);
        }

        private void RegisterBackgroundIntervalItem(MenuItem item, int intervalMs)
        {
            item.CheckOnClick = false;
            if (!_backgroundIntervalMenuItems.TryGetValue(intervalMs, out var list))
            {
                list = new List<MenuItem>();
                _backgroundIntervalMenuItems[intervalMs] = list;
            }

            list.Add(item);
        }

        private void RefreshBackgroundMenuChecks()
        {
            if (_backgroundHero == null)
                return;

            if (_backgroundSlideshowMenuItem != null)
                _backgroundSlideshowMenuItem.Checked = _backgroundHero.BackgroundImageSlideshowEnabled;

            if (_backgroundRepeatMenuItem != null)
                _backgroundRepeatMenuItem.Checked = _backgroundHero.BackgroundImageSlideshowRepeat;

            foreach (var pair in _backgroundLayoutMenuItems)
            {
                var isSelected = pair.Key == _backgroundHero.BackgroundImageLayout;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _backgroundEffectMenuItems)
            {
                var isSelected = pair.Key == _backgroundHero.BackgroundImageTransitionEffect;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _backgroundCaptionDesignMenuItems)
            {
                var isSelected = pair.Key == _backgroundHero.CurrentBackgroundImageCaptionDesignMode;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _backgroundDurationMenuItems)
            {
                var isSelected = pair.Key == _backgroundTransitionDurationPreset;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _backgroundIntervalMenuItems)
            {
                var isSelected = pair.Key == _backgroundIntervalPreset;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            RefreshWindowBackgroundMenuChecks();
        }

        private void SetBackgroundSlideshowEnabled(bool enabled)
        {
            if (_backgroundHero == null)
                return;

            _backgroundHero.BackgroundImageSlideshowEnabled = enabled;
            UpdateBackgroundShowcaseStatus();
        }

        private void SetBackgroundRepeatEnabled(bool enabled)
        {
            if (_backgroundHero == null)
                return;

            _backgroundHero.BackgroundImageSlideshowRepeat = enabled;
            UpdateBackgroundShowcaseStatus();
        }

        private void SetBackgroundLayout(ImageLayout layout)
        {
            if (_backgroundHero == null)
                return;

            _backgroundHero.BackgroundImageLayout = layout;
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void SetBackgroundEffect(BackgroundImageTransitionEffect effect)
        {
            if (_backgroundHero == null)
                return;

            _backgroundHero.BackgroundImageTransitionEffect = effect;
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void SetBackgroundCaptionDesignMode(BackgroundImageCaptionDesignMode mode)
        {
            if (_backgroundHero == null || _backgroundSlides.Count == 0)
                return;

            _backgroundHero.BackgroundImageCaptionDesignMode = mode;
        }

        private void SetBackgroundTransitionDuration(int durationMs)
        {
            if (_backgroundHero == null)
                return;

            _backgroundTransitionDurationPreset = durationMs;
            _backgroundHero.BackgroundImageTransitionDurationMs = durationMs;
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void SetBackgroundInterval(int intervalMs)
        {
            if (_backgroundHero == null)
                return;

            _backgroundIntervalPreset = intervalMs;
            _backgroundHero.BackgroundImageSlideshowIntervalMs = intervalMs;
            UpdateBackgroundShowcaseStatus();
        }

        private void InitializeWindowBackgroundMenu()
        {
            if (menuStrip == null)
                return;

            var windowBackgroundMenu = menuStrip.AddMenuItem("Window Background");

            _windowBackgroundEnabledMenuItem = windowBackgroundMenu.AddMenuItem("Active");
            _windowBackgroundEnabledMenuItem.CheckOnClick = true;
            _windowBackgroundEnabledMenuItem.Checked = _windowBackgroundEnabled;
            _windowBackgroundEnabledMenuItem.CheckedChanged += (_, _) => SetWindowBackgroundEnabled(_windowBackgroundEnabledMenuItem.Checked);

            windowBackgroundMenu.AddSeparator();

            var modeMenu = windowBackgroundMenu.AddMenuItem("Mode");
            var normalItem = modeMenu.AddMenuItem("Normal", (_, _) => SetWindowBackgroundMode(WindowBackgroundMode.Normal));
            RegisterWindowBackgroundModeItem(normalItem, WindowBackgroundMode.Normal);
            var slideItem = modeMenu.AddMenuItem("Slide", (_, _) => SetWindowBackgroundMode(WindowBackgroundMode.Slide));
            RegisterWindowBackgroundModeItem(slideItem, WindowBackgroundMode.Slide);

            var blurMenu = windowBackgroundMenu.AddMenuItem("Blur");
            var blurAmountMenu = blurMenu.AddMenuItem("Amount");
            RegisterWindowBackgroundBlurAmountItem(blurAmountMenu.AddMenuItem("Off  (0 px)", (_, _) => SetWindowBackgroundBlurAmount(0)), 0);
            RegisterWindowBackgroundBlurAmountItem(blurAmountMenu.AddMenuItem("Soft  (4 px)", (_, _) => SetWindowBackgroundBlurAmount(4)), 4);
            RegisterWindowBackgroundBlurAmountItem(blurAmountMenu.AddMenuItem("Balanced  (8 px)", (_, _) => SetWindowBackgroundBlurAmount(8)), 8);
            RegisterWindowBackgroundBlurAmountItem(blurAmountMenu.AddMenuItem("Strong  (14 px)", (_, _) => SetWindowBackgroundBlurAmount(14)), 14);
            RegisterWindowBackgroundBlurAmountItem(blurAmountMenu.AddMenuItem("Heavy  (22 px)", (_, _) => SetWindowBackgroundBlurAmount(22)), 22);

            var blurModeMenu = blurMenu.AddMenuItem("Mode");
            foreach (BackgroundImageBlurMode mode in Enum.GetValues(typeof(BackgroundImageBlurMode)))
            {
                var item = blurModeMenu.AddMenuItem(mode.ToString(), (_, _) => SetWindowBackgroundBlurMode(mode));
                RegisterWindowBackgroundBlurModeItem(item, mode);
            }

            RefreshWindowBackgroundMenuChecks();
        }

        private void RegisterWindowBackgroundModeItem(MenuItem item, WindowBackgroundMode mode)
        {
            item.CheckOnClick = false;
            if (!_windowBackgroundModeMenuItems.TryGetValue(mode, out var list))
            {
                list = new List<MenuItem>();
                _windowBackgroundModeMenuItems[mode] = list;
            }

            list.Add(item);
        }

        private void RegisterWindowBackgroundBlurAmountItem(MenuItem item, int amount)
        {
            item.CheckOnClick = false;
            if (!_windowBackgroundBlurAmountMenuItems.TryGetValue(amount, out var list))
            {
                list = new List<MenuItem>();
                _windowBackgroundBlurAmountMenuItems[amount] = list;
            }

            list.Add(item);
        }

        private void RegisterWindowBackgroundBlurModeItem(MenuItem item, BackgroundImageBlurMode mode)
        {
            item.CheckOnClick = false;
            if (!_windowBackgroundBlurModeMenuItems.TryGetValue(mode, out var list))
            {
                list = new List<MenuItem>();
                _windowBackgroundBlurModeMenuItems[mode] = list;
            }

            list.Add(item);
        }

        private void RefreshWindowBackgroundMenuChecks()
        {
            if (_windowBackgroundEnabledMenuItem != null)
                _windowBackgroundEnabledMenuItem.Checked = _windowBackgroundEnabled;

            foreach (var pair in _windowBackgroundModeMenuItems)
            {
                var isSelected = pair.Key == _windowBackgroundMode;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _windowBackgroundBlurAmountMenuItems)
            {
                var isSelected = pair.Key == _windowBackgroundBlurAmountPreset;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }

            foreach (var pair in _windowBackgroundBlurModeMenuItems)
            {
                var isSelected = pair.Key == BackgroundImageBlurMode;
                for (var i = 0; i < pair.Value.Count; i++)
                    pair.Value[i].Checked = isSelected;
            }
        }

        private void SetWindowBackgroundEnabled(bool enabled)
        {
            if (_windowBackgroundEnabled == enabled)
            {
                RefreshWindowBackgroundMenuChecks();
                return;
            }

            _windowBackgroundEnabled = enabled;
            if (enabled && _windowBackgroundMode == WindowBackgroundMode.Normal)
                CaptureWindowBackgroundNormalImage();

            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void SetWindowBackgroundBlurAmount(int amount)
        {
            var normalizedAmount = Math.Max(0, amount);
            _windowBackgroundBlurAmountPreset = normalizedAmount;
            BackgroundImageBlurAmount = normalizedAmount;
            RefreshWindowBackgroundMenuChecks();
            UpdateBackgroundShowcaseStatus();
            Invalidate();
        }

        private void SetWindowBackgroundBlurMode(BackgroundImageBlurMode mode)
        {
            if (BackgroundImageBlurMode == mode)
            {
                RefreshWindowBackgroundMenuChecks();
                return;
            }

            BackgroundImageBlurMode = mode;
            RefreshWindowBackgroundMenuChecks();
            UpdateBackgroundShowcaseStatus();
            Invalidate();
        }

        private void SetWindowBackgroundMode(WindowBackgroundMode mode)
        {
            if (_windowBackgroundMode == mode)
            {
                if (mode == WindowBackgroundMode.Normal)
                {
                    CaptureWindowBackgroundNormalImage();
                    SyncWindowBackgroundWithShowcase();
                    UpdateBackgroundShowcaseStatus();
                }

                RefreshWindowBackgroundMenuChecks();
                return;
            }

            _windowBackgroundMode = mode;
            if (mode == WindowBackgroundMode.Normal)
                CaptureWindowBackgroundNormalImage();

            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void CaptureWindowBackgroundNormalImage()
        {
            if (_backgroundHero == null || _backgroundSlides.Count == 0)
                return;

            var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
            var activeFrame = _backgroundHero.CurrentBackgroundImageFrame ?? _backgroundSlides[index];
            _windowBackgroundNormalImage = activeFrame.Image;
        }

        private void SyncWindowBackgroundWithShowcase()
        {
            if (!_windowBackgroundEnabled || _backgroundHero == null || _backgroundSlides.Count == 0)
            {
                _windowBackgroundSlideInitialized = false;
                BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.None;
                BackgroundImageTransitionDurationMs = 0;
                BackgroundImages = Array.Empty<BackgroundImageFrame>();
                BackgroundImage = null;
                BackgroundImageSlideshowEnabled = false;
                BackgroundImageSlideshowRepeat = false;
                return;
            }

            var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
            var activeFrame = _backgroundHero.CurrentBackgroundImageFrame ?? _backgroundSlides[index];

            BackgroundImageLayout = _backgroundHero.BackgroundImageLayout;
            if (_windowBackgroundMode == WindowBackgroundMode.Slide)
            {
                var slideEffect = _backgroundSlides.Count > 1
                    ? _backgroundHero.BackgroundImageTransitionEffect
                    : BackgroundImageTransitionEffect.None;
                var slideDuration = _backgroundSlides.Count > 1
                    ? _backgroundHero.BackgroundImageTransitionDurationMs
                    : 0;

                if (!_windowBackgroundSlideInitialized)
                {
                    BackgroundImage = null;
                    BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.None;
                    BackgroundImageTransitionDurationMs = 0;
                    BackgroundImages = _backgroundSlides.ToArray();
                    BackgroundImageIndex = index;
                    _windowBackgroundSlideInitialized = true;
                }

                BackgroundImageTransitionEffect = slideEffect;
                BackgroundImageTransitionDurationMs = slideDuration;

                if (BackgroundImageIndex != index)
                    BackgroundImageIndex = index;
            }
            else
            {
                _windowBackgroundSlideInitialized = false;
                if (_windowBackgroundNormalImage == null)
                    CaptureWindowBackgroundNormalImage();

                BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.None;
                BackgroundImageTransitionDurationMs = 0;
                BackgroundImages = Array.Empty<BackgroundImageFrame>();
                BackgroundImage = _windowBackgroundNormalImage ?? activeFrame.Image;
            }

            BackgroundImageSlideshowEnabled = false;
            BackgroundImageSlideshowRepeat = false;
        }

        private void InitializeBackgroundImageShowcase()
        {
            if (windowPageControl == null)
                return;

            _backgroundPanel = new Container
            {
                Name = "backgroundPanel",
                Text = "Backgrounds",
                Dock = DockStyle.Fill,
                Padding = new Thickness(28),
                Radius = new Radius(0),
                Border = new Thickness(0),
            };

            var tabIcon = CreateGridListIcon(new SKColor(0x06, 0xB6, 0xD4), GridListIconKind.Pulse);
            _backgroundPanel.Image = tabIcon;

            var header = new Element
            {
                Dock = DockStyle.Top,
                Height = 92,
                Margin = new Thickness(0, 0, 0, 18),
                Padding = new Thickness(18),
                BackColor = SKColors.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Background Image Showcase\nAsset-backed imagery, caption metadata, transition duration, slideshow interval and repeat behavior are all driven directly by ElementBase.",
            };

            _backgroundHero = new Container
            {
                Name = "backgroundHero",
                Dock = DockStyle.Top,
                Height = 292,
                BackColor = SKColors.Transparent,
                Margin = new Thickness(0, 0, 0, 18),
                Padding = new Thickness(18),
                Radius = new Radius(22),
                Border = new Thickness(0),
                BorderColor = ColorScheme.BackColor.WithAlpha(25),
                BackgroundImageLayout = ImageLayout.Zoom,
                BackgroundImageTransitionEffect = BackgroundImageTransitionEffect.ScaleFade,
                BackgroundImageTransitionDurationMs = _backgroundTransitionDurationPreset,
                BackgroundImageSlideshowEnabled = true,
                BackgroundImageSlideshowRepeat = true,
                BackgroundImageSlideshowIntervalMs = _backgroundIntervalPreset,
            };

            _backgroundHero.ConfigureVisualStyles(s => s.Base(b => b
                .Background(ColorScheme.SurfaceContainerHigh)
                .BorderColor(ColorScheme.Outline.WithAlpha(110))
                .Radius(22)
                .Shadow(new BoxShadow(0f, 14f, 28f, 0, ColorScheme.ShadowColor.WithAlpha(30)))));

            _backgroundHeroCaption = new Element
            {
                Name = "backgroundHeroCaption",
                Dock = DockStyle.Bottom,
                Height = 108,
                Padding = new Thickness(16),
                Radius = new Radius(18),
                Border = new Thickness(1),
                BackColor = SKColors.Black.WithAlpha(118),
                BorderColor = SKColors.White.WithAlpha(42),
                ForeColor = SKColors.White,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _backgroundHero.Controls.Add(_backgroundHeroCaption);

            var actionRow = new Container
            {
                Dock = DockStyle.Top,
                Height = 46,
                Margin = new Thickness(0, 0, 0, 14),
                Radius = new Radius(0),
                Border = new Thickness(0),
                BackColor = SKColors.Transparent,
            };

            var previousButton = new Button
            {
                Text = "Previous",
                Dock = DockStyle.Left,
                Width = 128,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 0),
            };
            previousButton.Click += BackgroundPreviousButton_Click;

            _backgroundPlayPauseButton = new Button
            {
                Text = "Pause Slideshow",
                Dock = DockStyle.Left,
                Width = 168,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 0),
            };
            _backgroundPlayPauseButton.Click += BackgroundPlayPauseButton_Click;

            var nextButton = new Button
            {
                Text = "Next",
                Dock = DockStyle.Left,
                Width = 112,
                Height = 38,
            };
            nextButton.Click += BackgroundNextButton_Click;

            actionRow.Controls.Add(previousButton);
            actionRow.Controls.Add(_backgroundPlayPauseButton);
            actionRow.Controls.Add(nextButton);

            _backgroundStatusCard = new Element
            {
                Name = "backgroundStatusCard",
                Dock = DockStyle.Top,
                Height = 108,
                Padding = new Thickness(18),
                Radius = new Radius(18),
                Border = new Thickness(1),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _backgroundStatusCard.ConfigureVisualStyles(s => s.Base(b => b
                .Background(ColorScheme.SurfaceContainer)
                .Foreground(ColorScheme.ForeColor)
                .BorderColor(ColorScheme.Outline.WithAlpha(94))
                .Radius(18)));

            _backgroundPanel.Controls.Add(_backgroundStatusCard);
            _backgroundPanel.Controls.Add(actionRow);
            _backgroundPanel.Controls.Add(_backgroundHero);
            _backgroundPanel.Controls.Add(header);

            var slides = LoadBackgroundShowcaseSlides();
            for (var i = 0; i < slides.Count; i++)
                _backgroundSlides.Add(slides[i]);

            if (_backgroundSlides.Count > 0)
                _backgroundHero.BackgroundImages = _backgroundSlides.ToArray();

            _backgroundHero.BackgroundImageChanged += BackgroundHero_BackgroundImageChanged;
            _backgroundHero.BackgroundImageCaptionChanged += BackgroundHero_BackgroundImageCaptionChanged;

            windowPageControl.Controls.Add(_backgroundPanel);
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private List<BackgroundImageFrame> LoadBackgroundShowcaseSlides()
        {
            var assetDirectory = Path.Combine(AppContext.BaseDirectory, "assets", "images");
            if (Directory.Exists(assetDirectory))
            {
                var candidateFiles = Directory.GetFiles(assetDirectory, "*.*", SearchOption.TopDirectoryOnly);
                Array.Sort(candidateFiles, StringComparer.OrdinalIgnoreCase);

                var assetSlides = new List<BackgroundImageFrame>(candidateFiles.Length);
                for (var i = 0; i < candidateFiles.Length; i++)
                {
                    if (!IsSupportedBackgroundImageAsset(candidateFiles[i]))
                        continue;

                    var image = LoadBackgroundShowcaseAssetImage(candidateFiles[i]);
                    if (image == null)
                        continue;

                    assetSlides.Add(CreateBackgroundShowcaseFrame(image, candidateFiles[i], assetSlides.Count));
                }

                if (assetSlides.Count > 0)
                    return assetSlides;
            }

            return new List<BackgroundImageFrame>();
        }

        private static bool IsSupportedBackgroundImageAsset(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static SKImage? LoadBackgroundShowcaseAssetImage(string path)
        {
            var sourceImage = SKImage.FromEncodedData(path);
            if (sourceImage == null)
                return null;

            var targetSize = GetBackgroundAssetTargetSize(sourceImage.Width, sourceImage.Height);
            if (targetSize.Width >= sourceImage.Width && targetSize.Height >= sourceImage.Height)
                return sourceImage;

            using (sourceImage)
            using (var surface = SKSurface.Create(new SKImageInfo(targetSize.Width, targetSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                if (surface == null)
                    return null;

                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.DrawImage(sourceImage, SKRect.Create(targetSize.Width, targetSize.Height));
                return surface.Snapshot();
            }
        }

        private static SKSizeI GetBackgroundAssetTargetSize(int sourceWidth, int sourceHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                return new SKSizeI(1, 1);

            var scale = Math.Min(
                1f,
                Math.Min(
                    BackgroundAssetMaxWidth / (float)sourceWidth,
                    BackgroundAssetMaxHeight / (float)sourceHeight));

            var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return new SKSizeI(width, height);
        }

        private static BackgroundImageFrame CreateBackgroundShowcaseFrame(SKImage image, string imagePath, int index)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            return fileName.ToLowerInvariant() switch
            {
                "bg1" => new BackgroundImageFrame(
                    image,
                    new BackgroundImageCaption(
                        "Gallery Entrance",
                        "Warm highlights and layered foreground depth establish the opening scene for the asset-backed slideshow.\n\nThe caption is now defined directly in code, so sample content stays deterministic."),
                    ContentAlignment.MiddleLeft,
                    BackgroundImageCaptionDesignMode.Overlay),
                "bg2" => new BackgroundImageFrame(
                    image,
                    new BackgroundImageCaption(
                        "Material Study",
                        "A denser frame helps verify caption overlays and transition pacing against a real-world composition.\n\nGlass mode keeps the panel readable while still preserving the image beneath it."),
                    ContentAlignment.MiddleCenter,
                    BackgroundImageCaptionDesignMode.Glass),
                "bg3" => new BackgroundImageFrame(
                    image,
                    new BackgroundImageCaption(
                        "Studio Corridor",
                        "Long horizontal structure makes slide motion and zoom cropping easier to judge.\n\nSolid mode gives stronger separation for brighter photography and busier image regions."),
                    ContentAlignment.MiddleRight,
                    BackgroundImageCaptionDesignMode.Solid),
                "bg4" => new BackgroundImageFrame(
                    image,
                    new BackgroundImageCaption(
                        "Ambient Lounge",
                        "Soft contrast is useful when checking readability of caption and summary text over photos.\n\nMinimal mode keeps the content light and unobtrusive while preserving more of the scene."),
                    ContentAlignment.BottomLeft,
                    BackgroundImageCaptionDesignMode.Minimal),
                "bg5" => new BackgroundImageFrame(
                    image,
                    new BackgroundImageCaption(
                        "Night Passage",
                        "The final frame gives repeat mode a clear end-state before the slideshow loops back to the start.\n\nHidden mode suppresses the caption panel entirely so the image can stand on its own."),
                    ContentAlignment.BottomRight,
                    BackgroundImageCaptionDesignMode.Hidden),
                _ => new BackgroundImageFrame(
                    image,
                    CreateDefaultBackgroundCaption(imagePath, index),
                    GetDefaultBackgroundCaptionLayout(index),
                    GetDefaultBackgroundCaptionDesignMode(index))
            };
        }

        private static BackgroundImageCaptionDesignMode GetDefaultBackgroundCaptionDesignMode(int index)
        {
            return (index % 4) switch
            {
                1 => BackgroundImageCaptionDesignMode.Glass,
                2 => BackgroundImageCaptionDesignMode.Solid,
                3 => BackgroundImageCaptionDesignMode.Minimal,
                _ => BackgroundImageCaptionDesignMode.Overlay,
            };
        }

        private static ContentAlignment GetDefaultBackgroundCaptionLayout(int index)
        {
            return (index % 4) switch
            {
                1 => ContentAlignment.MiddleCenter,
                2 => ContentAlignment.MiddleRight,
                3 => ContentAlignment.BottomLeft,
                _ => ContentAlignment.MiddleLeft,
            };
        }

        private static BackgroundImageCaption CreateDefaultBackgroundCaption(string imagePath, int index)
        {
            return new BackgroundImageCaption($"Scene {index + 1}", $"Loaded from assets/images/{Path.GetFileName(imagePath)}.");
        }

        private void BackgroundHero_BackgroundImageChanged(object? sender, EventArgs e)
        {
            SyncActiveBackgroundFrameFromHero();
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void BackgroundHero_BackgroundImageCaptionChanged(object? sender, EventArgs e)
        {
            SyncActiveBackgroundFrameFromHero();
            SyncWindowBackgroundWithShowcase();
            UpdateBackgroundShowcaseStatus();
        }

        private void SyncActiveBackgroundFrameFromHero()
        {
            if (_backgroundHero == null || _backgroundSlides.Count == 0)
                return;

            var activeFrame = _backgroundHero.CurrentBackgroundImageFrame;
            if (activeFrame == null)
                return;

            var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
            _backgroundSlides[index] = activeFrame;
        }

        private void ApplyBackgroundCaptionVisuals(BackgroundImageFrame activeFrame)
        {
            if (_backgroundHeroCaption == null)
                return;

            var hasCaption = !activeFrame.Caption.IsEmpty;
            var designMode = activeFrame.CaptionDesignMode;
            _backgroundHeroCaption.Visible = hasCaption && designMode != BackgroundImageCaptionDesignMode.Hidden;

            if (!hasCaption || designMode == BackgroundImageCaptionDesignMode.Hidden)
                return;

            _backgroundHeroCaption.TextAlign = activeFrame.CaptionLayout;
            _backgroundHeroCaption.Height = designMode == BackgroundImageCaptionDesignMode.Minimal ? 82 : 108;
            _backgroundHeroCaption.Padding = designMode == BackgroundImageCaptionDesignMode.Minimal ? new Thickness(4) : new Thickness(16);
            _backgroundHeroCaption.Radius = designMode == BackgroundImageCaptionDesignMode.Minimal ? new Radius(0) : new Radius(18);

            switch (designMode)
            {
                case BackgroundImageCaptionDesignMode.Glass:
                    _backgroundHeroCaption.BackColor = ColorScheme.Surface.WithAlpha(ColorScheme.IsDarkMode ? (byte)154 : (byte)194);
                    _backgroundHeroCaption.Border = new Thickness(1);
                    _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(ColorScheme.IsDarkMode ? (byte)72 : (byte)112);
                    _backgroundHeroCaption.ForeColor = ColorScheme.ForeColor;
                    _backgroundHeroCaption.Shadow = new BoxShadow(0f, 10f, 24f, 0, ColorScheme.ShadowColor.WithAlpha(24));
                    break;

                case BackgroundImageCaptionDesignMode.Solid:
                    _backgroundHeroCaption.BackColor = ColorScheme.SurfaceContainerHigh.WithAlpha(236);
                    _backgroundHeroCaption.Border = new Thickness(1);
                    _backgroundHeroCaption.BorderColor = ColorScheme.Outline.WithAlpha(116);
                    _backgroundHeroCaption.ForeColor = ColorScheme.ForeColor;
                    _backgroundHeroCaption.Shadow = new BoxShadow(0f, 8f, 18f, 0, ColorScheme.ShadowColor.WithAlpha(22));
                    break;

                case BackgroundImageCaptionDesignMode.Minimal:
                    _backgroundHeroCaption.BackColor = SKColors.Transparent;
                    _backgroundHeroCaption.Border = new Thickness(0);
                    _backgroundHeroCaption.BorderColor = SKColors.Transparent;
                    _backgroundHeroCaption.ForeColor = SKColors.White.WithAlpha(236);
                    _backgroundHeroCaption.Shadow = BoxShadow.None;
                    break;

                default:
                    _backgroundHeroCaption.BackColor = SKColors.Black.WithAlpha(118);
                    _backgroundHeroCaption.Border = new Thickness(1);
                    _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(42);
                    _backgroundHeroCaption.ForeColor = SKColors.White;
                    _backgroundHeroCaption.Shadow = BoxShadow.None;
                    break;
            }
        }

        private void BackgroundPreviousButton_Click(object? sender, EventArgs e)
        {
            MoveBackgroundSlide(-1);
        }

        private void BackgroundNextButton_Click(object? sender, EventArgs e)
        {
            MoveBackgroundSlide(1);
        }

        private void BackgroundPlayPauseButton_Click(object? sender, EventArgs e)
        {
            if (_backgroundHero == null)
                return;

            SetBackgroundSlideshowEnabled(!_backgroundHero.BackgroundImageSlideshowEnabled);
        }

        private void MoveBackgroundSlide(int delta)
        {
            if (_backgroundHero == null || _backgroundSlides.Count == 0)
                return;

            var nextIndex = _backgroundHero.BackgroundImageIndex + delta;
            if (_backgroundHero.BackgroundImageSlideshowRepeat)
            {
                nextIndex = (nextIndex % _backgroundSlides.Count + _backgroundSlides.Count) % _backgroundSlides.Count;
            }
            else
            {
                nextIndex = Math.Clamp(nextIndex, 0, _backgroundSlides.Count - 1);
            }

            _backgroundHero.BackgroundImageIndex = nextIndex;
            UpdateBackgroundShowcaseStatus();
        }

        private void UpdateBackgroundShowcaseStatus()
        {
            if (_backgroundHero == null || _backgroundHeroCaption == null || _backgroundStatusCard == null)
                return;

            if (_backgroundSlides.Count == 0)
            {
                _backgroundHeroCaption.Visible = true;
                _backgroundHeroCaption.Text = "No background assets were loaded from assets/images.";
                _backgroundHeroCaption.TextAlign = ContentAlignment.MiddleCenter;
                _backgroundHeroCaption.BackColor = SKColors.Black.WithAlpha(118);
                _backgroundHeroCaption.Border = new Thickness(1);
                _backgroundHeroCaption.BorderColor = SKColors.White.WithAlpha(42);
                _backgroundHeroCaption.ForeColor = SKColors.White;
                _backgroundHeroCaption.Padding = new Thickness(16);
                _backgroundHeroCaption.Radius = new Radius(18);
                _backgroundHeroCaption.Shadow = BoxShadow.None;
                _backgroundStatusCard.Text =
                    "Background Slideshow\nNo background assets are available. Add images under assets/images to populate the showcase and window background mirror.";

                if (_backgroundPlayPauseButton != null)
                    _backgroundPlayPauseButton.Text = "No Assets";

                RefreshBackgroundMenuChecks();
                return;
            }

            var index = Math.Clamp(_backgroundHero.BackgroundImageIndex, 0, _backgroundSlides.Count - 1);
            var activeFrame = _backgroundHero.CurrentBackgroundImageFrame ?? _backgroundSlides[index];
            var caption = activeFrame.Caption;

            _backgroundHeroCaption.Text = caption.ToString();
            ApplyBackgroundCaptionVisuals(activeFrame);
            _backgroundStatusCard.Text =
                $"Background Slideshow\nScene {index + 1}/{_backgroundSlides.Count}  •  Layout: {_backgroundHero.BackgroundImageLayout}  •  Effect: {_backgroundHero.BackgroundImageTransitionEffect}  •  Duration: {_backgroundTransitionDurationPreset} ms\nCaption: {activeFrame.CaptionDesignMode}  •  Align: {activeFrame.CaptionLayout}  •  Slideshow: {(_backgroundHero.BackgroundImageSlideshowEnabled ? "Active" : "Passive")}  •  Repeat: {(_backgroundHero.BackgroundImageSlideshowRepeat ? "Active" : "Passive")}  •  Interval: {_backgroundIntervalPreset} ms  •  Window Background: {(_windowBackgroundEnabled ? "Active" : "Passive")} ({_windowBackgroundMode})\nWindow Blur: {_windowBackgroundBlurAmountPreset} px  •  Mode: {BackgroundImageBlurMode}\nUse the Backgrounds menu to switch layout, effect, duration, caption design and slideshow mode in real time. Use the Window Background menu to mirror the active scene across the window and test blur modes on the root window surface.";

            if (_backgroundPlayPauseButton != null)
                _backgroundPlayPauseButton.Text = _backgroundHero.BackgroundImageSlideshowEnabled ? "Pause Slideshow" : "Start Slideshow";

            RefreshBackgroundMenuChecks();
        }

        private void NotifBtnInfo_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Information",
                "The operation completed successfully. No further action is required. This is a longer message to demonstrate text wrapping behavior in the notification layout.",
                NotificationKind.Info);

        private void NotifBtnSuccess_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Deployment Successful",
                "The build artifact has been deployed to the staging environment and all health probes are green.",
                NotificationKind.Success);

        private void NotifBtnWarning_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "High Latency Detected",
                "Response times on the Telemetry workload have exceeded the 40 ms threshold for the last three checks.",
                NotificationKind.Warning);

        private void NotifBtnError_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Connection Failed",
                "Unable to establish a connection to the remote endpoint. Check network configuration and try again.",
                NotificationKind.Error);

        private void NotifBtnAllFour_Click(object sender, EventArgs e)
        {
            NotificationToast.Show("Information", "Background sync completed with no conflicts detected.", NotificationKind.Info, 4000);
            NotificationToast.Show("Changes Saved", "Your configuration has been written to disk and is active immediately.", NotificationKind.Success, 5000);
            NotificationToast.Show("Token Expiring Soon", "Your session token will expire in 15 minutes. Save your work before it does.", NotificationKind.Warning, 6000);
            NotificationToast.Show("Render Error", "The DirectX 11 context was lost. The renderer has fallen back to software mode.", NotificationKind.Error, 7000);
        }

        private void NotifBtnDismissAll_Click(object sender, EventArgs e)
            => NotificationToast.DismissAll();

        private void NotifBtnLongMessage_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Audit Trail Delayed",
                "The retention sweep has been postponed because the archive lane is warming up.\nEstimated completion: 3–5 minutes.\nNo data will be lost during this window.",
                NotificationKind.Warning,
                6000);

        private void NotifBtnLongDuration_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Background Task Running",
                "An 8-second background task is in progress. Hover to pause the countdown.",
                NotificationKind.Info,
                8000);

        private async void NotifBtnConfirm_Click(object sender, EventArgs e)
        {
            var result = await NotificationToast.ConfirmAsync(
                "Delete Workload",
                "This will permanently remove the selected workload. This action cannot be undone.",
                NotificationKind.Warning,
                0,
                "Delete", "Cancel");

            if (result == "Delete")
                NotificationToast.Show("Deleted", "The workload has been permanently removed.", NotificationKind.Success, 3000);
            else if (result == "Cancel")
                NotificationToast.Show("Cancelled", "No changes were made.", NotificationKind.Info, 2500);
        }

        private void NotifBtnActions_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Update Available",
                "Version 2.4.1 is ready to install. It includes performance improvements and security patches.",
                NotificationKind.Info,
                0,
                new NotificationAction("Install Now", () =>
                    NotificationToast.Show("Installing", "Updating to v2.4.1 in the background…", NotificationKind.Info, 3000)),
                new NotificationAction("Later"));

        private async void NotifBtnManualProgress_Click(object sender, EventArgs e)
        {
            _manualProgressToast?.Dismiss();
            _manualProgressToast = NotificationToast.Show(
                "Publishing Build",
                "Pushing artifacts to the release channel and verifying checksum state.",
                NotificationKind.Info,
                new NotificationOptions
                {
                    LayoutMode = NotificationToastLayoutMode.List,
                    DurationMs = 0,
                    ShowProgressBar = true,
                    Progress = 0f,
                    Actions = [new NotificationAction("Hide", () => _manualProgressToast?.Dismiss())]
                });

            for (var i = 1; i <= 10; i++)
            {
                await Task.Delay(220);
                _manualProgressToast?.SetProgress(i / 10f);
            }

            await Task.Delay(180);
            _manualProgressToast?.Dismiss();
            _manualProgressToast = null;
            NotificationToast.Show("Release Ready", "Build publishing completed successfully.", NotificationKind.Success, 2600);
        }

        private async void NotifBtnProgressToggle_Click(object sender, EventArgs e)
        {
            var toast = NotificationToast.Show(
                "Background Indexing",
                "Collecting symbols and warming the query cache in the background.",
                NotificationKind.Info,
                new NotificationOptions
                {
                    LayoutMode = NotificationToastLayoutMode.List,
                    DurationMs = 0,
                    ShowProgressBar = false,
                    Progress = 0.18f,
                });

            await Task.Delay(700);
            toast.SetProgressVisible(true);

            for (var i = 2; i <= 9; i++)
            {
                await Task.Delay(140);
                toast.SetProgress(i / 10f);
            }

            await Task.Delay(320);
            toast.Dismiss();
        }

        private void NotifBtnThemeAuto_Click(object sender, EventArgs e)
            => ShowNotificationThemeModeExample(
                NotificationKind.Info,
                "Auto Theme",
                "This toast resolves its palette from the current application theme. Toggle dark mode and trigger it again to compare the result.");

        private void NotifBtnThemeLight_Click(object sender, EventArgs e)
            => ShowNotificationThemeModeExample(
                NotificationKind.Light,
                "Light Theme",
                "This toast forces the light palette regardless of the current window theme.");

        private void NotifBtnThemeDark_Click(object sender, EventArgs e)
            => ShowNotificationThemeModeExample(
                NotificationKind.Dark,
                "Dark Theme",
                "This toast forces the dark palette even if the rest of the sample window is currently light.");

        private void NotifBtnThemeCustom_Click(object sender, EventArgs e)
            => NotificationToast.Show(
                "Custom Theme",
                "Custom mode uses an explicit NotificationToastPalette so background, accent and foreground colors can be branded per toast.",
                NotificationKind.Custom,
                new NotificationOptions
                {
                    DurationMs = 5200,
                    ShowProgressBar = true,
                    CustomPalette = CreateNotificationThemeModePalette(),
                    Actions =
                    [
                        new NotificationAction("Apply Globally", ApplyCustomNotificationThemeMode),
                        new NotificationAction("Reset", ResetNotificationThemeModeDefaults),
                    ]
                });

        private void NotifBtnTopLeft_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.TopLeft, "Top Left");

        private void NotifBtnTopCenter_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.TopCenter, "Top Center");

        private void NotifBtnTopRight_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.TopRight, "Top Right");

        private void NotifBtnBottomLeft_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.BottomLeft, "Bottom Left");

        private void NotifBtnBottomCenter_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.BottomCenter, "Bottom Center");

        private void NotifBtnBottomRight_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.BottomRight, "Bottom Right");

        private void NotifBtnCenter_Click(object sender, EventArgs e)
            => ShowNotificationAtPosition(ContentAlignment.MiddleCenter, "Center");

        private void NotifBtnStackMode_Click(object sender, EventArgs e)
        {
            _notificationStackModeEnabled = !_notificationStackModeEnabled;
            NotificationToast.DefaultLayoutMode = _notificationStackModeEnabled
                ? NotificationToastLayoutMode.Stack
                : NotificationToastLayoutMode.List;
            RefreshNotificationStackModeButton();

            NotificationToast.Show(
                "Stack Mode Updated",
                _notificationStackModeEnabled
                    ? "New notifications now use stacked presentation by default. Click the front toast to reveal the ones behind it."
                    : "New notifications now use the standard list layout again.",
                NotificationKind.Info,
                3200);
        }

        private void NotifBtnDialog_Click(object sender, EventArgs e)
            => NotificationToast.ShowDialog(
                "Dialog Presentation",
                "Dialog mode centers the notification, adds a scrim and keeps the action area readable for confirm-style flows.",
                NotificationKind.Info,
                new NotificationOptions
                {
                    DurationMs = 0,
                    ShowProgressBar = false,
                    Actions =
                    [
                        new NotificationAction("Primary"),
                        new NotificationAction("Close")
                    ]
                });

        private void ShowNotificationAtPosition(
            ContentAlignment position,
            string label)
        {
            var layoutLabel = _notificationStackModeEnabled ? "stack" : "list";
            NotificationToast.Show(
                label,
                $"Toasts can anchor to {label.ToLowerInvariant()} using the {layoutLabel} layout. Different positions and layouts run in separate trays.",
                NotificationKind.Info,
                new NotificationOptions
                {
                    DurationMs = 4000,
                    ShowProgressBar = true,
                    Position = position,
                });
        }

        private void RefreshNotificationStackModeButton()
        {
            if (notifBtnStackMode == null)
                return;

            notifBtnStackMode.Text = _notificationStackModeEnabled ? "Stack Mode: On" : "Stack Mode: Off";
        }

        private void ShowNotificationThemeModeExample(NotificationKind kind, string title, string message)
        {
            NotificationToast.Show(
                title,
                message,
                kind,
                new NotificationOptions
                {
                    DurationMs = 4600,
                    ShowProgressBar = true,
                    CustomPalette = kind == NotificationKind.Custom ? CreateNotificationThemeModePalette() : null,
                });
        }

        private static NotificationToastPalette CreateNotificationThemeModePalette()
            => new(
                new SKColor(12, 33, 60),
                new SKColor(56, 189, 248),
                new SKColor(240, 249, 255));

        private void ApplyCustomNotificationThemeMode()
        {
            NotificationToast.CustomPalette = CreateNotificationThemeModePalette();

            NotificationToast.Show(
                "Custom Default Active",
                "NotificationKind.Custom and NotificationToast.CustomPalette now point to the custom sample palette for subsequent toasts.",
                NotificationKind.Custom,
                4200);
        }

        private void ResetNotificationThemeModeDefaults()
        {
            NotificationToast.CustomPalette = null;

            NotificationToast.Show(
                "Theme Defaults Reset",
                "Notification toasts now resolve from Auto mode again.",
                NotificationKind.Info,
                3200);
        }

        private void VisualStyleDangerToggle_Click(object sender, EventArgs e)
        {
            _dangerModeEnabled = !_dangerModeEnabled;
            visualStyleDangerCard.Tag = _dangerModeEnabled ? "danger" : "normal";
            visualStyleDangerCard.Text = _dangerModeEnabled
                ? "Predicate Card\nDanger mode active. Click again to revert."
                : "Predicate Card\nClick to toggle a custom predicate state.";
            visualStyleDangerCard.ReevaluateVisualStyles();
        }

        private void VisualStyleEnableDisabled_Click(object sender, EventArgs e)
        {
            visualStyleDisabledCard.Enabled = !visualStyleDisabledCard.Enabled;
            visualStyleDisabledCard.Text = visualStyleDisabledCard.Enabled
                ? "Disabled State Card\nEnabled again. Click the footer action to disable it."
                : "Disabled State Card\nThis card is disabled and styled by OnDisabled.";
        }

        private void VisualStylePrimaryButton_Click(object sender, EventArgs e)
        {
            visualStyleGhostButton.AccentMotionEnabled = !visualStyleGhostButton.AccentMotionEnabled;
            visualStyleGhostButton.Text = visualStyleGhostButton.AccentMotionEnabled
                ? "Secondary Button - Accent Motion On"
                : "Secondary Button - Accent Motion Off";

            visualStyleScrollProbe.Text = visualStyleGhostButton.AccentMotionEnabled
                ? "Scroll Probe\nSecondary button motion is now enabled. If you can still reach this block, AutoScroll and the new Button control are both working together."
                : "Scroll Probe\nSecondary button motion is now disabled. If you can reach this block, AutoScroll is now measuring content after dock layout. The two Button controls above also prove the new control works inside the example page.";
        }

        private void InitializeGridListDemo()
        {
            var healthyIcon = CreateGridListIcon(new SKColor(34, 197, 94), GridListIconKind.Healthy);
            var warningIcon = CreateGridListIcon(new SKColor(245, 158, 11), GridListIconKind.Warning);
            var lockedIcon = CreateGridListIcon(new SKColor(239, 68, 68), GridListIconKind.Locked);
            var pulseIcon = CreateGridListIcon(new SKColor(59, 130, 246), GridListIconKind.Pulse);

            ConfigurePrimaryGridList(healthyIcon, warningIcon, lockedIcon, pulseIcon);
            ConfigureCompactGridList(healthyIcon, pulseIcon, warningIcon);

            gridListPrimary.SelectedIndex = 0;
            gridListCompact.SelectedIndex = 0;
            UpdateGridListButtons();
            UpdateGridListStatus("Ready", "Primary grid now has enough rows to test sticky header, animated group collapse and optional row resizing in-place.");
        }

        private void ConfigurePrimaryGridList(SKImage healthyIcon, SKImage warningIcon, SKImage lockedIcon, SKImage pulseIcon)
        {
            gridListPrimary.Columns.Clear();
            gridListPrimary.Items.Clear();

            gridListPrimary.Columns.Add(new GridListColumn { Name = "workload", HeaderText = "Workload", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
            gridListPrimary.Columns.Add(new GridListColumn { Name = "live", HeaderText = "Live", Width = 92f, MinWidth = 72f, MaxWidth = 108f, ShowCheckBox = true, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
            gridListPrimary.Columns.Add(new GridListColumn { Name = "owner", HeaderText = "Owner", Width = 138f, MinWidth = 110f, SizeMode = GridListColumnSizeMode.Auto });
            gridListPrimary.Columns.Add(new GridListColumn { Name = "latency", HeaderText = "Latency", Width = 110f, MinWidth = 88f, CellTextAlign = ContentAlignment.MiddleRight, SizeMode = GridListColumnSizeMode.Auto });
            gridListPrimary.Columns.Add(new GridListColumn { Name = "summary", HeaderText = "Summary", Width = 320f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.65f });

            AddPrimaryRow("core", "Core Systems", healthyIcon, "Renderer", true, "Graphics", "14 ms", "DirectX11 path is stable; cache hit ratio above target.");
            AddPrimaryRow("core", "Core Systems", pulseIcon, "Layout", true, "UI", "18 ms", "Measure/arrange pass includes nested cards and sticky regions.");
            AddPrimaryRow("core", "Core Systems", healthyIcon, "Input Hub", true, "Platform", "16 ms", "Pointer capture and wheel routing stay deterministic through overlays.");
            AddPrimaryRow("core", "Core Systems", pulseIcon, "Theme Engine", true, "Design", "19 ms", "Palette interpolation is synchronized with visual-state transitions.");
            AddPrimaryRow("diag", "Diagnostics", warningIcon, "Telemetry", false, "Platform", "41 ms", "Event batcher is backpressured; investigate queue saturation.");
            AddPrimaryRow("diag", "Diagnostics", pulseIcon, "Scroll Lab", true, "QA", "22 ms", "Wheel routing and thumb drag stay stable under nested hosts.");
            AddPrimaryRow("diag", "Diagnostics", warningIcon, "Frame Trace", true, "Rendering", "27 ms", "GPU timings are sampled, but capture export is still warming the pipeline.");
            AddPrimaryRow("diag", "Diagnostics", healthyIcon, "Crash Watch", true, "Ops", "13 ms", "Guard rails are live and no fatal exceptions were observed in the last pass.");
            AddPrimaryRow("secure", "Security", lockedIcon, "Session Guard", true, "Identity", "11 ms", "Lock escalation rules loaded and group policy sync is complete.");
            AddPrimaryRow("secure", "Security", warningIcon, "Audit Trail", false, "Compliance", "35 ms", "Retention sweep delayed because archive lane is warming up.");
            AddPrimaryRow("secure", "Security", lockedIcon, "Vault Mirror", true, "Storage", "17 ms", "Encrypted snapshots are mirrored and signature verification passed.");
            AddPrimaryRow("secure", "Security", pulseIcon, "Access Review", true, "Risk", "24 ms", "Review queue is active and staged approvals refresh every minute.");
            AddPrimaryRow("ship", "Release Channel", pulseIcon, "Preview Ring", true, "Release", "21 ms", "Preview users received the latest package and rollback marker is set.");
            AddPrimaryRow("ship", "Release Channel", warningIcon, "Canary Ring", false, "Release", "38 ms", "Canary deployment paused because health probes dipped below threshold.");
            AddPrimaryRow("ship", "Release Channel", healthyIcon, "Stable Ring", true, "Release", "12 ms", "Stable channel remains green with no pending incidents.");

            gridListPrimary.SortByColumn(0, GridListSortDirection.Ascending);
            gridListPrimary.SelectionChanged += GridListPrimary_SelectionChanged;
            gridListPrimary.CellCheckChanged += GridListPrimary_CellCheckChanged;
            gridListPrimary.ColumnClick += GridListPrimary_ColumnClick;
            gridListPrimary.CellClick += GridListPrimary_CellClick;
        }

        private void ConfigureCompactGridList(SKImage healthyIcon, SKImage pulseIcon, SKImage warningIcon)
        {
            gridListCompact.Columns.Clear();
            gridListCompact.Items.Clear();

            gridListCompact.Columns.Add(new GridListColumn { Name = "stream", HeaderText = "Stream", Width = 220f, MinWidth = 150f, SizeMode = GridListColumnSizeMode.Auto });
            gridListCompact.Columns.Add(new GridListColumn { Name = "state", HeaderText = "State", Width = 100f, MinWidth = 80f, CellTextAlign = ContentAlignment.MiddleCenter, SizeMode = GridListColumnSizeMode.Auto });
            gridListCompact.Columns.Add(new GridListColumn { Name = "note", HeaderText = "Note", Width = 420f, MinWidth = 220f, Sortable = false, SizeMode = GridListColumnSizeMode.Fill, FillWeight = 1.4f });

            AddCompactRow(healthyIcon, "Commit Watcher", "Live", "High-frequency feed without a header bar.");
            AddCompactRow(pulseIcon, "Animation Bus", "Sync", "Transition snapshots update while list selection remains stable.");
            AddCompactRow(warningIcon, "Alert Stream", "Warn", "Compact list mode still paints icons and supports selection.");
        }

        private void AddPrimaryRow(string groupKey, string groupText, SKImage icon, string workload,
            bool isLive, string owner, string latency, string summary)
        {
            var item = new GridListItem { GroupKey = groupKey, GroupText = groupText, Icon = icon };
            item.Cells.Add(new GridListCell { Text = workload, Icon = icon });
            item.Cells.Add(new GridListCell { CheckState = isLive ? CheckState.Checked : CheckState.Unchecked, Text = isLive ? "On" : "Off" });
            item.Cells.Add(new GridListCell { Text = owner });
            item.Cells.Add(new GridListCell { Text = latency });
            item.Cells.Add(new GridListCell { Text = summary });
            gridListPrimary.Items.Add(item);
        }

        private void AddCompactRow(SKImage icon, string stream, string state, string note)
        {
            var item = new GridListItem { Icon = icon };
            item.Cells.Add(new GridListCell { Text = stream, Icon = icon });
            item.Cells.Add(new GridListCell { Text = state });
            item.Cells.Add(new GridListCell { Text = note });
            gridListCompact.Items.Add(item);
        }

        private SKImage CreateGridListIcon(SKColor accent, GridListIconKind kind)
        {
            var info = new SKImageInfo(18, 18);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = accent };
            using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, Color = SKColors.White.WithAlpha(220) };

            switch (kind)
            {
                case GridListIconKind.Healthy:
                    canvas.DrawCircle(9f, 9f, 7f, fill);
                    using (var path = new SKPath())
                    {
                        path.MoveTo(5.4f, 9.3f);
                        path.LineTo(7.7f, 11.7f);
                        path.LineTo(12.8f, 6.3f);
                        canvas.DrawPath(path, stroke);
                    }
                    break;

                case GridListIconKind.Warning:
                    using (var path = new SKPath())
                    {
                        path.MoveTo(9f, 2.2f);
                        path.LineTo(15.2f, 14.8f);
                        path.LineTo(2.8f, 14.8f);
                        path.Close();
                        canvas.DrawPath(path, fill);
                    }
                    canvas.DrawLine(9f, 6f, 9f, 10f, stroke);
                    using (var dotPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeWidth = 1.8f, Color = SKColors.White.WithAlpha(220) })
                        canvas.DrawPoint(9f, 12.7f, dotPaint);
                    break;

                case GridListIconKind.Locked:
                    canvas.DrawRoundRect(new SKRect(4.2f, 8f, 13.8f, 15f), 2.2f, 2.2f, fill);
                    using (var path = new SKPath())
                    {
                        path.MoveTo(5.8f, 8f);
                        path.ArcTo(new SKRect(5.8f, 3.2f, 12.2f, 9.8f), 180f, -180f, false);
                        canvas.DrawPath(path, stroke);
                    }
                    break;

                default:
                    canvas.DrawCircle(9f, 9f, 7f, fill);
                    using (var pulse = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f, Color = SKColors.White.WithAlpha(220) })
                    using (var path = new SKPath())
                    {
                        path.MoveTo(3.2f, 9.1f);
                        path.LineTo(6.2f, 9.1f);
                        path.LineTo(7.8f, 6.2f);
                        path.LineTo(10.1f, 12.1f);
                        path.LineTo(11.6f, 8.6f);
                        path.LineTo(14.8f, 8.6f);
                        canvas.DrawPath(path, pulse);
                    }
                    break;
            }

            var image = surface.Snapshot();
            _gridListImages.Add(image);
            return image;
        }

        private void GridListPrimary_SelectionChanged(object? sender, GridListSelectionChangedEventArgs e)
        {
            var selected = gridListPrimary.SelectedItem;
            var workload = selected?.Cells.Count > 0 ? selected.Cells[0].Text : "None";
            UpdateGridListStatus("Selection", $"Active row: {workload}. Selected index: {e.SelectedIndex}. Multi-select count: {gridListPrimary.SelectedIndices.Count}.");
        }

        private void GridListPrimary_CellCheckChanged(object? sender, GridListCellCheckChangedEventArgs e)
        {
            UpdateGridListStatus("Checkbox", $"{e.Item.Cells[0].Text} changed from {e.PreviousState} to {e.CurrentState}.");
        }

        private void GridListPrimary_ColumnClick(object? sender, GridListColumnClickEventArgs e)
        {
            UpdateGridListStatus("Sort", $"Column '{e.Column.HeaderText}' clicked. Direction: {e.SortDirection}.");
        }

        private void GridListPrimary_CellClick(object? sender, GridListCellEventArgs e)
        {
            UpdateGridListStatus("Cell Click", $"Row '{e.Item.Cells[0].Text}', column '{e.Column.HeaderText}' was activated.");
        }

        private void GridListToggleHeaderButton_Click(object sender, EventArgs e)
        {
            gridListPrimary.HeaderVisible = !gridListPrimary.HeaderVisible;
            if (!gridListPrimary.HeaderVisible)
                gridListPrimary.StickyHeader = false;
            UpdateGridListButtons();
            UpdateGridListStatus("Display", gridListPrimary.HeaderVisible ? "Primary grid header is visible again." : "Primary grid is now in headerless mode.");
        }

        private void GridListToggleStickyButton_Click(object sender, EventArgs e)
        {
            if (!gridListPrimary.HeaderVisible)
                gridListPrimary.HeaderVisible = true;
            gridListPrimary.StickyHeader = !gridListPrimary.StickyHeader;
            UpdateGridListButtons();
            UpdateGridListStatus("Display", gridListPrimary.StickyHeader ? "Sticky header enabled for the primary grid." : "Sticky header disabled; header scrolls with content.");
        }

        private void GridListToggleGroupingButton_Click(object sender, EventArgs e)
        {
            gridListPrimary.GroupingEnabled = !gridListPrimary.GroupingEnabled;
            UpdateGridListButtons();
            UpdateGridListStatus("Grouping", gridListPrimary.GroupingEnabled ? "Group headers are enabled. Click a group row to collapse it." : "Grouping disabled; rows now render as a flat sorted list.");
        }

        private void GridListToggleGridLinesButton_Click(object sender, EventArgs e)
        {
            var next = !gridListPrimary.ShowGridLines;
            gridListPrimary.ShowGridLines = next;
            gridListCompact.ShowGridLines = next;
            UpdateGridListButtons();
            UpdateGridListStatus("Grid Lines", next ? "Row and column separators are visible." : "Grid lines hidden for a cleaner card-like presentation.");
        }

        private void GridListToggleRowResizeButton_Click(object sender, EventArgs e)
        {
            var next = !gridListPrimary.AllowRowResize;
            gridListPrimary.AllowRowResize = next;
            gridListCompact.AllowRowResize = next;
            UpdateGridListButtons();
            UpdateGridListStatus("Row Density", next ? "Row resize enabled. Drag the lower edge of a visible row to change row height." : "Row resize disabled and the grid returns to a fixed rhythm.");
        }

        private void UpdateGridListButtons()
        {
            gridListToggleHeaderButton.Text = gridListPrimary.HeaderVisible ? "Header: On" : "Header: Off";
            gridListToggleStickyButton.Text = gridListPrimary.StickyHeader ? "Sticky: On" : "Sticky: Off";
            gridListToggleGroupingButton.Text = gridListPrimary.GroupingEnabled ? "Grouping: On" : "Grouping: Off";
            gridListToggleGridLinesButton.Text = gridListPrimary.ShowGridLines ? "Grid Lines: On" : "Grid Lines: Off";
            gridListToggleRowResizeButton.Text = gridListPrimary.AllowRowResize ? "Row Resize: On" : "Row Resize: Off";
        }

        private void UpdateGridListStatus(string title, string body)
        {
            gridListStatus.Text = $"{title}\n{body}";
        }

        private void BindingValidationSubmitButton_Click(object? sender, EventArgs e)
        {
            var isTeamValid = bindingValidationTeamCombo.ValidateNow();
            var isPresetValid = bindingValidationPresetCombo.ValidateNow();

            bindingValidationStatusCard.Text = isTeamValid && isPresetValid
                ? "Validation Submit\nAll rules passed. Existing ValidationRule infrastructure is now gating this small workflow."
                : "Validation Submit\nFix the highlighted rule breaches before continuing. The cards above are bound directly to ValidationText and HasValidationError.";

            bindingValidationStatusCard.BackColor = isTeamValid && isPresetValid
                ? ColorScheme.Primary.WithAlpha(196)
                : new SKColor(185, 28, 28);
            bindingValidationStatusCard.ForeColor = SKColors.White;
        }

        protected override void Dispose(bool disposing)
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

                for (var i = 0; i < _gridListImages.Count; i++)
                    _gridListImages[i].Dispose();
                _gridListImages.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
