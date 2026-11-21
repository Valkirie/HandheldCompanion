using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Helpers
{
    /// <summary>
    /// Display mode rules shared by the display apply path, the profile pages and QuickTools.
    /// </summary>
    public static class DisplayModeHelper
    {
        /// <summary>
        /// Closest available refresh rate, the higher one on a tie. Returns the requested rate when none is listed.
        /// </summary>
        public static int SnapRefreshRate(IEnumerable<int> refreshRates, int frequency)
        {
            List<int> rates = refreshRates.ToList();
            if (rates.Count == 0)
                return frequency;

            return rates
                .OrderBy(f => Math.Abs(f - frequency))
                .ThenByDescending(f => f)
                .First();
        }

        /// <summary>
        /// Resolution matching width x height in either orientation, a stored mode might come from a rotated display.
        /// </summary>
        public static T? FindResolution<T>(IEnumerable<T> resolutions, int width, int height, Func<T, int> getWidth, Func<T, int> getHeight)
            where T : class
        {
            return resolutions.FirstOrDefault(r => getWidth(r) == width && getHeight(r) == height)
                ?? resolutions.FirstOrDefault(r => getWidth(r) == height && getHeight(r) == width);
        }

        /// <summary>
        /// Known monitor the game window of a profile was last active on, sub profiles without one use their parent's.
        /// </summary>
        public static Guid GetLastMonitorId(Profile profile)
        {
            Guid monitorId = profile.LastMonitorId;
            if (monitorId == Guid.Empty && profile.IsSubProfile)
                monitorId = ManagerFactory.profileManager.GetParent(profile)?.LastMonitorId ?? Guid.Empty;

            return monitorId;
        }

        /// <summary>
        /// A process of the profile game is running, sub profiles without a path use their parent's.
        /// </summary>
        public static bool IsGameRunning(Profile profile)
        {
            string path = profile.Path;
            if (string.IsNullOrEmpty(path) && profile.IsSubProfile)
                path = ManagerFactory.profileManager.GetParent(profile)?.Path ?? string.Empty;

            if (string.IsNullOrEmpty(path))
                return false;

            return ProcessManager.GetProcesses().Any(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}
