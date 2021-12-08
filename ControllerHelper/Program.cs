using CommandLine;
using ControllerCommon;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic.ApplicationServices;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ControllerHelper.Options;

namespace ControllerHelper
{
    static class Program
    {
        #region imports

        private enum ShowWindowEnum
        {
            Hide = 0,
            ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
            Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
            Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
            Restore = 9, ShowDefault = 10, ForceMinimized = 11
        };

        private struct Windowplacement
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowTitle);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);
        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref Windowplacement lpwndpl);
        #endregion

        static ControllerHelper MainForm;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(params string[] Arguments)
        {
            string proc = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(proc);

            if (processes.Length > 1 && Arguments.Length == 0)
            {
                // an instance of helper is already running and no arguments were given
                BringWindowToFront(proc);
                return;
            }
            else if (processes.Length > 1 && Arguments.Length != 0)
            {
                // an instance of helper is already running, pass arguments to it
                MainForm = new ControllerHelper();
                SingleInstanceApplication.Run(MainForm, NewInstanceHandler);
            }
            else
            {
                // no instance of helper is running
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                var configuration = new ConfigurationBuilder()
                            .AddJsonFile("helpersettings.json")
                            .Build();

                var serilogLogger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                var microsoftLogger = new SerilogLoggerFactory(serilogLogger).CreateLogger("ControllerHelper");

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                MainForm = new ControllerHelper(microsoftLogger);
                SingleInstanceApplication.Run(MainForm, NewInstanceHandler);
            }
        }

        public static void NewInstanceHandler(object sender, StartupNextInstanceEventArgs e)
        {
            string[] args = new string[e.CommandLine.Count - 1];
            Array.Copy(e.CommandLine.ToArray(), 1, args, 0, e.CommandLine.Count - 1);

            Parser.Default.ParseArguments<ProfileOption>(args).MapResult(
                (ProfileOption opts) => RunProfile(opts),
                errs => RunError(errs)
                );
            e.BringToForeground = false;
        }

        private static bool RunError(IEnumerable<Error> errs)
        {
            // do something
            return true;
        }

        private static bool RunProfile(ProfileOption opts)
        {
            if (!File.Exists(opts.exe))
                return false;

            string ProcessExec = Path.GetFileNameWithoutExtension(opts.exe);

            Profile profile = new Profile(ProcessExec, opts.exe);
            
            if (MainForm.ProfileManager.profiles.ContainsKey(ProcessExec))
                profile = MainForm.ProfileManager.profiles[ProcessExec];

            profile.fullpath = opts.exe;

            switch(opts.mode)
            {
                case "xinput":
                    profile.whitelisted = true;
                    break;
                case "ds4":
                    profile.whitelisted = false;
                    break;
                default:
                    return false;
            }

            MainForm.ProfileManager.UpdateProfile(profile);
            MainForm.ProfileManager.SerializeProfile(profile);
            return true;
        }

        public class SingleInstanceApplication : WindowsFormsApplicationBase
        {
            private SingleInstanceApplication()
            {
                base.IsSingleInstance = true;
            }

            public static void Run(Form f, StartupNextInstanceEventHandler startupHandler)
            {
                SingleInstanceApplication app = new SingleInstanceApplication();
                app.MainForm = f;
                app.StartupNextInstance += startupHandler;
                app.Run(Environment.GetCommandLineArgs());
            }
        }

        private static void BringWindowToFront(string name)
        {
            IntPtr wdwIntPtr = FindWindow(null, name);

            //get the hWnd of the process
            Windowplacement placement = new Windowplacement();
            GetWindowPlacement(wdwIntPtr, ref placement);

            // Check if window is minimized
            if (placement.showCmd == 2)
            {
                //the window is hidden so we restore it
                ShowWindow(wdwIntPtr, ShowWindowEnum.Restore);
            }

            //set user's focus to the window
            SetForegroundWindow(wdwIntPtr);
        }
    }
}
