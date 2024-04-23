using Force.Crc32;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static HandheldCompanion.Utils.ProcessUtils;
using static HandheldCompanion.Utils.XInputPlusUtils;

namespace HandheldCompanion;

// thanks 0dd14
// https://github.com/0dd14/XInputPlus-Injector-Sample/tree/master/XIPlusInjector

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class XInputPlusLoaderSetting
{
    /* C/C++ Struct
        typedef struct XInputPlusLoaderSetting
        {
            WCHAR LoaderDLL32[MAX_PATH];
            WCHAR LoaderDLL64[MAX_PATH];
            WCHAR XInputDLL32[MAX_PATH];
            WCHAR DInputDLL32[MAX_PATH];
            WCHAR DInput8DLL32[MAX_PATH];
            WCHAR XInputDLL64[MAX_PATH];
            WCHAR DInputDLL64[MAX_PATH];
            WCHAR DInput8DLL64[MAX_PATH];
            WCHAR TargetProgram[MAX_PATH];
            WCHAR LoaderDir[MAX_PATH];
            bool HookChildProcess;
            bool Launched;
        }
 */
    const int MAX_PATH = 260;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string LoaderDLL32;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string LoaderDLL64;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string XInputDLL32;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string DInputDLL32;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string DInput8DLL32;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string XInputDLL64;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string DInputDLL64;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string DInput8DLL64;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string TargetProgram;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)] public string LoaderDir;
    [MarshalAs(UnmanagedType.U1)] public bool HookChildProcess;
    [MarshalAs(UnmanagedType.U1)] public bool Launched;
}

public static class XInputPlus
{
    // todo: move me
    [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

    private static readonly Dictionary<bool, uint> CRCs = new()
    {
        { false, 0xcd4906cc },
        { true, 0x1e9df650 }
    };

    // XInputPlus main directory
    private static readonly string XInputPlusDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "XInputPlus");

    // XInputPlus Loader (injector)
    private static readonly string XInputPlus_InjectorDir = Path.Combine(XInputPlusDir, "Loader");
    private static readonly string XInputPlus_Injectorx86 = Path.Combine(XInputPlus_InjectorDir, "XInputPlusInjector.dll");
    private static readonly string XInputPlus_Injectorx64 = Path.Combine(XInputPlus_InjectorDir, "XInputPlusInjector64.dll");

    // XInputPlus XInput/DInput x86
    private static readonly string XInputPlus_x86Dir = Path.Combine(XInputPlusDir, "x86");
    private static readonly string XInputPlus_XInputx86 = Path.Combine(XInputPlus_x86Dir, "xinput1_3.dl_");
    private static readonly string XInputPlus_DInputx86 = Path.Combine(XInputPlus_x86Dir, "dinput.dl_");
    private static readonly string XInputPlus_DInput8x86 = Path.Combine(XInputPlus_x86Dir, "dinput8.dl_");

    // XInputPlus XInput/Dinput x64
    private static readonly string XInputPlus_x64Dir = Path.Combine(XInputPlusDir, "x64");
    private static readonly string XInputPlus_XInputx64 = Path.Combine(XInputPlus_x64Dir, "xinput1_3.dl_");
    private static readonly string XInputPlus_DInputx64 = Path.Combine(XInputPlus_x64Dir, "dinput.dl_");
    private static readonly string XInputPlus_DInput8x64 = Path.Combine(XInputPlus_x64Dir, "dinput8.dl_");

    private static readonly string IniContent = Resources.XInputPlus;

    static XInputPlus()
    {
        ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
    }

    // this should be handled by the installer at some point.
    public static void ExtractXInputPlusLibraries()
    {
        if (!Directory.Exists(XInputPlusDir))
            Directory.CreateDirectory(XInputPlusDir);

        if (!Directory.Exists(XInputPlus_InjectorDir))
            Directory.CreateDirectory(XInputPlus_InjectorDir);

        if (!Directory.Exists(XInputPlus_x86Dir))
            Directory.CreateDirectory(XInputPlus_x86Dir);

        if (!Directory.Exists(XInputPlus_x64Dir))
            Directory.CreateDirectory(XInputPlus_x64Dir);

        if (!File.Exists(XInputPlus_Injectorx86))
            File.WriteAllBytes(XInputPlus_Injectorx86, Resources.XInputPlusInjector);

        if (!File.Exists(XInputPlus_Injectorx64))
            File.WriteAllBytes(XInputPlus_Injectorx64, Resources.XInputPlusInjector64);

        if (!File.Exists(XInputPlus_XInputx86))
            File.WriteAllBytes(XInputPlus_XInputx86, Resources.xinput1_x86);

        if (!File.Exists(XInputPlus_XInputx64))
            File.WriteAllBytes(XInputPlus_XInputx64, Resources.xinput1_x64);

        // todo: add support for xinputplus dinput libraries
    }

