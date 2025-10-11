using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Automation;
using System.Windows.Media;

namespace HandheldCompanion.Misc;

public class ProcessEx : IDisposable, ICloneable
{
    #region filters
    public enum ProcessFilter
    {
        Allowed = 0,
        Restricted = 1,
        HandheldCompanion = 2,
        Desktop = 3
    }

    private static readonly string[] launcherExecutables = new[]
    {
        // Epic Games Launcher
        "EpicGamesLauncher.exe",
        "EpicWebHelper.exe",

        // Blizzard (Battle.net)
        "agent.exe", "BlizzardError.exe", "Battle.net.exe",

        // Ubisoft Connect
        "uplay_bootstrapper.exe", "UbisoftConnect.exe", "UplayService.exe",
        "UbisoftGameLaun", "upc.exe", "UplayWebCore.exe",

        // EA App (formerly Origin)
        "EALink.exe", "EADesktop.exe", "OriginWebHelperService.exe", "EALaunchHelper.exe",
        "EALauncher.exe", "EABackgroundService.exe", "EAConnect_microsoft.exe",
        "EACrashReporter.exe", "EAGEP.exe", "EAStreamProxy.exe", "EAUninstall.exe",
        "ErrorReporter.exe", "GetGameToken32.exe", "GetGameToken64.exe",
        "IGOProxy32.exe", "Link2EA.exe", "OriginLegacyCompatibility.exe",

        // GOG Galaxy
        "GalaxyClient.exe", "GalaxyClientService.exe",

        // Steam
        "steam.exe", "steamwebhelper.exe", "SteamService.exe",

        // Steam on Wine
        "Steam.exe",

        // Xbox Game Pass / Microsoft Store
        "XboxAppServices.exe", "MicrosoftGamingServices.exe",

        // Rockstar Games Launcher
        "RockstarService.exe",

        // Bethesda.net Launcher
        "BethesdaNetLauncher.exe",

        // Amazon Games
        "AGSGameLaunchHelper.exe",

        // Itch.io App
        "itch-setup.exe", "butler.exe",

        // VK Play GameCenter
        "GameCenter.exe",

        // Unified Games Launcher
        "com.github.tkashkin.gamehub",

        // Integrated Web Engine
        "QtWebEngineProcess.exe"
    };

    private static readonly string[] inputModules = new[]
    {
        // Input Libraries
        "xinput1_1.dll", "xinput1_2.dll", "xinput1_3.dll", "xinput1_4.dll", "xinput9_1_0.dll",
        "dinput.dll", "dinput8.dll", "GameInput.dll", "SDL2.DLL"
    };

    private static readonly string[] renderModules = new[]
    {
        // DirectX
        "d3d9.dll", "d3d11.dll", "d3d12.dll", // Direct3D
        "dxgi.dll", // DirectX Graphics Infrastructure
        "d3dcompiler_43.dll", "d3dcompiler_47.dll", // Direct3D Shader Compiler

        // OpenGL
        "opengl32.dll", // Core OpenGL functionality
        "glu32.dll", // OpenGL Utility Library

        // Vulkan
        "vulkan-1.dll", // Vulkan API loader
        "vulkan-1-999-0-0-0.dll" // Vulkan API loader
    };

