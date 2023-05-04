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
using System.Threading.Tasks;
using static HandheldCompanion.Platforms.RTSS;

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

        public event HookedEventHandler Hooked;
        public delegate void HookedEventHandler(int processId);

        public event UnhookedEventHandler Unhooked;
        public delegate void UnhookedEventHandler(int processId);

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
                LogManager.LogWarning("Rivatuner Statistics Server is missing. Please get it from: {0}", "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html");
                return;
            }

            if (!HasModules)
            {
                LogManager.LogWarning("Rivatuner Statistics Server RTSSHooks64.dll is missing. Please get it from: {0}", "https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html");
                return;
            }

            // start RTSS if not running
            if (IsRunning())
                Stop();
            Start();

            // our main watchdog to (re)apply requested settings
            base.PlatformWatchdog = new(2000) { Enabled = true };
            base.PlatformWatchdog.Elapsed += Watchdog_Elapsed;

            // hook into process manager
            ProcessManager.ProcessStarted += ProcessManager_ProcessStartedAsync;
            ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        }

        private async void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            // unhook previous process
            if (backgroundEx is not null)
                Unhooked?.Invoke(backgroundEx.GetProcessId());

            // hook new process
            AppEntry appEntry = null;

            var ProcessId = processEx.GetProcessId();
            if (ProcessId == 0)
                return;

            do
            {
                try
                {
                    appEntry = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None).Where(a => a.ProcessId == ProcessId).FirstOrDefault();
                    if (processEx.Process.HasExited)
                        break;
                }
                catch (InvalidOperationException) { }
                catch (FileNotFoundException) { }

                await Task.Delay(250);
            }
            while (appEntry is null);

            Hooked?.Invoke(ProcessId);
        }

        private async void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            // do something
        }

        private async void ProcessManager_ProcessStartedAsync(ProcessEx processEx, bool OnStartup)
        {
            // do something
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

        public double GetInstantaneousFramerate(int processId)
        {
            try
            {
                var appE = OSD.GetAppEntries().Where(x => (x.Flags & AppFlags.MASK) != AppFlags.None).Where(a => a.ProcessId == processId).FirstOrDefault();
                if (appE is null)
                    return 0.0d;

                // todo: @Casper fix me
                double duration = appE.InstantaneousTimeStart - appE.InstantaneousTimeEnd;
                return Math.Round(duration / appE.InstantaneousFrameTime);
            }
            catch (FileNotFoundException ex) { }

            return 0.0d;
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

            try
            {
                // set lock
                IsStarting = true;

                var process = Process.Start(new ProcessStartInfo()
                {
                    FileName = ExecutablePath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is not null)
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;

                    process.WaitForInputIdle();

                    // release lock
                    IsStarting = false;
                }

                return true;
            }
            catch {}

            return false;
        }

        public override bool Stop()
        {
            if (IsStarting)
                return false;
            if (!IsInstalled)
                return false;
            if (!IsRunning())
                return false;

            Process.Kill();

            return true;
        }

        public override void Dispose()
        {
            PlatformWatchdog.Stop();

            base.Dispose();
        }
    }
}