    private static async void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
    {
        try
        {
            // get related profile
            Profile profile = ProfileManager.GetProfileFromPath(processEx.Path, true);

            if (profile.XInputPlus != XInputPlusMethod.Injection)
                return;

            int attempt = 0;
            while (!ProcessManager.CheckXInput(processEx.Process))
            {
                attempt++;

                if (attempt == 10)
                    return;

                await Task.Delay(500);
            }

            bool x64bit = Is64bitProcess(processEx.Process);
            WriteXInputPlusINI(XInputPlus_InjectorDir, x64bit);
            InjectXInputPlus(processEx.Process, x64bit);
        }
        catch (Exception ex)
        {
            LogManager.LogError("Error when injecting XInputPlus to {0}: {1}", processEx.Executable, ex.Message);
        }
    }

    public static void InjectXInputPlus(Process targetProcess, bool x64bit)
    {
        XInputPlusLoaderSetting setting = new XInputPlusLoaderSetting();

        setting.LoaderDLL32 = XInputPlus_Injectorx86;
        setting.LoaderDLL64 = XInputPlus_Injectorx64;
        setting.XInputDLL32 = XInputPlus_XInputx86;
        setting.DInputDLL32 = XInputPlus_DInputx86;                         // unused
        setting.DInput8DLL32 = XInputPlus_DInput8x86;                       // unused
        setting.XInputDLL64 = XInputPlus_XInputx64;
        setting.DInputDLL64 = XInputPlus_DInputx64;                         // unused
        setting.DInput8DLL64 = XInputPlus_DInput8x64;                       // unused
        setting.TargetProgram = "";                                         // Internal use
        setting.LoaderDir = XInputPlus_InjectorDir;                         // "XInputPlus.ini" in this folder is used
        setting.HookChildProcess = false;
        setting.Launched = false;                                           // Internal use

        // Write Unmanaged Struct to MemoryMappedFile
        using (MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("XInputPlusLoader", Marshal.SizeOf(typeof(XInputPlusLoaderSetting))))
        {
            using (MemoryMappedViewStream mmvs = mmf.CreateViewStream())
            {
                // Struct to byte[]
                int buffsize = Marshal.SizeOf(typeof(XInputPlusLoaderSetting));
                byte[] buff = new byte[buffsize];
                IntPtr ptr = Marshal.AllocCoTaskMem(buffsize);
                Marshal.StructureToPtr(setting, ptr, false);
                Marshal.Copy(ptr, buff, 0, buffsize);
                Marshal.FreeCoTaskMem(ptr);

                // Write to MemoryMappledFile
                mmvs.Write(buff, 0, buffsize);
                mmvs.Flush();
            }

            string XInputInjectorDLL;

            // using this native api function because sometimes we can't access the exe (such as UWP games)
            XInputInjectorDLL = x64bit ? XInputPlus_Injectorx64 : XInputPlus_Injectorx86;

            // using rundll32.exe because we can't load a 32 bit library in a 64 bit application
            string args = $"\"{XInputInjectorDLL}\",HookProcess {targetProcess.Id}";
            Process p = Process.Start("rundll32.exe", args);
            p.WaitForExit();
        }
    }

    private static bool CheckDeployment(string DirectoryPath)
    {
        for (var i = 0; i < 5; i++)
        {
            string XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");

            // dll has a different naming format
            if (i == 4)
                XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, "xinput9_1_0.dll");

            if (FileUtils.IsFileUsed(XInputPlusDLLTargetPath))
                return false;

            if (!FileUtils.IsFileWritable(XInputPlusDLLTargetPath))
                return false;
        }

