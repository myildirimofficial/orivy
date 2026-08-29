using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Orivy.Studio.Persistence;

/// <summary>
/// Tracks recently opened project files (and opened folders) across sessions, persisted as a small
/// JSON file under the user's local app data — the "project system" the Start Screen surfaces so the
/// app doesn't just boot straight into a throwaway blank canvas every time.
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
        public bool IsFolder { get; set; }
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
            // Drop entries whose file/folder no longer exists instead of showing dead links.
            return entries.Where(e => e.IsFolder ? Directory.Exists(e.Path) : File.Exists(e.Path)).ToList();
        }
        catch (IOException) { return new List<Entry>(); }
        catch (JsonException) { return new List<Entry>(); }
        catch (UnauthorizedAccessException) { return new List<Entry>(); }
    }

    public static void Add(string path, bool isFolder)
    {
        try
        {
            var entries = Load();
            entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, new Entry { Path = path, IsFolder = isFolder, OpenedAt = DateTime.UtcNow });
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
