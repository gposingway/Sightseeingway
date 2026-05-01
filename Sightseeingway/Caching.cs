using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Sightseeingway
{
    public static class Caching
    {
        private static readonly ConcurrentDictionary<string, DateTime> RenamedFilesCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

        public static void AddToRenameCache(string filename)
        {
            RenamedFilesCache[filename] = DateTime.UtcNow;
            Plugin.Logger?.Debug($"Filename '{filename}' added/updated in rename cache.");
            CleanRenameCache();
        }

        public static bool IsInRenameCache(string filename)
        {
            if (!RenamedFilesCache.TryGetValue(filename, out var timestamp)) return false;

            if (DateTime.UtcNow - timestamp > CacheDuration)
            {
                // Expired — drop it and treat as a miss.
                RenamedFilesCache.TryRemove(filename, out _);
                Plugin.Logger?.Debug($"Filename '{filename}' expired from rename cache on read.");
                return false;
            }

            Plugin.Logger?.Debug($"Filename '{filename}' found in rename cache.");
            return true;
        }

        private static void CleanRenameCache()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = RenamedFilesCache
                .Where(kvp => now - kvp.Value > CacheDuration)
                .Select(kvp => kvp.Key)
                .ToList();

            if (expiredKeys.Count == 0) return;

            Plugin.Logger?.Debug($"Cleaning {expiredKeys.Count} expired items from rename cache.");
            foreach (var key in expiredKeys)
            {
                RenamedFilesCache.TryRemove(key, out _);
            }
        }
    }
}
