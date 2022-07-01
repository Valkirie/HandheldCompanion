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

        private string mchbar;

        // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) — Offset 59A0h
        private const string pnt_limit = "59";
        private const string pnt_clock = "94";

        public Rw()
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "Rw.exe");

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

        internal bool init_rw()
        {
            if (startInfo == null)
                return false;

            startInfo.Arguments = "/Min /Nologo /Stdout /command=\"rpci32 0 0 0 0x48;rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("0x"))
                        continue;

                    line = line.Substring(line.Length - 10);
                    mchbar = line.Substring(0, 6);
                    return true;
                }
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
            startInfo.Arguments = $"/Min /Nologo /Stdout /command=\"r16 {mchbar}{pnt_limit}{pointer};rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("0x"))
                        continue;

                    line = line.Substring(line.Length - 6);
                    var value = Convert.ToInt32(line, 16);
                    var output = ((double)value - 32768.0d) / 8.0d;
                    return (int)output;
                }
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
            string command = "/Min /Nologo /Stdout /command=\"";
            string hex = TDPToHex(limit);

            command += $"w16 {mchbar}{pnt_limit}{pointer1} 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
            command += $"wrmsr 0x610 0x0 {pointer2}{hex};";

            startInfo.Arguments = $"{command}rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();
                }
            }
        }

        internal void set_all_limit(int limit)
        {
            string command = "/Min /Nologo /Stdout /command=\"";
            string hex = TDPToHex(limit);

            // long
            command += $"w16 {mchbar}{pnt_limit}a4 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
            command += $"wrmsr 0x610 0x0 0x00438{hex.Substring(hex.Length - 3)};";

            // short
            command += $"w16 {mchbar}{pnt_limit}a0 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
            command += $"wrmsr 0x610 0x0 0x00dd8{hex.Substring(hex.Length - 3)};";

            startInfo.Arguments = $"{command}rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();
                }
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
            string command = "/Min /Nologo /Stdout /command=\"";
            string hex = ClockToHex(clock);

            command += $"w {mchbar}{pnt_clock} {hex};";

            startInfo.Arguments = $"{command}rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string line = ProcessOutput.StandardOutput.ReadLine();
                }
            }
        }
    }
}
