using ControllerCommon.Managers;
using ControllerCommon.Processor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon
{
    public static class LegacyDevcon
    {
        private static readonly string path;
        private static bool _IsInstalled;

        static LegacyDevcon()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "devcon.exe");

            if (!File.Exists(path))
            {
                LogManager.LogError("devcon.exe is missing.");
                return;
            }

            _IsInstalled = true;
        }

        public static bool Restart(string InstanceId)
        {
            if (!_IsInstalled)
                return false;

            string output = string.Empty;
            using (Process process = new Process())
            {
                process.StartInfo.FileName = path;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // register command
                process.StartInfo.Arguments = $"restart \"{InstanceId}\"";

                process.Start();

                StreamReader reader = process.StandardOutput;
                output = reader.ReadToEnd();

                process.WaitForExit();
            }

            if (output.Contains("No matching devices found."))
                return false;
            else if (output.Contains("Restarted"))
                return true;

            return false;
        }
    }
}
