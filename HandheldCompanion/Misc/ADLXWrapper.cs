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

        public static int SetRSR(bool isEnabled) => Execute<int>(() => ADLXBackend.SetRSR(isEnabled), -1);
        public static int GetRSRState() => Execute<int>(ADLXBackend.GetRSRState, -1);
        public static bool SetRSRSharpness(int sharpness) => Execute<bool>(() => ADLXBackend.SetRSRSharpness(sharpness), false);
        public static int GetRSRSharpness() => Execute<int>(ADLXBackend.GetRSRSharpness, -1);
        public static int SetImageSharpening(bool isEnabled, int percent) => Execute<int>(() => ADLXBackend.SetImageSharpning(0, isEnabled, percent), -1);
        public static bool HasIntegerScalingSupport() => Execute<bool>(ADLXBackend.HasIntegerScalingSupport, false);
        public static int SetIntegerScaling(int key) => Execute<int>(() => ADLXBackend.SetIntegerScaling(key), -1);
        public static bool IsIntegerScalingEnabled() => Execute<bool>(ADLXBackend.IsIntegerScalingEnabled, false);
        public static bool HasGPUScalingSupport() => Execute<bool>(ADLXBackend.HasGPUScalingSupport, false);
        public static int SetGPUScaling(int key) => Execute<int>(() => ADLXBackend.SetGPUScaling(key), -1);
        public static bool IsGPUScalingEnabled() => Execute<bool>(ADLXBackend.IsGPUScalingEnabled, false);
        public static bool HasScalingModeSupport() => Execute<bool>(ADLXBackend.HasScalingModeSupport, false);
        public static int SetScalingMode(int key) => Execute<int>(() => ADLXBackend.SetScalingMode(key), -1);
        public static int GetScalingMode() => Execute<int>(ADLXBackend.GetScalingMode, -1);
    }
}
