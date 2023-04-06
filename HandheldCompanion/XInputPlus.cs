using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using Force.Crc32;
using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ControllerCommon.Utils.ProcessUtils;
using static PInvoke.Kernel32;

namespace HandheldCompanion
{
    public static class XInputPlus
    {
        static Dictionary<bool, uint> CRCs = new Dictionary<bool, uint>()
        {
            { false, 0xcd4906cc },
            { true, 0x1e9df650 },
        };

        static string IniContent = Properties.Resources.XInputPlus;

        static XInputPlus()
        {
        }

        public static void RegisterApplication(Profile profile)
        {
            string DirectoryPath = Path.GetDirectoryName(profile.Path);
            string IniPath = Path.Combine(DirectoryPath, "XInputPlus.ini");

            if (!File.Exists(IniPath))
                File.WriteAllText(IniPath, IniContent);

            // we need to define Controller index overwrite
            IController controller = ControllerManager.GetTargetController();
            if (controller.GetType() == typeof(XInputController))
            {
                XInputController XController = (XInputController)controller;
                int idx = XController.GetUserIndex() + 1;
                
                IniFile IniFile = new IniFile(IniPath);
                IniFile.Write("Controller1", Convert.ToString(idx), "ControllerNumber");

                List<int> userIndex = new List<int>() { 1, 2, 3, 4 };
                userIndex.Remove(idx);

                for (int i = 0; i < 3; i++)
                {
                    var ControllerIdx = userIndex[i];
                    IniFile.Write($"Controller{i + 2}", Convert.ToString(ControllerIdx), "ControllerNumber");
                }

                LogManager.LogDebug("XInputPlus controller index updated for {0}. Controller1 set to UserIndex: {1}", profile.Name, idx);
            }

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(profile.Path, out bt);
            bool x64 = bt == BinaryType.SCS_64BIT_BINARY;

            for (int i = 0; i < 5; i++)
            {
                string dllpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
                string backpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

                // dll has a different naming format
                if (i == 4)
                {
                    dllpath = Path.Combine(DirectoryPath, $"xinput9_1_0.dll");
                    backpath = Path.Combine(DirectoryPath, $"xinput9_1_0.back");
                }

                bool dllexist = File.Exists(dllpath);
                bool backexist = File.Exists(backpath);

                byte[] inputData = new byte[] { 0 };

                // check CRC32
                if (dllexist) inputData = File.ReadAllBytes(dllpath);
                var crc = Crc32Algorithm.Compute(inputData);
                bool is_x360ce = CRCs[x64] == crc;

                // pull data from dll
                var outputData = x64 ? Properties.Resources.xinput1_x64 : Properties.Resources.xinput1_x86;

                if (dllexist && is_x360ce)
                    continue; // skip to next file
                else if (!dllexist)
                    File.WriteAllBytes(dllpath, outputData);
                else if (dllexist && !is_x360ce)
                {
                    // create backup of current dll
                    if (!backexist)
                        File.Move(dllpath, backpath, true);

                    // deploy wrapper
                    File.WriteAllBytes(dllpath, outputData);
                }
            }
        }

        public static void UnregisterApplication(Profile profile)
        {
            string DirectoryPath = Path.GetDirectoryName(profile.Path);
            string IniPath = Path.Combine(DirectoryPath, "XInputPlus.ini");

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(profile.Path, out bt);
            bool x64 = bt == BinaryType.SCS_64BIT_BINARY;

            for (int i = 0; i < 5; i++)
            {
                string dllpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
                string backpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

                // dll has a different naming format
                if (i == 4)
                {
                    dllpath = Path.Combine(DirectoryPath, $"xinput9_1_0.dll");
                    backpath = Path.Combine(DirectoryPath, $"xinput9_1_0.back");
                }

                bool dllexist = File.Exists(dllpath);
                bool backexist = File.Exists(backpath);

                byte[] inputData = new byte[] { 0 };

                // check CRC32
                if (dllexist) inputData = File.ReadAllBytes(dllpath);
                var crc = Crc32Algorithm.Compute(inputData);
                bool is_x360ce = CRCs[x64] == crc;

                // restore backup is exists
                if (backexist)
                    File.Move(backpath, dllpath, true);
            }
        }
    }
}
