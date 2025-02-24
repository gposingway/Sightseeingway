using System;
using System.Collections.Generic;
using System.Linq;

namespace Sightseeingway
{
    public static class Caching
    {
        private static readonly Dictionary<string, DateTime> RenamedFilesCache = [];
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

        private static void AddToRenameCache(string filename)
        {
            if (RenamedFilesCache.ContainsKey(filename))
            {
                RenamedFilesCache[filename] = DateTime.Now;
                Plugin.Log.Debug($"Filename cache updated for '{filename}'.");
            }
            else
            {
                RenamedFilesCache.Add(filename, DateTime.Now);
                Plugin.Log.Debug($"Filename '{filename}' added to rename cache.");
            }
            CleanRenameCache();
        }

        private static bool IsInRenameCache(string filename)
        {
            if (RenamedFilesCache.ContainsKey(filename))
            {
                Plugin.Log.Debug($"Filename '{filename}' found in rename cache.");
                return true;
            }
            return false;
        }

        private static void CleanRenameCache()
        {
            var expiredKeys = RenamedFilesCache.Keys.Where(key => DateTime.Now - RenamedFilesCache[key] > CacheDuration).ToList();
            if (expiredKeys.Any())
            {
                Plugin.Log.Debug($"Cleaning {expiredKeys.Count} expired items from rename cache.");
                foreach (var key in expiredKeys)
                {
                    RenamedFilesCache.Remove(key);
                    Plugin.Log.Debug($"Expired filename '{key}' removed from cache.");
                }
            }
        }
    }
}
