using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using PrecisionTiming;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace HandheldCompanion.Platforms
{
    public class RTSS : IPlatform
    {
        #region struct
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("RTSSHooks64.dll")]
        public static extern uint SetFlags(uint dwAND, uint dwXOR);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern void LoadProfile(string profile = GLOBAL_PROFILE);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern void SaveProfile(string profile = GLOBAL_PROFILE);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern void DeleteProfile(string profile = GLOBAL_PROFILE);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern bool GetProfileProperty(string propertyName, IntPtr value, uint size);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern bool SetProfileProperty(string propertyName, IntPtr value, uint size);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern void ResetProfile(string profile = GLOBAL_PROFILE);

        [DllImport("RTSSHooks64.dll", CharSet = CharSet.Ansi)]
        public static extern void UpdateProfiles();
        #endregion

        private const uint WM_APP = 0x8000;
        private const uint WM_RTSS_UPDATESETTINGS = WM_APP + 100;
        private const uint WM_RTSS_SHOW_PROPERTIES = WM_APP + 102;

        private const uint RTSSHOOKSFLAG_OSD_VISIBLE = 1;
        private const uint RTSSHOOKSFLAG_LIMITER_DISABLED = 4;
        private const string GLOBAL_PROFILE = "";

        private int RequestedFramerate = 0;

        private PrecisionTimer FrameRateTimer;

        public RTSS()
        {
            base.PlatformType = PlatformType.RTSS;
            base.KeepAlive = true;

            Name = "RTSS";
            ExecutableName = "RTSS.exe";

            // store specific modules
            Modules = new List<string>()
            {
                "RTSSHooks64.dll",
            };

            // check if platform is installed
            InstallPath = RegistryUtils.GetString(@"SOFTWARE\WOW6432Node\Unwinder\RTSS", "InstallDir");
            if (Path.Exists(InstallPath))
            {
                // update paths
                SettingsPath = Path.Combine(InstallPath, @"Profiles\Global");
                ExecutablePath = Path.Combine(InstallPath, ExecutableName);

                // check executable
                IsInstalled = File.Exists(ExecutablePath);
            }

            if (!IsInstalled)
            {
                LogManager.LogCritical("Rivatuner Statistics Server is missing. Please get it from: {0}", "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html");
                return;
            }

            if (!HasModules)
            {
                LogManager.LogCritical("Rivatuner Statistics Server RTSSHooks64.dll is missing. Please get it from: {0}", "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html");
                return;
            }

            // start RTSS if not running
            if (!IsRunning())
                Start();

            // hook into RTSS process
            Process.Exited += Process_Exited;

            // our main watchdog to (re)apply requested settings
            base.PlatformWatchdog = new(2000) { Enabled = true };
            base.PlatformWatchdog.Elapsed += Watchdog_Elapsed;

            // hook into process manager
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            // timer used to monitor foreground application framerate
            FrameRateTimer = new PrecisionTimer();
            FrameRateTimer.SetAutoResetMode(true);
            FrameRateTimer.SetResolution(0);
            FrameRateTimer.SetPeriod(100);
            FrameRateTimer.Tick += TimerTicked;
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            var app = OSD.GetAppEntries(AppFlags.MASK).Where(a => a.ProcessId == processEx.GetProcessId()).FirstOrDefault();
            if (app is null)
                FrameRateTimer.Stop();
            else
            {
                ProcessId = app.ProcessId;
                FrameRateTimer.Start();
            }
        }

        private int ProcessId = 0;
        private void TimerTicked(object? sender, EventArgs e)
        {
            var appE = OSD.GetAppEntries(AppFlags.MASK).Where(a => a.ProcessId == ProcessId).FirstOrDefault();
            if (appE is not null)
            {
                var duration = appE.InstantaneousTimeStart - appE.InstantaneousTimeEnd;
                Debug.WriteLine("{0} at {1}", Math.Round(duration / appE.InstantaneousFrameTime), appE.StatTimeStart.ToString());
            }
        }

        private void Watchdog_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                if (GetTargetFPS() != RequestedFramerate)
                    SetTargetFPS(RequestedFramerate);

                Monitor.Exit(updateLock);
            }
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            if (KeepAlive)
                Start();
        }

        public bool GetProfileProperty<T>(string propertyName, out T value)
        {
            var bytes = new byte[Marshal.SizeOf<T>()];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            value = default;
            try
            {
                if (!GetProfileProperty(propertyName, handle.AddrOfPinnedObject(), (uint)bytes.Length))
                    return false;

                value = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                handle.Free();
            }
        }

        public bool SetProfileProperty<T>(string propertyName, T value)
        {
            var bytes = new byte[Marshal.SizeOf<T>()];
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
                return SetProfileProperty(propertyName, handle.AddrOfPinnedObject(), (uint)bytes.Length);
            }
            catch
            {
                return false;
            }
            finally
            {
                handle.Free();
            }
        }

        public void UpdateSettings()
        {
            PostMessage(WM_RTSS_UPDATESETTINGS, IntPtr.Zero, IntPtr.Zero);
        }

        private void PostMessage(uint Msg, IntPtr wParam, IntPtr lParam)
        {
            var hWnd = FindWindow(null, "RTSS");
            if (hWnd == IntPtr.Zero)
                hWnd = FindWindow(null, "RivaTuner Statistics Server");

            if (hWnd != IntPtr.Zero)
                PostMessage(hWnd, Msg, wParam, lParam);
        }

        public uint EnableFlag(uint flag, bool status)
        {
            var current = SetFlags(~flag, status ? flag : 0);
            UpdateSettings();
            return current;
        }

        private bool SetTargetFPS(int Limit)
        {
            if (!IsRunning())
                return false;

            try
            {
                LoadProfile();

                if (SetProfileProperty("FramerateLimit", Limit))
                {
                    SaveProfile();
                    UpdateSettings();
                    UpdateProfiles();

                    return true;
                }
            }
            catch {}

            /*
            if (File.Exists(SettingsPath))
            {
                IniFile iniFile = new(SettingsPath);
                if (iniFile.Write("Limit", Limit.ToString(), "Framerate"))
                {
                    UpdateProfiles();
                    return true;
                }
            }
            */

            return false;
        }

        private int GetTargetFPS()
        {
            if (!IsRunning())
                return 0;

            try
            {
                LoadProfile();

                if (GetProfileProperty("FramerateLimit", out int fpsLimit))
                    return fpsLimit;
            }
            catch { }

            return 0;

            /*
            if (File.Exists(SettingsPath))
            {
                IniFile iniFile = new(SettingsPath);
                return Convert.ToInt32(iniFile.Read("Limit", "Framerate"));
            }
            */
        }

        public void RequestFPS(double framerate)
        {
            RequestedFramerate = (int)framerate;
        }

        public override bool Start()
        {
            if (!IsInstalled)
                return false;

            if (IsRunning())
                return false;

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = ExecutablePath,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process.WaitForInputIdle();

            return process is not null;
        }

        public override bool Stop()
        {
            if (!IsInstalled)
                return false;

            if (!IsRunning())
                return false;

            Process.Kill();

            return true;
        }

        public override void Dispose()
        {
            FrameRateTimer.Dispose();
            base.Dispose();
        }
    }
}