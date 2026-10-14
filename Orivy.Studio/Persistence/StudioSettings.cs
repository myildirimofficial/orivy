using Orivy;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orivy.Studio.Persistence;

/// <summary>Shell preferences persisted under LocalAppData (theme, preview host, …).</summary>
public enum StudioThemePreference
{
    Light,
    Dark,
    Auto,
}

/// <summary>
/// Loads/saves Orivy Studio shell settings as JSON next to <see cref="RecentProjects"/>.
/// Theme <see cref="StudioThemePreference.Auto"/> follows the OS apps-dark flag via
/// <see cref="Orivy.Native.Windows.Helpers.IsDark"/>.
/// </summary>
internal static class StudioSettings
{
    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OrivyStudio", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public sealed class Data
    {
        public StudioThemePreference Theme { get; set; } = StudioThemePreference.Auto;
        public bool PreviewInSeparateWindow { get; set; }
    }

    private static Data _current = new();

    public static StudioThemePreference Theme
    {
        get => _current.Theme;
        set
        {
            if (_current.Theme == value)
                return;
            _current.Theme = value;
            ApplyTheme();
            Save();
        }
    }

    public static bool PreviewInSeparateWindow
    {
        get => _current.PreviewInSeparateWindow;
        set
        {
            if (_current.PreviewInSeparateWindow == value)
                return;
            _current.PreviewInSeparateWindow = value;
            Save();
        }
    }

    public static void Load()
    {
        try
        {
            var path = StoragePath;
            if (!File.Exists(path))
            {
                _current = new Data();
                ApplyTheme();
                return;
            }

            _current = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), JsonOptions) ?? new Data();
            ApplyTheme();
        }
        catch (IOException)
        {
            _current = new Data();
            ApplyTheme();
        }
        catch (JsonException)
        {
            _current = new Data();
            ApplyTheme();
        }
        catch (UnauthorizedAccessException)
        {
            _current = new Data();
            ApplyTheme();
        }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(_current, JsonOptions));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Cycles Light → Dark → Auto → Light.</summary>
    public static StudioThemePreference CycleTheme()
    {
        Theme = Theme switch
        {
            StudioThemePreference.Light => StudioThemePreference.Dark,
            StudioThemePreference.Dark => StudioThemePreference.Auto,
            _ => StudioThemePreference.Light,
        };
        return Theme;
    }

    /// <summary>Applies <see cref="Theme"/> to <see cref="ColorScheme.IsDarkMode"/> (Auto reads the OS).</summary>
    public static void ApplyTheme()
    {
        var dark = Theme switch
        {
            StudioThemePreference.Dark => true,
            StudioThemePreference.Light => false,
            _ => Orivy.Native.Windows.Helpers.IsDark(),
        };
        ColorScheme.IsDarkMode = dark;
    }

    public static string ThemeIcon(StudioThemePreference preference) => preference switch
    {
        StudioThemePreference.Light => "sun",
        StudioThemePreference.Dark => "moon",
        _ => "theme-auto",
    };

    public static string ThemeToolTip(StudioThemePreference preference) => preference switch
    {
        StudioThemePreference.Light => "Theme: Light (click for Dark)",
        StudioThemePreference.Dark => "Theme: Dark (click for Auto)",
        _ => "Theme: Auto — follow system (click for Light)",
    };
}
