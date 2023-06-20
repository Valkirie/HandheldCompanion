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
        private static readonly ProcessStartInfo startInfo;

        static LegacyDevcon()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "devcon.exe");

            if (!File.Exists(path))
            {
                LogManager.LogError("devcon.exe is missing.");
                return;
            }

            startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        public static bool Restart(string InstanceId)
        {
            // register command
            startInfo.Arguments = $"restart \"{InstanceId}\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                string output = ProcessOutput.StandardOutput.ReadToEnd();

                ProcessOutput.WaitForExit();

                if (output.Contains("No matching devices found."))
                    return false;
                else if (output.Contains("Restarted"))
                    return true;
            }

            return false;
        }
    }
}
