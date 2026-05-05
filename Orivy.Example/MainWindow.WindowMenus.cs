using Orivy;
using Orivy.Controls;
using System;
using System.Collections.Generic;

namespace Orivy.Example;

internal partial class MainWindow
{
    private readonly Dictionary<WindowThemeType, List<MenuItem>> _windowThemeMenuItems = new();

    private readonly Dictionary<bool, List<MenuItem>> _windowThemeModeMenuItems = new();

    private readonly List<MenuItem> _titleBarMenuPlacementItems = new();

    private readonly List<MenuItem> _embeddedTabStripResizerItems = new();

    private bool _windowThemeModePreset = ColorScheme.IsDarkMode;

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
        if (TabView != null)
            tabView.ShowTabStripResizer = visible;

        if (_embeddedTabsPage != null)
            _embeddedTabsPage.ShowTabStripResizer = visible;

        RefreshEmbeddedTabStripResizerChecks();
    }

    private bool IsEmbeddedTabStripResizerVisible()
    {
        if (_embeddedTabsPage != null)
            return _embeddedTabsPage.ShowTabStripResizer;

        return tabView != null && tabView.ShowTabStripResizer;
    }

    private void RefreshEmbeddedTabStripResizerChecks()
    {
        var isVisible = IsEmbeddedTabStripResizerVisible();
        for (var i = 0; i < _embeddedTabStripResizerItems.Count; i++)
            _embeddedTabStripResizerItems[i].Checked = isVisible;
    }
}
