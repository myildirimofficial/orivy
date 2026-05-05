using Orivy.Animation;
using Orivy.Controls;
using System;
using System.Collections.Generic;

namespace Orivy.Example;

internal partial class MainWindow
{
    private readonly Dictionary<TabViewTransitionEffect, List<MenuItem>> _transitionMenuItems = new();

    private readonly Dictionary<AnimationType, List<MenuItem>> _transitionEasingMenuItems = new();

    private readonly Dictionary<int, List<MenuItem>> _transitionSpeedMenuItems = new();

    private int _transitionDurationPreset = 350;

    private void InitializeTransitionMenu(MenuItem rootItem)
    {
        var effects = (TabViewTransitionEffect[])Enum.GetValues(typeof(TabViewTransitionEffect));
        foreach (var effect in effects)
        {
            var menuItem = rootItem.AddMenuItem(effect.ToString(), (_, _) => SetTransitionEffect(effect));
            RegisterEffectItem(menuItem, effect);
        }

        rootItem.AddSeparator();

        var animationType = (AnimationType[])Enum.GetValues(typeof(AnimationType));

        // ¦¦ Easing ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦
        var easingMenu = rootItem.AddMenuItem("Easing");
        
        foreach (var type in animationType)
        {
            var menuItem = easingMenu.AddMenuItem(type.ToString(), (_, _) => SetTransitionAnimationType(type));
            RegisterEasingItem(menuItem, type);
        }

        rootItem.AddSeparator();

        // ¦¦ Speed ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦
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

    private void RegisterEffectItem(MenuItem item, TabViewTransitionEffect effect)
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

    internal void SetTransitionEffect(TabViewTransitionEffect effect)
    {
        tabView.TransitionEffect = effect;
        RefreshTransitionMenuChecks();
    }

    internal void SetTransitionAnimationType(AnimationType type)
    {
        tabView.TransitionAnimationType = type;
        RefreshTransitionEasingMenuChecks();
    }

    internal void SetTransitionDuration(int ms)
    {
        _transitionDurationPreset = ms;
        tabView.TransitionDurationMs = ms;
        RefreshTransitionSpeedMenuChecks();
    }

    private void RefreshTransitionMenuChecks()
    {
        foreach (var item in _transitionMenuItems)
        {
            var isSelected = item.Key == tabView.TransitionEffect;
            for (var i = 0; i < item.Value.Count; i++)
                item.Value[i].Checked = isSelected;
        }
    }

    private void RefreshTransitionEasingMenuChecks()
    {
        var current = tabView.TransitionAnimationType;
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
}
