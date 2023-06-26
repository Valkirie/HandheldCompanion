using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Force.Crc32;
using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using static ControllerCommon.Utils.ProcessUtils;

namespace HandheldCompanion;

public static class XInputPlus
{
    private static readonly Dictionary<bool, uint> CRCs = new()
    {
        { false, 0xcd4906cc },
        { true, 0x1e9df650 }
    };

    private static readonly string IniContent = Resources.XInputPlus;

    static XInputPlus()
    {
    }

    public static void RegisterApplication(Profile profile)
    {
        var DirectoryPath = Path.GetDirectoryName(profile.Path);
        var IniPath = Path.Combine(DirectoryPath, "XInputPlus.ini");

        if (!CommonUtils.IsFileWritable(IniPath))
            return;
        
        File.WriteAllText(IniPath, IniContent);

        // we need to define Controller index overwrite
        XInputController controller = (XInputController)ControllerManager.GetVirtualControllers().FirstOrDefault(c => c.GetType() == typeof(XInputController));
        if (controller is null)
            return;

        // get virtual controller index
        var idx = controller.GetUserIndex() + 1;

        // make virtual controller new 1st controller
        var IniFile = new IniFile(IniPath);
        IniFile.Write("Controller1", Convert.ToString(idx), "ControllerNumber");

        // prepare index array and remove current index from it
        var userIndex = new List<int> { 1, 2, 3, 4 };
        userIndex.Remove(idx);

        for (var i = 0; i < userIndex.Count; i++)
        {
            var ControllerIdx = userIndex[i];
            IniFile.Write($"Controller{i + 2}", Convert.ToString(ControllerIdx), "ControllerNumber");
        }

        LogManager.LogDebug("XInputPlus controller index updated for {0}. Controller1 set to UserIndex: {1}",
            profile.Name, idx);

        // get binary type (x64, x86)
        BinaryType bt;
        GetBinaryType(profile.Path, out bt);
        var x64 = bt == BinaryType.SCS_64BIT_BINARY;

        for (var i = 0; i < 5; i++)
        {
            var dllpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
            var backpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

            // dll has a different naming format
            if (i == 4)
            {
                dllpath = Path.Combine(DirectoryPath, "xinput9_1_0.dll");
                backpath = Path.Combine(DirectoryPath, "xinput9_1_0.back");
            }

            var dllexist = File.Exists(dllpath);
            var backexist = File.Exists(backpath);

            byte[] inputData = { 0 };

            // check CRC32
            if (dllexist) inputData = File.ReadAllBytes(dllpath);
            var crc = Crc32Algorithm.Compute(inputData);
            var is_x360ce = CRCs[x64] == crc;

            // pull data from dll
            var outputData = x64 ? Resources.xinput1_x64 : Resources.xinput1_x86;

            if (dllexist && is_x360ce) continue; // skip to next file

            if (!dllexist)
            {
                File.WriteAllBytes(dllpath, outputData);
            }
            else if (dllexist && !is_x360ce)
            {
                // create backup of current dll
                if (!backexist)
                    File.Move(dllpath, backpath, true);

                // deploy wrapper
                if (CommonUtils.IsFileWritable(dllpath))
                    File.WriteAllBytes(dllpath, outputData);
            }
        }
    }

    public static void UnregisterApplication(Profile profile)
    {
        var DirectoryPath = Path.GetDirectoryName(profile.Path);
        var IniPath = Path.Combine(DirectoryPath, "XInputPlus.ini");

        // get binary type (x64, x86)
        BinaryType bt;
        GetBinaryType(profile.Path, out bt);
        var x64 = bt == BinaryType.SCS_64BIT_BINARY;

        for (var i = 0; i < 5; i++)
        {
            var dllpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
            var backpath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

            // dll has a different naming format
            if (i == 4)
            {
                dllpath = Path.Combine(DirectoryPath, "xinput9_1_0.dll");
                backpath = Path.Combine(DirectoryPath, "xinput9_1_0.back");
            }

            var dllexist = File.Exists(dllpath);
            var backexist = File.Exists(backpath);

            byte[] inputData = { 0 };

            // check CRC32
            if (dllexist) inputData = File.ReadAllBytes(dllpath);
            var crc = Crc32Algorithm.Compute(inputData);
            var is_x360ce = CRCs[x64] == crc;

            // restore backup is exists
            if (backexist)
                File.Move(backpath, dllpath, true);
        }
    }
}