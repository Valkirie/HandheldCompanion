using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class GPU
    {
        #region
        public event IntegerScalingChangedEvent IntegerScalingChanged;
        public delegate void IntegerScalingChangedEvent(bool Supported, bool Enabled);

        public event ImageSharpeningChangedEvent ImageSharpeningChanged;
        public delegate void ImageSharpeningChangedEvent(bool Enabled, int Sharpness);

        public event GPUScalingChangedEvent GPUScalingChanged;
        public delegate void GPUScalingChangedEvent(bool Supported, bool Enabled, int Mode);
        #endregion

        private static GPU gpu;
        private static string Manufacturer;

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

        protected static object wrapperLock = new();
        protected static T Execute<T>(Func<T> func, T defaultValue)
        {
            lock (wrapperLock)
            {
                Task<T> task = Task.Run(func);
                if (task.Wait(TimeSpan.FromSeconds(5)))
                {
                    return task.Result;
                }

                return defaultValue;
            }
        }

        public GPU()
        {
            Manufacturer = MotherboardInfo.VideoController;

            TelemetryTimer = new Timer(TelemetryInterval);
            TelemetryTimer.AutoReset = true;
        }

        public static GPU GetCurrent()
        {
            if (gpu is not null)
                return gpu;

            switch (Manufacturer)
            {
                case "Advanced Micro Devices, Inc.":
                    gpu = new AMDGPU();
                    break;
                case "Intel Corporation":
                    gpu = new IntelGPU();
                    break;
            }

            return gpu;
        }

        public virtual void Start() 
        {
            if (UpdateTimer != null)
                UpdateTimer.Start();

            if (TelemetryTimer != null)
                TelemetryTimer.Start();
        }

        public virtual void Stop()
        {
            if (UpdateTimer != null)
                UpdateTimer.Stop();

            if (TelemetryTimer != null)
                TelemetryTimer.Stop();
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

        public virtual float GetLoad()
        {
            return 0.0f;
        }

        public virtual float GetPower()
        {
            return 0.0f;
        }
    }
}
