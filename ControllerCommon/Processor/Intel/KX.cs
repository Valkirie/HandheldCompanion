using ControllerCommon.Managers;
using ControllerCommon.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ControllerCommon.Processor.Intel
{
    public class KX
    {
        private ProcessStartInfo startInfo;
        private string path;

        private string mchbar;

        // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) — Offset 59A0h
        private const string pnt_limit = "59";
        private const string pnt_clock = "94";

        public KX()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Intel", "KX", "KX.exe");

            if (!File.Exists(path))
            {
                LogManager.LogError("Rw.exe is missing. Power Manager won't work.");
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

        internal bool init()
        {
            if (startInfo == null)
                return false;

            startInfo.Arguments = "/RdPci32 0 0 0 0x48";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Return ");
                    long returned = long.Parse(line);
                    string output = "0x" + returned.ToString("X2").Substring(0, 4);

                    mchbar = output;

                    ProcessOutput.Close();
                    return true;
                }
                ProcessOutput.Close();
            }

            return false;
        }

        internal int get_short_limit()
        {
            return get_limit("a4");
        }

        internal int get_long_limit()
        {
            return get_limit("a0");
        }

        internal int get_limit(string pointer)
        {
            startInfo.Arguments = $"/rdmem16 {mchbar}{pnt_limit}{pointer}";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Return ");
                    long returned = long.Parse(line);
                    var output = ((double)returned + short.MinValue) / 8.0d;

                    ProcessOutput.Close();
                    return (int)output;
                }
                ProcessOutput.Close();
            }

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
            set_limit("a4", "0x00438", limit);
        }

        internal void set_long_limit(int limit)
        {
            set_limit("a0", "0x00dd8", limit);
        }

        internal void set_limit(string pointer1, string pointer2, int limit)
        {
            string hex = TDPToHex(limit);

            string command = $"/wrmem16 {mchbar}{pnt_limit}{pointer1} 0x8{hex.Substring(0, 1)}{hex.Substring(1)}";

            startInfo.Arguments = command;
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();
                    break;
                }
                ProcessOutput.Close();
            }
        }

        private string TDPToHex(int decValue)
        {
            decValue *= 8;
            string output = decValue.ToString("X3");
            return output;
        }

        private string ClockToHex(int decValue)
        {
            decValue /= 50;
            string output = "0x" + decValue.ToString("X2");
            return output;
        }

        internal void set_gfx_clk(int clock)
        {
            string hex = ClockToHex(clock);

            string command = $"/wrmem8 {mchbar}{pnt_clock} {hex}";

            startInfo.Arguments = command;
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();
                    break;
                }
                ProcessOutput.Close();
            }
        }
    }
}
