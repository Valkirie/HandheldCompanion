using HandheldCompanion.Shared;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Timers;

namespace HandheldCompanion.Watchers
{
    public class OneXConsoleWatcher : ISpaceWatcher
    {
        public OneXConsoleWatcher()
        {
            // OneXConsole registers a logon scheduled task named "OneXConsole" (no Windows service).
            // OneXConsole.exe is the Electron frontend, CompatLayerCT.exe is its hardware backend.
            taskNames = new() { "OneXConsole" };
            executableNames = new() { "OneXConsole", "CompatLayerCT" };

            // set notification
            notification = new(
                Properties.Resources.Hint_OneXConsoleCheck,
                Properties.Resources.Hint_OneXConsoleCheckDesc,
                string.Empty,
                InfoBarSeverity.Warning);

            // prepare timer
            watchdogTimer = new Timer(4000);
            watchdogTimer.Elapsed += WatchdogTimer_Elapsed;
        }

        // OneXConsole keeps its "start on power" preference in an SQLite database and, on every launch,
        // (re)creates or deletes the "OneXConsole" logon task from it. That setting is the source of truth.
        private static readonly string SettingsDatabase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gamerzone", "db", "core.db");

        private static readonly string ExecutablePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OneXConsole", "OneXConsole.exe");

        // OneXConsole is optional on OneXPlayer devices, only watch it where it is installed
        public static bool IsInstalled => File.Exists(ExecutablePath);

        public override void Enable()
        {
            SetAutoStartup(true);

            // OneXConsole deletes its task when autostart is off, and it skips the task sync when launched
            // with --startupHidden, so recreate the task exactly as OneXConsole does before re-enabling it.
            if (!GetTasks().Any())
                CreateStartupTask();

            base.Enable();

            // fallback: task could not be created/run, start OneXConsole directly
            if (!HasProcesses() && File.Exists(ExecutablePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(ExecutablePath, "--startupHidden") { UseShellExecute = true })?.Dispose();
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to start OneXConsole: {0}", ex.Message);
                }
            }
        }

        public override void Disable()
        {
            // kills OneXConsole first so it can't overwrite the setting on exit
            base.Disable();
            SetAutoStartup(false);
        }

        private static void CreateStartupTask()
        {
            string startupScript = Path.Combine(Path.GetDirectoryName(ExecutablePath)!, "resources", "resources", "tools", "OneXConsoleStartup.vbs");
            if (!File.Exists(startupScript))
                return;

            string? userSid = WindowsIdentity.GetCurrent().User?.Value;
            if (string.IsNullOrEmpty(userSid))
                return;

            // mirrors OneXConsole's CreateAutoStartupTask (CompatLayerCT HardwareService)
            string xml =
                "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
                "  <RegistrationInfo>\r\n    <Author>OneXConsole</Author>\r\n    <URI>\\OneXConsole</URI>\r\n  </RegistrationInfo>\r\n" +
                "  <Triggers>\r\n    <LogonTrigger>\r\n      <Enabled>true</Enabled>\r\n    </LogonTrigger>\r\n  </Triggers>\r\n" +
                "  <Principals>\r\n    <Principal id=\"Author\">\r\n      <UserId>" + userSid + "</UserId>\r\n      <RunLevel>HighestAvailable</RunLevel>\r\n    </Principal>\r\n  </Principals>\r\n" +
                "  <Settings>\r\n    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
                "    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n    <AllowHardTerminate>true</AllowHardTerminate>\r\n    <StartWhenAvailable>true</StartWhenAvailable>\r\n" +
                "    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n    <Enabled>true</Enabled>\r\n    <Hidden>false</Hidden>\r\n    <Priority>4</Priority>\r\n  </Settings>\r\n" +
                "  <Actions Context=\"Author\">\r\n    <Exec>\r\n      <Command>wscript.exe</Command>\r\n      <Arguments>\"" + startupScript + "\"</Arguments>\r\n    </Exec>\r\n  </Actions>\r\n" +
                "</Task>";

            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, xml, System.Text.Encoding.Unicode);

                using Process process = new()
                {
                    StartInfo = new ProcessStartInfo("schtasks.exe", $"/create /tn \"OneXConsole\" /xml \"{tempFile}\" /f")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    LogManager.LogError("Failed to create OneXConsole startup task: {0}", error);
                else
                    LogManager.LogInformation("OneXConsole startup task created");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create OneXConsole startup task: {0}", ex.Message);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        private static void SetAutoStartup(bool enabled)
        {
            if (!File.Exists(SettingsDatabase))
                return;

            try
            {
                using SqliteConnection connection = new($"Data Source={SettingsDatabase};Mode=ReadWrite;Pooling=False;Default Timeout=5");
                connection.Open();

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE setting SET autoStartup = $value";
                command.Parameters.AddWithValue("$value", enabled ? 1 : 0);
                int rows = command.ExecuteNonQuery();

                LogManager.LogInformation("OneXConsole autoStartup set to {0} ({1} row(s))", enabled, rows);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to update OneXConsole autoStartup setting: {0}", ex.Message);
            }
        }

        public override void Start()
        {
            watchdogTimer?.Start();
            base.Start();
        }

        public override void Stop()
        {
            watchdogTimer?.Stop();
            base.Stop();
        }

        private void WatchdogTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            bool status = IsRunning;
            if (status != prevStatus)
            {
                prevStatus = status;
                UpdateStatus(status);
            }
        }
    }
}
