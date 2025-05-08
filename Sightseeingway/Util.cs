using System;
using System.Diagnostics;

namespace Sightseeingway
{
    /// <summary>
    /// Utility functions
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Opens a URL in the default browser
        /// </summary>
        /// <param name="url">The URL to open</param>
        public static void OpenLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error($"Failed to open URL: {url}", ex, true);
            }
        }
    }
}