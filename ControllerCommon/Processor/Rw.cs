using ControllerCommon.Managers;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Processor
{
    public class Rw
    {
        private ProcessStartInfo startInfo;
        private string path;

        public Rw()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "Rw.exe");

            if (!File.Exists(path))
                LogManager.LogError("Rw.exe is missing. Power Manager won't work.");

            startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        internal string init_rw()
        {
            startInfo.Arguments = "/Min /Nologo /Stdout /command=\"Delay 1000;rpci32 0 0 0 0x48;Delay 1000;rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("0x"))
                        continue;

                    var MCHBAR = line.Substring(line.Length - 10);
                    return MCHBAR.Substring(0, 6) + "59";
                }
            }

            return null;
        }

        internal int get_short_limit()
        {
            return 0;
        }

        internal int get_long_limit()
        {
            return 0;
        }

        internal int get_short_value()
        {
            return 0;
        }

        internal int get_long_value()
        {
            return 0;
        }

        internal void set_short_limit(int limit)
        {
            // do something
        }

        internal void set_long_limit(int limit)
        {
            // do something
        }
    }
}
