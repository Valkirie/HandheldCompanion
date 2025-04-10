using HandheldCompanion.Shared;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Timers;
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

        // true: GPU is busy, false: GPU is free
        public event StatusChangedEvent StatusChanged;
        public delegate void StatusChangedEvent(bool status);
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

        protected volatile bool halting = false;
        protected object updateLock = new();
        protected object telemetryLock = new();
        protected object functionLock = new();

        private Timer BusyTimer;
        private bool busyEventRaised = false;

        protected static HashSet<string> ProcessTargets = new HashSet<string>();
        private static readonly object processTargetsLock = new object();

        public bool IsBusy
        {
            get
            {
                bool lockTaken = false;
                try
                {
                    // Try to enter the lock immediately
                    Monitor.TryEnter(functionLock, 0, ref lockTaken);
                    // If we couldn't take the lock, it means someone else is holding it.
                    return !lockTaken;
                }
                finally
                {
                    if (lockTaken)
                        Monitor.Exit(functionLock);
                }
            }
        }

        private bool _disposed = false; // Prevent multiple disposals

        public enum UpdateGraphicsSettingsSource
        {
            GPUScaling,
            RadeonSuperResolution,
            RadeonImageSharpening,
            IntegerScaling,
            AFMF,
        }

        /// <summary>
        /// Execute a function while managing the busy/free status.
        /// A class-level timer is started before calling func().
        /// If func() runs longer than 1 second, the timer elapses and raises StatusChanged(true).
        /// When func() finishes, if busy status was raised, StatusChanged(false) is raised.
        /// </summary>
        protected T Execute<T>(Func<T> func, T defaultValue)
        {
            if (!halting && IsInitialized)
            {
                lock (functionLock)
                {
                    try
                    {
                        // Reset flag
                        busyEventRaised = false;

                        // Reset timer
                        BusyTimer.Stop();
                        BusyTimer.Start();

                        // Execute function
                        T result = func();

                        // Stop timer since func() has completed
                        BusyTimer.Stop();

                        // If the busy event was raised, signal that we're now free.
                        if (busyEventRaised)
                            StatusChanged?.Invoke(false);

                        return result;
                    }
                    catch { }
                }
            }

            return defaultValue;
        }

        public GPU(AdapterInformation adapterInformation)
        {
            this.adapterInformation = adapterInformation;

            // Initialize the busy timer with a 2 seconds interval and set AutoReset to false.
            BusyTimer = new(2000) { AutoReset = false };
            BusyTimer.Elapsed += BusyTimer_Elapsed;
        }

        ~GPU()
        {
            Dispose();
        }

        public override string ToString()
        {
            return adapterInformation.Details.Description;
        }

        protected virtual void BusyTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            busyEventRaised = true;
            StatusChanged?.Invoke(true);
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

            if (BusyTimer != null && BusyTimer.Enabled)
                BusyTimer.Stop();
        }

        /// <summary>
        /// Terminates processes whose names appear in ProcessTargets.
        /// </summary>
        protected void TerminateConflictingProcesses()
        {
            // Attempt to obtain the lock immediately.
            if (!Monitor.TryEnter(processTargetsLock))
                return;

            try
            {
                foreach (Process proc in Process.GetProcesses())
                {
                    if (ProcessTargets.Contains(proc.ProcessName))
                    {
                        proc.Kill();
                        // Remove the target so we don't try killing it again.
                        ProcessTargets.Remove(proc.ProcessName);

                        // If all targets are handled, exit early.
                        if (ProcessTargets.Count == 0)
                            break;
                    }

                    LogManager.LogError("{0} was killed to restore {1} library", proc.ProcessName, this.GetType().Name);
                }
            }
            finally
            {
                Monitor.Exit(processTargetsLock);
            }
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

        // todo: replace me with LHM readings
        public virtual float GetVRAMUsage()
        {
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
            halting = true;

            if (disposing)
            {
                // Free managed resources
                UpdateTimer?.Stop();
                UpdateTimer?.Dispose();
                UpdateTimer = null;

                TelemetryTimer?.Stop();
                TelemetryTimer?.Dispose();
                TelemetryTimer = null;

                BusyTimer?.Stop();
                BusyTimer?.Dispose();
                BusyTimer = null;

                // Clear event handlers to prevent memory leaks
                IntegerScalingChanged = null;
                ImageSharpeningChanged = null;
                GPUScalingChanged = null;
                StatusChanged = null;
            }

            _disposed = true;
        }
    }
}