        return true;
    }

    public static bool RegisterApplication(Profile profile)
    {
        string DirectoryPath = Path.GetDirectoryName(profile.Path);
        if (string.IsNullOrEmpty(DirectoryPath))
            return false;

        // get binary type (x64, x86)
        BinaryType bt;
        GetBinaryType(profile.Path, out bt);
        bool x64bit = bt == BinaryType.SCS_64BIT_BINARY;

        // prepare ini file
        if (!WriteXInputPlusINI(DirectoryPath, x64bit))
            return false;

        // check if dll files can be deployed
        if (!CheckDeployment(DirectoryPath))
        {
            LogManager.LogError("XInputPlus, RegisterApplication failed: {0}", DirectoryPath);
            return false;
        }

        // prepare dll files
        try
        {
            for (var i = 0; i < 5; i++)
            {
                string XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
                string XInputPlusDLLTargetBackPath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

                // dll has a different naming format
                if (i == 4)
                {
                    XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, "xinput9_1_0.dll");
                    XInputPlusDLLTargetBackPath = Path.Combine(DirectoryPath, "xinput9_1_0.back");
                }

                bool dllexist = File.Exists(XInputPlusDLLTargetPath);
                bool backexist = File.Exists(XInputPlusDLLTargetBackPath);

                byte[] inputData = { 0 };

                // check CRC32
                if (dllexist) inputData = File.ReadAllBytes(XInputPlusDLLTargetPath);
                uint crc = Crc32Algorithm.Compute(inputData);
                bool is_x360ce = CRCs[x64bit] == crc;

                // pull data from dll
                string XInputPlusDLLSrcPath = x64bit ? XInputPlus_XInputx64 : XInputPlus_XInputx86;

                if (dllexist && is_x360ce) continue; // skip to next file

                if (!dllexist)
                {
                    //File.WriteAllBytes(dllpath, outputData);
                    File.Copy(XInputPlusDLLSrcPath, XInputPlusDLLTargetPath);
                }
                else if (dllexist && !is_x360ce)
                {
                    // create backup of current dll
                    if (!backexist)
                        File.Move(XInputPlusDLLTargetPath, XInputPlusDLLTargetBackPath, true);

                    // deploy wrapper
                    File.Copy(XInputPlusDLLSrcPath, XInputPlusDLLTargetPath, true);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool UnregisterApplication(Profile profile)
    {
        string DirectoryPath = Path.GetDirectoryName(profile.Path);
        if (string.IsNullOrEmpty(DirectoryPath))
            return false;

        // check if dll files can be deployed
        if (!CheckDeployment(DirectoryPath))
        {
            LogManager.LogError("XInputPlus, UnregisterApplication failed: {0}", DirectoryPath);
            return false;
        }

        // remove dll files
        try
        {
            for (var i = 0; i < 5; i++)
            {
                string XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.dll");
                string XInputPlusDLLTargetBackupPath = Path.Combine(DirectoryPath, $"xinput1_{i + 1}.back");

                // dll has a different naming format
                if (i == 4)
                {
                    XInputPlusDLLTargetPath = Path.Combine(DirectoryPath, "xinput9_1_0.dll");
                    XInputPlusDLLTargetBackupPath = Path.Combine(DirectoryPath, "xinput9_1_0.back");
                }

                bool dllexist = File.Exists(XInputPlusDLLTargetPath);
                bool backexist = File.Exists(XInputPlusDLLTargetBackupPath);

                if (backexist)
                {
                    // restore original DLL
                    File.Move(XInputPlusDLLTargetBackupPath, XInputPlusDLLTargetPath, true);
                }
                else if (dllexist)
                {
                    // clean up XInputPlus files
                    FileUtils.FileDelete(XInputPlusDLLTargetPath);
                }
            }

            // remove XInputPlus INI file
            string IniPath = Path.Combine(DirectoryPath, "XInputPlus.ini");
            FileUtils.FileDelete(IniPath);
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static object writeLock = new();
    public static bool WriteXInputPlusINI(string directoryPath, bool x64bit)
    {
        lock (writeLock)
        {
            string IniPath = Path.Combine(directoryPath, "XInputPlus.ini");

            if (!FileUtils.IsFileWritable(IniPath))
                return false;

            // prepare index array
            List<int> userIndex = new() { 1, 2, 3, 4 };

            // prepare IniFile
            try
            {
                File.WriteAllText(IniPath, IniContent);
                IniFile IniFile = new IniFile(IniPath);
                IniFile.Write("FileVersion", x64bit ? "X64" : "X86", "Misc");

                // reset controller index values
                for (int i = 0; i < userIndex.Count; i++)
                    IniFile.Write($"Controller{i + 1}", "0", "ControllerNumber");

                // we need to define Controller index overwrite
                XInputController vController = ControllerManager.GetVirtualControllers().OfType<XInputController>().FirstOrDefault();
                if (vController is null)
                    return false;

                // get virtual controller index and update IniFile
                int idx = vController.GetUserIndex() + 1;
                IniFile.Write("Controller1", Convert.ToString(idx), "ControllerNumber");

                // remove virtual controller index from it
                userIndex.Remove(idx);

                // remove all hidden physical controllers from the list
                foreach (XInputController pController in ControllerManager.GetPhysicalControllers().OfType<XInputController>().Where(c => c.IsHidden()))
                    userIndex.Remove(pController.GetUserIndex() + 1);

                for (int i = 0; i < userIndex.Count; i++)
                {
                    int cIdx = userIndex[i];
                    IniFile.Write($"Controller{i + 2}", Convert.ToString(cIdx), "ControllerNumber");
                }

                LogManager.LogDebug("XInputPlus INI wrote in {0}. Controller1 set to UserIndex: {1}",
                    directoryPath, idx);

                return true;
            }
            catch { return false; }
        }
    }

    // https://msdn.microsoft.com/en-us/library/windows/desktop/ms684139%28v=vs.85%29.aspx
    public static bool Is64bitProcess(Process process)
    {
        if (!Environment.Is64BitOperatingSystem)
            return false;

        bool isWow64;
        if (!IsWow64Process(process.Handle, out isWow64))
            throw new Win32Exception();
        return !isWow64;
    }
}