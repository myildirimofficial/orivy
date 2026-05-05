using Orivy.Controls;
using System;
using System.Collections.Generic;

namespace Orivy.Example;

internal partial class MainWindow
{
    private readonly Dictionary<ImageLayout, List<MenuItem>> _backgroundLayoutMenuItems = new();

    private readonly Dictionary<BackgroundImageTransitionEffect, List<MenuItem>> _backgroundEffectMenuItems = new();

    private readonly Dictionary<BackgroundImageCaptionDesignMode, List<MenuItem>> _backgroundCaptionDesignMenuItems = new();

    private readonly Dictionary<int, List<MenuItem>> _backgroundDurationMenuItems = new();

    private readonly Dictionary<int, List<MenuItem>> _backgroundIntervalMenuItems = new();

    private MenuItem? _backgroundSlideshowMenuItem;

    private MenuItem? _backgroundRepeatMenuItem;

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
}
