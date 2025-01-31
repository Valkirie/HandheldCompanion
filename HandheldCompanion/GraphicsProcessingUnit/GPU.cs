using HandheldCompanion.Utils;
using SharpDX.Direct3D9;
using System;
using System.Management;
using Task = System.Threading.Tasks.Task;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class GPU : IDisposable
    {
        #region
        public event IntegerScalingChangedEvent IntegerScalingChanged;
        public delegate void IntegerScalingChangedEvent(bool Supported, bool Enabled);

        public event ImageSharpeningChangedEvent ImageSharpeningChanged;
        public delegate void ImageSharpeningChangedEvent(bool Enabled, int Sharpness);

        public event GPUScalingChangedEvent GPUScalingChanged;
        public delegate void GPUScalingChangedEvent(bool Supported, bool Enabled, int Mode);
        #endregion

        public AdapterInformation adapterInformation;
        protected int deviceIdx = -1;
        protected int displayIdx = -1;

        public bool IsInitialized = false;

        protected const int UpdateInterval = 5000;
        protected Timer UpdateTimer;

        protected const int TelemetryInterval = 1000;
        protected Timer TelemetryTimer;

        protected bool prevGPUScalingSupport = false;
        protected bool prevGPUScaling = false;
        protected int prevScalingMode = -1;

        protected bool prevIntegerScalingSupport = false;
        protected bool prevIntegerScaling = false;

        protected bool prevImageSharpeningSupport = false;
        protected bool prevImageSharpening = false;
        protected int prevImageSharpeningSharpness = -1;

        protected bool halting = false;
        protected object updateLock = new();
        protected object telemetryLock = new();
        public CrossThreadLock functionLock = new();

        private bool _disposed = false; // Prevent multiple disposals

        public enum UpdateGraphicsSettingsSource
        {
            GPUScaling,
            RadeonSuperResolution,
            RadeonImageSharpening,
            IntegerScaling,
            AFMF,
        }

        protected T Execute<T>(Func<T> func, T defaultValue)
        {
            if (!halting && IsInitialized)
            {
                try
                {
                    if (functionLock.TryEnter(1000))
                    {
                        try
                        {
                            var task = Task.Run(func);
                            if (task.Wait(1000))
                                return task.Result;
                            else
                                return defaultValue;
                        }
                        finally
                        {
                            functionLock.Exit();
                        }
                    }
                }
                catch (AccessViolationException) { }
                catch (Exception) { }
            }

            return defaultValue;
        }

        public GPU(AdapterInformation adapterInformation)
        {
            this.adapterInformation = adapterInformation;
        }

        ~GPU()
        {
            Dispose();
        }

        public override string ToString()
        {
            return adapterInformation.Details.Description;
        }

        public virtual void Start()
        {
            // release halting flag
            halting = false;

            if (UpdateTimer != null && !UpdateTimer.Enabled)
                UpdateTimer.Start();

            if (TelemetryTimer != null && !TelemetryTimer.Enabled)
                TelemetryTimer.Start();
        }

        public virtual void Stop()
        {
            // set halting flag
            halting = true;

            if (UpdateTimer != null && UpdateTimer.Enabled)
                UpdateTimer.Stop();

            if (TelemetryTimer != null && TelemetryTimer.Enabled)
                TelemetryTimer.Stop();
        }

        protected virtual void OnIntegerScalingChanged(bool supported, bool enabled)
        {
            IntegerScalingChanged?.Invoke(supported, enabled);

            prevIntegerScalingSupport = supported;
            prevIntegerScaling = enabled;
        }

        protected virtual void OnImageSharpeningChanged(bool enabled, int sharpness)
        {
            ImageSharpeningChanged?.Invoke(enabled, sharpness);

            prevImageSharpening = enabled;
            prevImageSharpeningSharpness = sharpness;
        }

        protected virtual void OnGPUScalingChanged(bool supported, bool enabled, int mode)
        {
            GPUScalingChanged?.Invoke(supported, enabled, mode);

            prevGPUScalingSupport = supported;
            prevGPUScaling = enabled;
            prevScalingMode = mode;
        }

        public virtual bool SetImageSharpening(bool enabled)
        {
            return false;
        }

        public virtual bool SetImageSharpeningSharpness(int sharpness)
        {
            return false;
        }

        public virtual bool SetIntegerScaling(bool enabled, byte type)
        {
            return false;
        }

        public virtual bool SetGPUScaling(bool enabled)
        {
            return false;
        }

        public virtual bool SetScalingMode(int scalingMode)
        {
            return false;
        }

        public virtual bool GetGPUScaling()
        {
            return false;
        }

        public virtual bool GetIntegerScaling()
        {
            return false;
        }

        public virtual bool GetImageSharpening()
        {
            return false;
        }

        public virtual bool HasScalingModeSupport()
        {
            return false;
        }

        public virtual bool HasIntegerScalingSupport()
        {
            return false;
        }

        public virtual bool HasGPUScalingSupport()
        {
            return false;
        }

        public virtual int GetScalingMode()
        {
            return 0;
        }

        public virtual int GetImageSharpeningSharpness()
        {
            return 0;
        }

        public virtual bool HasClock()
        {
            return false;
        }

        public virtual float GetClock()
        {
            return 0.0f;
        }

        public virtual bool HasLoad()
        {
            return false;
        }

        public virtual float GetLoad()
        {
            return 0.0f;
        }

        public virtual bool HasPower()
        {
            return false;
        }

        public virtual float GetPower()
        {
            return 0.0f;
        }

        public virtual bool HasTemperature()
        {
            return false;
        }

        public virtual float GetTemperature()
        {
            return 0.0f;
        }

        public virtual float GetVRAMUsage()
        {
            ObjectQuery query = new ObjectQuery("SELECT AdapterRAM FROM Win32_VideoController");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection queryCollection = searcher.Get();

            // todo: we shouldn't loop through all video controllers but instead only look for "main" one
            foreach (ManagementObject m in queryCollection)
            {
                object AdapterRAM = m["AdapterRAM"];
                if (AdapterRAM is null)
                    continue;

                return Convert.ToUInt64(m["AdapterRAM"].ToString()) / 1024 / 1024;
            }

            return 0.0f;
        }

        public static bool HasIntelGPU()
        {
            return CheckForGPU("intel");
        }

        public static bool HasAMDGPU()
        {
            return CheckForGPU("amd") || CheckForGPU("radeon");
        }

        public static bool HasNvidiaGPU()
        {
            return CheckForGPU("nvidia");
        }

        /// <summary>
        /// Private helper method to check for a specific GPU vendor.
        /// </summary>
        private static bool CheckForGPU(string vendorKeyword)
        {
            string query = "SELECT Name FROM Win32_VideoController";

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.ToLower();

                    if (!string.IsNullOrEmpty(name) && name.Contains(vendorKeyword.ToLower()))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Free managed resources
                UpdateTimer?.Stop();
                UpdateTimer?.Dispose();
                UpdateTimer = null;

                TelemetryTimer?.Stop();
                TelemetryTimer?.Dispose();
                TelemetryTimer = null;

                functionLock?.Dispose();

                // Clear event handlers to prevent memory leaks
                IntegerScalingChanged = null;
                ImageSharpeningChanged = null;
                GPUScalingChanged = null;
            }

            _disposed = true;
        }
    }
}
