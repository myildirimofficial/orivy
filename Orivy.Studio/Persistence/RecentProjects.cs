using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Orivy.Studio.Persistence;

/// <summary>
/// Tracks recently opened folders across sessions, persisted as a small JSON file under the user's
/// local app data — the list the Start Screen surfaces so the app doesn't just boot straight into a
/// throwaway blank canvas every time. Folders only, not individual files: the shell is folder-centric
/// (see <see cref="StudioWindow.OpenFolder"/>), and a recent list of every file ever opened inside
/// whatever folder was already browsed added noise without a matching workflow to use it for.
/// </summary>
internal static class RecentProjects
{
    private const int MaxEntries = 10;

    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OrivyStudio", "recent.json");

    public sealed class Entry
    {
        public string Path { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; }
    }

    public static List<Entry> Load()
    {
        try
        {
            var path = StoragePath;
            if (!File.Exists(path))
                return new List<Entry>();

            var entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? new List<Entry>();
            // Drop entries whose folder no longer exists instead of showing a dead link — also
            // naturally discards any individual-file entries a pre-folder-only version of Studio wrote,
            // since a file path never satisfies Directory.Exists.
            return entries.Where(e => Directory.Exists(e.Path)).ToList();
        }
        catch (IOException) { return new List<Entry>(); }
        catch (JsonException) { return new List<Entry>(); }
        catch (UnauthorizedAccessException) { return new List<Entry>(); }
    }

    public static void Add(string folder)
    {
        try
        {
            var entries = Load();
            entries.RemoveAll(e => string.Equals(e.Path, folder, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new Entry { Path = folder, OpenedAt = DateTime.UtcNow });
            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            var dir = System.IO.Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(StoragePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException) { /* Best-effort — a failed write just means no recent-list entry. */ }
        catch (UnauthorizedAccessException) { }
    }
}
