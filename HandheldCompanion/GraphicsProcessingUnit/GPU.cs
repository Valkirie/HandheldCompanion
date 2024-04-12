using HandheldCompanion.Utils;
using SharpDX.Direct3D9;
using System;
using System.Management;
using System.Threading.Tasks;
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

        protected const int UpdateInterval = 2000;
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

        protected object updateLock = new();
        protected object telemetryLock = new();
        protected object functionLock = new();
        protected bool halting = false;

        protected T Execute<T>(Func<T> func, T defaultValue)
        {
            if (halting)
                return defaultValue;

            lock (functionLock)
            {
                try
                {
                    Task<T> task = Task.Run(() =>
                    {
                        return func();
                    });

                    if (task.Wait(TimeSpan.FromSeconds(3)))
                        return task.Result;
                }
                catch (AccessViolationException)
                { }
                catch (Exception)
                { }
            }

            return defaultValue;
        }

        public bool IsBusy => (UpdateTimer is not null && UpdateTimer.Enabled) || (TelemetryTimer is not null && TelemetryTimer.Enabled);

        public GPU(AdapterInformation adapterInformation)
        {
            this.adapterInformation = adapterInformation;
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

            try
            {
                if (UpdateTimer != null && UpdateTimer.Enabled)
                    UpdateTimer.Stop();

                if (TelemetryTimer != null && TelemetryTimer.Enabled)
                    TelemetryTimer.Stop();
            }
            catch (Exception ex)
            {
            }
        }

        protected virtual void OnIntegerScalingChanged(bool supported, bool enabled)
        {
            IntegerScalingChanged?.Invoke(supported, enabled);
        }

        protected virtual void OnImageSharpeningChanged(bool enabled, int sharpness)
        {
            ImageSharpeningChanged?.Invoke(enabled, sharpness);
        }

        protected virtual void OnGPUScalingChanged(bool supported, bool enabled, int mode)
        {
            GPUScalingChanged?.Invoke(supported, enabled, mode);
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

        public virtual float GetClock()
        {
            return 0.0f;
        }

        public virtual float GetLoad()
        {
            return 0.0f;
        }

        public virtual float GetPower()
        {
            return 0.0f;
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

        public void Dispose()
        {
            UpdateTimer?.Dispose();
            TelemetryTimer?.Dispose();
            updateLock = null;
            telemetryLock = null;

            GC.SuppressFinalize(this);
        }
    }
}