    private static readonly string[] gameModules = new[]
    {    
        // Unreal Engine DLLs
        "UE4Editor-Core.dll", "UE4Editor-CoreUObject.dll", "UE4Editor-Engine.dll",
        "UE4Editor-Renderer.dll", "UE4Editor-RHI.dll", "UE4Editor-PhysicsCore.dll",
        "PhysX3_x64.dll", "UE4Editor-AudioMixer.dll", "UE4Editor-OnlineSubsystem.dll",
        "UE4Editor-ScriptPlugin.dll", "UE4Editor-BlueprintGraph.dll",

        // Unity Engine DLLs
        "UnityEngine.CoreModule.dll", "UnityEngine.dll", "UnityEditor.dll",
        "UnityEngine.Rendering.dll", "UnityEngine.Shaders.dll", "UnityEngine.PhysicsModule.dll",
        "UnityEngine.AudioModule.dll", "UnityEngine.Scripting.dll",

        // CryEngine DLLs
        "CryEngine.Core.dll", "CryEngine.Common.dll", "CryEngine.Render.dll",
        "CryRenderD3D11.dll", "CryRenderD3D12.dll", "CryRenderVulkan.dll", "CryPhysics.dll",
        "CryAudio.dll",

        // Godot Engine DLLs
        "godot.windows.tools.64.dll", "godot.windows.opt.64.dll",

        // Source Engine DLLs
        "engine.dll", "tier0.dll", "vstdlib.dll", "vphysics.dll",
        "shaderapidx9.dll", "shaderapivulkan.dll", "materialsystem.dll",
        "steam_api.dll", "steamclient.dll",

        // Frostbite Engine DLLs
        "FrostbiteCore.dll", "FrostbiteRuntime.dll", "FrostbiteRender.dll",
        "FrostbiteAudio.dll", "FrostbitePhysics.dll",

        // Lumberyard (Amazon) DLLs
        "CryEngine.Core.dll", "LumberyardLauncher.dll",
        "CryRenderD3D11.dll", "CryRenderVulkan.dll",

        // GameMaker Studio DLLs
        "GameMakerCore.dll", "Runner.dll",

        // BattleEye (BE)
        "BEClient_x64.dll", "BEClient_x86.dll", "BEService_x64.dll", "BEService_x86.dll",

        // Easy Anti-Cheat (EAC)
        "EasyAntiCheat_x64.dll", "EasyAntiCheat_x86.dll", "EasyAntiCheat_EOS.dll",

        // Riot Vanguard (Valorant)
        "vanguard.dll", "vgk.sys",

        // PunkBuster
        "pbcl.dll", "pbsv.dll", "pbag.dll",

        // Valve Anti-Cheat (VAC)
        "steam_api.dll", "steamclient.dll",

        // Xigncode3
        "x3.xem", "x3_x64.xem",

        // Hyperion (Activision/Call of Duty)
        "hyperion.dll",

        // Denuvo Anti-Tamper/Anti-Cheat
        "denuvo64.dll", "denuvo32.dll", 

        // FACEIT Anti-Cheat
        "faceitclient.dll", "faceitac.sys",

        // GameGuard (nProtect)
        "GameGuard.des", "npggNT.des", "ggerror.des",

        // Steam
        "steamclient.dll", "steam_api.dll", "steam_api64.dll",
        "tier0_s.dll", "vstdlib_s.dll",

        // Epic Games Launcher
        "EpicGamesLauncher.exe", "EOSSDK-Win64-Shipping.dll",
        "EOSSDK-Win32-Shipping.dll",

        // Battle.net (Blizzard)
        "Battle.net.dll", "Battle.net.runtime.dll", "Battle.net.runtime_x64.dll",
        "agent.exe", "BlizzardError.exe",

        // Ubisoft Connect (formerly Uplay)
        "uplay_r1.dll", "uplay_r1_loader.dll", "uplay_r2_loader.dll",
        "uplay_overlay.dll", "uplay_bootstrapper.exe",

        // EA App (formerly Origin)
        "EALink.exe", "EACore.dll", "EADesktop.exe",
        "EABootstrap.dll", "OriginWebHelperService.exe",

        // GOG Galaxy
        "Galaxy.dll", "Galaxy64.dll", "GalaxyClient.exe",
        "GalaxyCommunication.dll",

        // Xbox Game Pass / Microsoft Store
        "GamingServices.dll", "XboxAppServices.exe",
        "xbox_game_bar.dll", "MicrosoftGamingServices.exe",

        // Rockstar Games Launcher
        "RockstarService.exe", "launcher.dll", "SocialClubHelper.exe",
        "SocialClubV2.dll", "SocialClubV2_64.dll",

        // Bethesda.net Launcher
        "BethesdaNetLauncher.exe", "BethesdaNetHelper.dll",

        // Amazon Games
        "AGSGameLaunchHelper.exe", "AmazonGamesLauncher.dll",

        // Itch.io App
        "itch.dll", "butler.exe", "itch-setup.exe",
    };
    #endregion

    public const string AppCompatRegistry = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    public const string RunAsAdminRegistryValue = "RUNASADMIN";
    public const string DisabledMaximizedWindowedValue = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
    public const string HighDPIAwareValue = "HIGHDPIAWARE";

    public Process Process { get; private set; }
    public int ProcessId => Process?.Id ?? 0;
    public nint Handle => Process?.Handle ?? IntPtr.Zero;

    public ProcessFilter Filter { get; set; }
    public GamePlatform Platform { get; set; }
    public ImageSource ProcessIcon { get; private set; }

    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> AppProperties = new();

