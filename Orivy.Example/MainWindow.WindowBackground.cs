using Orivy;
using Orivy.Controls;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Orivy.Example;

internal partial class MainWindow
{
    private enum WindowBackgroundMode
    {
        Normal,
        Slide
    }

    private readonly Dictionary<WindowBackgroundMode, List<MenuItem>> _windowBackgroundModeMenuItems = new();

    private readonly Dictionary<int, List<MenuItem>> _windowBackgroundBlurAmountMenuItems = new();

    private readonly Dictionary<BackgroundImageBlurMode, List<MenuItem>> _windowBackgroundBlurModeMenuItems = new();

    private int _windowBackgroundBlurAmountPreset;

    private bool _windowBackgroundEnabled = false;

    private WindowBackgroundMode _windowBackgroundMode = WindowBackgroundMode.Normal;

    private bool _windowBackgroundSlideInitialized;

    private SKImage? _windowBackgroundNormalImage;

    private MenuItem? _windowBackgroundEnabledMenuItem;

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
}
