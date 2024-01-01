using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Misc
{
    public static class ADLXWrapper
    {
        private static object ADLXLock = new();

        private static T Execute<T>(Func<T> func, T defaultValue)
        {
            lock (ADLXLock)
            {
                Task<T> task = Task.Run(func);
                if (task.Wait(TimeSpan.FromSeconds(5)))
                {
                    return task.Result;
                }

                return defaultValue;
            }
        }

        // RSR
        public static bool HasRSRSupport() => Execute<bool>(ADLXBackend.HasRSRSupport, false);
        public static bool GetRSR() => Execute<bool>(ADLXBackend.GetRSR, false);
        public static bool SetRSR(bool enable) => Execute<bool>(() => ADLXBackend.SetRSR(enable), false);
        public static int GetRSRSharpness() => Execute<int>(ADLXBackend.GetRSRSharpness, -1);
        public static bool SetRSRSharpness(int sharpness) => Execute<bool>(() => ADLXBackend.SetRSRSharpness(sharpness), false);

        // ImageSharpening
        public static bool GetImageSharpening() => Execute<bool>(() => ADLXBackend.GetImageSharpening(0), false);
        public static bool SetImageSharpening(bool enable) => Execute<bool>(() => ADLXBackend.SetImageSharpening(0, enable), false);
        public static int GetImageSharpeningSharpness() => Execute<int>(() => ADLXBackend.GetImageSharpeningSharpness(0), -1);
        public static bool SetImageSharpeningSharpness(int sharpness) => Execute<bool>(() => ADLXBackend.SetImageSharpeningSharpness(0, sharpness), false);

        // IntegerScaling
        public static bool HasIntegerScalingSupport() => Execute<bool>(() => ADLXBackend.HasIntegerScalingSupport(0), false);
        public static bool GetIntegerScaling() => Execute<bool>(() => ADLXBackend.GetIntegerScaling(0), false);
        public static bool SetIntegerScaling(bool enabled) => Execute<bool>(() => ADLXBackend.SetIntegerScaling(0, enabled), false);

        public static bool HasGPUScalingSupport() => Execute<bool>(() => ADLXBackend.HasGPUScalingSupport(0), false);
        public static bool GetGPUScaling() => Execute<bool>(() => ADLXBackend.GetGPUScaling(0), false);
        public static bool SetGPUScaling(bool enabled) => Execute<bool>(() => ADLXBackend.SetGPUScaling(0, enabled), false);

        public static bool HasScalingModeSupport() => Execute<bool>(() => ADLXBackend.HasScalingModeSupport(0), false);
        public static int GetScalingMode() => Execute<int>(() => ADLXBackend.GetScalingMode(0), -1);
        public static bool SetScalingMode(int mode) => Execute<bool>(() => ADLXBackend.SetScalingMode(0, mode), false);
    }
}