    public ConcurrentDictionary<int, ProcessWindow> ProcessWindows { get; private set; } = new();

    public ProcessThread? MainThread { get; set; }

    private static object registryLock = new();

    private bool _disposed = false; // Prevent multiple disposals
    private bool IsDisposing = false;

    #region event
    public EventHandler Refreshed;

    public event WindowAttachedEventHandler WindowAttached;
    public delegate void WindowAttachedEventHandler(ProcessWindow processWindow);

    public event WindowDetachedEventHandler WindowDetached;
    public delegate void WindowDetachedEventHandler(ProcessWindow processWindow);
    #endregion

    public ProcessEx(Process process, string path, string executable, ProcessFilter filter)
    {
        Process = process;
        Path = path;
        Executable = executable;
        Filter = filter;

        Refresh(true);
        GetMainThread();

        // update main thread when disposed
        if (MainThread is null)
            throw new Exception($"Process {ProcessId} has exited or MainThread is unreachable during creation");

        // get executable icon
        if (File.Exists(Path))
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Path);
            ProcessIcon = icon?.ToImageSource();

            // get details
            AppProperties = ProcessUtils.GetAppProperties(path);
        }
    }

    ~ProcessEx()
    {
        Dispose(false);
    }

    public bool IsGame()
    {
        if (IsDisposing)
            return false;

        switch (Filter)
        {
            case ProcessFilter.Allowed:
                break;
            default:
                return false;
        }

        // avoid launchers
        if (launcherExecutables.Contains(Executable, StringComparer.InvariantCultureIgnoreCase))
            return false;

        // some gaming platforms won't let us read modules, try the certificate instead
        if (IsGamesSigned(Path))
        {
            return true;
        }
        else
        {
            bool hasInput = false;
            bool hasRender = false;

            try
            {
                // Loop through the modules of the process
                foreach (ProcessModule module in Process.Modules)
                {
                    try
                    {
                        // Get the name of the module
                        string moduleName = module.ModuleName;

                        if (gameModules.Contains(moduleName, StringComparer.InvariantCultureIgnoreCase))
                        {
                            return true;
                        }
                        else
                        {
                            if (inputModules.Contains(moduleName, StringComparer.InvariantCultureIgnoreCase))
                                hasInput = true;
                            else if (renderModules.Contains(moduleName, StringComparer.InvariantCultureIgnoreCase))
                                hasRender = true;

                            // If both conditions are met, we can exit early.
                            if (hasInput && hasRender)
                                return true;
                        }
                    }
                    catch (Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            catch { }
        }

        return false;
    }

    private static readonly string[] certificateRelatedModules = new[]
    {
        "Epic Games", "Ubisoft", "Blizzard", "Valve Corp", "Electronic Arts", "GOG  sp"
    };

    public bool IsGamesSigned(string executablePath)
    {
        try
        {
            // Load the signed file and extract the signer information
            X509Certificate cert = X509Certificate.CreateFromSignedFile(executablePath);

            // Get the signing certificate
            X509Certificate2 cert2 = new X509Certificate2(cert);

            foreach (string certificate in certificateRelatedModules)
                if (cert2.Subject.Contains(certificate, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        catch (CryptographicException) { }
        catch (Exception) { }

        return false;
    }

    public bool IsDesktop()
    {
        return !IsGame();
    }

    public void AttachWindow(AutomationElement automationElement, bool primary = false)
    {
        int hwnd = automationElement.Current.NativeWindowHandle;
        if (!ProcessWindows.TryGetValue(hwnd, out var window))
        {
            // create new window object
            window = new(this, automationElement, primary);
            window.Closed += Window_Closed;

            if (string.IsNullOrEmpty(window.Name))
                return;

            // add window
            ProcessWindows.TryAdd(hwnd, window);

            // raise event
            WindowAttached?.Invoke(window);
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        // get object
        ProcessWindow processWindow = (ProcessWindow)sender;

        // raise event
        if (ProcessWindows.TryRemove(processWindow.Hwnd, out _))
        {
            WindowDetached?.Invoke(processWindow);

            processWindow.Closed -= Window_Closed;
            processWindow.Dispose();
        }
    }

    public bool FullScreenOptimization
    {
        get
        {
            string valueStr = GetAppCompatFlags(Path);
            return !string.IsNullOrEmpty(valueStr)
                && valueStr.Split(' ').Any(s => s == DisabledMaximizedWindowedValue);
        }
    }

    public bool HighDPIAware
    {
        get
        {
            string valueStr = GetAppCompatFlags(Path);
            return !string.IsNullOrEmpty(valueStr)
                && valueStr.Split(' ').Any(s => s == HighDPIAwareValue);
        }
    }

    public string Executable { get; set; }

    public bool IsSuspended
    {
        get
        {
            GetMainThread();

            if (MainThread?.ThreadState == ThreadState.Wait && MainThread?.WaitReason == ThreadWaitReason.Suspended)
                return true;
            return false;
        }
        set
        {
            if (value != IsSuspended)
            {
                if (value)
                    ProcessManager.SuspendProcess(this).Wait();
                else
                    ProcessManager.ResumeProcess(this).Wait();
            }
        }
    }

    public void Kill()
    {
        try
        {
            if (Process.HasExited)
                return;

            Process.Kill();
        }
        catch { }
    }

    public void Refresh(bool force)
    {
        try
        {
            if (Process is null || Process.HasExited)
                return;

            if (ProcessWindows.IsEmpty && !force)
                return;

            Process.Refresh();

            if (MainThread is null)
                return;

            switch (MainThread.ThreadState)
            {
                case ThreadState.Terminated:
                    {
                        // dispose from MainThread
                        MainThread.Dispose();
                        MainThread = null;
                    }
                    break;
            }

            // refresh attached window names
            foreach (ProcessWindow processWindow in ProcessWindows.Values)
                processWindow.RefreshName();

            // raise event
            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException) { } // No process is associated with this object
        catch (NullReferenceException) { }
    }

    private void SetMainThread(ProcessThread mainThread)
    {
        // Unsubscribe from the previous MainThread's Disposed event
        if (MainThread != null)
        {
            MainThread.Disposed -= MainThread_Disposed;
            MainThread.Dispose();
            MainThread = null;
        }

        // Update the previous MainThread reference
        MainThread = mainThread;
        MainThread.Disposed += MainThread_Disposed;
    }

    private void MainThread_Disposed(object? sender, EventArgs e)
    {
        if (IsDisposing)
            return;

        // Update MainThread when disposed
        GetMainThread();
    }

    public static string GetAppCompatFlags(string Path)
    {
        if (string.IsNullOrEmpty(Path))
            return string.Empty;

        lock (registryLock)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AppCompatRegistry))
                {
                    string valueStr = (string)key?.GetValue(Path);
                    return valueStr;
                }
            }
            catch { }
        }

        return string.Empty;
    }

    public static void SetAppCompatFlag(string Path, string Flag, bool value)
    {
        if (string.IsNullOrEmpty(Path))
            return;

        lock (registryLock)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(AppCompatRegistry, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        List<string> values = ["~"];
                        string valueStr = (string)key.GetValue(Path);

                        if (!string.IsNullOrEmpty(valueStr))
                            values = valueStr.Split(' ').ToList();

                        values.Remove(Flag);

                        if (value)
                            values.Add(Flag);

                        if (values.Count == 1 && values[0] == "~" && !string.IsNullOrEmpty(valueStr))
                            key.DeleteValue(Path);
                        else
                            key.SetValue(Path, string.Join(" ", values), RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }
    }

    private void GetMainThread()
    {
        ProcessThread mainThread = null;
        DateTime startTime = DateTime.MaxValue;

        try
        {
            if (Process.HasExited || Process.Threads is null || Process.Threads.Count == 0)
                return;

            foreach (ProcessThread thread in Process.Threads)
            {
                try
                {
                    if (thread.ThreadState == ThreadState.Terminated)
                        continue;

                    if (thread.StartTime < startTime)
                    {
                        startTime = thread.StartTime;
                        mainThread = thread;
                    }
                }
                catch { }
            }

            if (mainThread is null)
                mainThread = Process.Threads[0];

            if (mainThread is null)
                return;

            SetMainThread(mainThread);
        }
        catch { }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // set flag
            IsDisposing = true;

            // Free managed resources
            Process?.Dispose();
            Process = null;

            // Unsubscribe from MainThread's event
            if (MainThread != null)
            {
                MainThread.Disposed -= MainThread_Disposed;
                MainThread.Dispose();
                MainThread = null;
            }

            // Dispose of all windows
            foreach (ProcessWindow window in ProcessWindows.Values)
                window.Dispose();
            ProcessWindows.Clear();
        }

        _disposed = true;
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }
}