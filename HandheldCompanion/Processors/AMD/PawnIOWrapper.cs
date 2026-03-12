using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace HandheldCompanion.Processors.AMD
{
    /// <summary>
    /// Wrapper for PawnIO driver communication using direct IOCTL calls.
    /// Based on ZenStates-Core implementation.
    /// </summary>
    public class PawnIOWrapper : IDisposable
    {
        private const int FN_NAME_LENGTH = 32;
        private Version VERSION_LAST = new(2, 1, 0, 0);

        // IOCTL codes based on ZenStates-Core
        private const uint DEVICE_TYPE = 41394u << 16;  // 0xA1B20000
        private const uint IOCTL_PIO_LOAD_BINARY = 0x821 << 2;  // 0x2084
        private const uint IOCTL_PIO_EXECUTE_FN = 0x841 << 2;   // 0x2104

        private enum ControlCode : uint
        {
            LoadBinary = DEVICE_TYPE | IOCTL_PIO_LOAD_BINARY,  // 0xA1B22084
            Execute = DEVICE_TYPE | IOCTL_PIO_EXECUTE_FN       // 0xA1B22104
        }

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        // Use IntPtr version like ZenStates-Core
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // DeviceIoControl with IntPtr handle (for initial load)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            ControlCode dwIoControlCode,
            byte[] lpInBuffer,
            uint nInBufferSize,
            byte[] lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        // DeviceIoControl with SafeFileHandle (for execute after successful load)
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            ControlCode ioControlCode,
            [In] byte[] inBuffer,
            uint inBufferSize,
            [Out] byte[] outBuffer,
            uint nOutBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        private IntPtr _rawHandle = IntPtr.Zero;
        private SafeFileHandle _safeHandle;
        private bool _disposed;
        private bool _moduleLoaded;

        /// <summary>
        /// Gets whether we're connected to PawnIO.
        /// </summary>
        public bool IsConnected => _rawHandle != IntPtr.Zero && _rawHandle.ToInt64() != -1;

        /// <summary>
        /// Gets whether a module is loaded.
        /// </summary>
        public bool IsModuleLoaded => _moduleLoaded && _safeHandle != null && !_safeHandle.IsInvalid;

        /// <summary>
        /// Connects to the PawnIO driver.
        /// </summary>
        /// <returns>True if connection successful.</returns>
        public bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                Version? version = GetVersion();
                if (version < VERSION_LAST)
                {
                    // PawnIO pre 2.1.0.0
                    _rawHandle = CreateFile(
                        @"\\.\PawnIO",
                        GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);
                }
                else
                {
                    // PawnIO post 2.1.0.0
                    _rawHandle = CreateFile(
                        @"\\?\GLOBALROOT\Device\PawnIO",
                        GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);
                }

                if (_rawHandle == IntPtr.Zero || _rawHandle.ToInt64() == -1)
                {
                    int error = Marshal.GetLastWin32Error();
                    LogManager.LogError($"Failed to open PawnIO device. Error code: {error}");
                    _rawHandle = IntPtr.Zero;
                    return false;
                }

                LogManager.LogInformation("Connected to PawnIO driver");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Exception connecting to PawnIO: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the PawnIO installed version from registry (same approach as the InnoSetup script).
        /// </summary>
        public Version? GetVersion()
        {
            // PawnIO.Setup registers itself under Windows "Uninstall" entries.
            // We resolve DisplayVersion from:
            //  - HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\* 
            //  - HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*  (32-bit view)
            // and match DisplayName containing "PawnIO".

            if (TryGetInstalledPawnIOVersion(out string versionString))
                return new Version(versionString);

            return new Version();
        }

        private static bool TryGetInstalledPawnIOVersion(out string versionString)
        {
            versionString = null;

            // Prefer direct 64-bit uninstall hive first.
            if (TryGetFromUninstallHive(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", out versionString))
                return true;

            // Fallback to WOW6432Node if PawnIO was registered in 32-bit view.
            if (TryGetFromUninstallHive(@"SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall", out versionString))
                return true;

            return false;
        }

        private static bool TryGetFromUninstallHive(string hivePath, out string versionString)
        {
            versionString = null;

            try
            {
                using RegistryKey root = Registry.LocalMachine.OpenSubKey(hivePath);
                if (root is null)
                    return false;

                foreach (string subKeyName in root.GetSubKeyNames())
                {
                    // Use our shared registry helpers (mirrors InnoSetup approach of reading uninstall entries).
                    string fullKeyPath = hivePath + "\\" + subKeyName;
                    string displayName = RegistryUtils.GetString(fullKeyPath, "DisplayName");
                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    // Match like Inno: compare against product name.
                    // We keep it resilient to minor naming variations.
                    if (!displayName.Contains("PawnIO", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    string displayVersion = RegistryUtils.GetString(fullKeyPath, "DisplayVersion");
                    if (string.IsNullOrWhiteSpace(displayVersion))
                        continue;

                    versionString = displayVersion.Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to read PawnIO version from registry ({hivePath}): {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Loads a PawnIO module from an embedded resource.
        /// This is the preferred method, matching ZenStates-Core behavior.
        /// </summary>
        /// <param name="assembly">Assembly containing the resource.</param>
        /// <param name="resourceName">Full resource name (e.g., "XboxGamingBarHelper.Resources.PawnIO.RyzenSMU.bin").</param>
        /// <returns>True if module loaded successfully.</returns>
        public bool LoadModuleFromResource(Assembly assembly, string resourceName)
        {
            if (!IsConnected)
            {
                LogManager.LogError("Cannot load module: not connected to PawnIO");
                return false;
            }

            try
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        LogManager.LogError($"Embedded resource not found: {resourceName}");
                        // List available resources for debugging
                        var available = assembly.GetManifestResourceNames();
                        LogManager.LogInformation($"Available resources ({available.Length}): {string.Join(", ", available)}");
                        return false;
                    }

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        byte[] moduleData = memoryStream.ToArray();
                        LogManager.LogInformation($"Loaded embedded resource {resourceName} ({moduleData.Length} bytes)");
                        return LoadModuleFromBytes(moduleData, $"embedded:{resourceName}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Exception loading module from resource: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a PawnIO module from a file.
        /// </summary>
        /// <param name="modulePath">Path to the .bin module file.</param>
        /// <returns>True if module loaded successfully.</returns>
        public bool LoadModule(string modulePath)
        {
            if (!IsConnected)
            {
                LogManager.LogError("Cannot load module: not connected to PawnIO");
                return false;
            }

            try
            {
                if (!File.Exists(modulePath))
                {
                    LogManager.LogError($"Module file not found: {modulePath}");
                    return false;
                }

                byte[] moduleData = File.ReadAllBytes(modulePath);
                return LoadModuleFromBytes(moduleData, modulePath);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Exception loading module: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a PawnIO module from byte array.
        /// </summary>
        /// <param name="moduleData">The compiled module data.</param>
        /// <param name="sourceName">Optional name for logging.</param>
        /// <returns>True if module loaded successfully.</returns>
        public bool LoadModuleFromBytes(byte[] moduleData, string sourceName = "bytes")
        {
            if (!IsConnected)
            {
                LogManager.LogError("Cannot load module: not connected to PawnIO");
                return false;
            }

            try
            {
                LogManager.LogInformation($"Loading PawnIO module from {sourceName} ({moduleData.Length} bytes)");

                // Use IntPtr handle for loading (like ZenStates-Core)
                bool result = DeviceIoControl(
                    _rawHandle,
                    ControlCode.LoadBinary,
                    moduleData,
                    (uint)moduleData.Length,
                    null,
                    0,
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (result)
                {
                    // Wrap in SafeFileHandle for future execute calls
                    _safeHandle = new SafeFileHandle(_rawHandle, true);
                    _rawHandle = IntPtr.Zero; // SafeFileHandle now owns this
                    _moduleLoaded = true;
                    LogManager.LogInformation("Module loaded successfully");
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    int hr = Marshal.GetHRForLastWin32Error();
                    LogManager.LogError($"Failed to load module. Win32 error: {error}, HRESULT: 0x{hr:X8}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Exception loading module: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a function from the loaded module.
        /// </summary>
        /// <param name="functionName">Name of the function to execute (max 31 chars).</param>
        /// <param name="inputArgs">Input arguments (array of UInt64).</param>
        /// <param name="outputArgs">Output buffer for results (array of UInt64).</param>
        /// <returns>True if execution succeeded.</returns>
        public bool ExecuteFunction(string functionName, ulong[] inputArgs, ulong[] outputArgs)
        {
            if (!IsModuleLoaded)
            {
                LogManager.LogError("Cannot execute function {0}: no module loaded", functionName);
                return false;
            }

            try
            {
                int inSize = inputArgs?.Length ?? 0;
                int outSize = outputArgs?.Length ?? 0;

                // Build input buffer: 32-byte function name + input args as bytes
                byte[] nameBytes = Encoding.ASCII.GetBytes(functionName);
                int nameLen = Math.Min(nameBytes.Length, FN_NAME_LENGTH - 1);

                byte[] totalInput = new byte[(inSize * 8) + FN_NAME_LENGTH];
                Buffer.BlockCopy(nameBytes, 0, totalInput, 0, nameLen);

                if (inputArgs != null && inSize > 0)
                {
                    byte[] inBuffer = new byte[inSize * 8];
                    Buffer.BlockCopy(inputArgs, 0, inBuffer, 0, inSize * 8);
                    Buffer.BlockCopy(inBuffer, 0, totalInput, FN_NAME_LENGTH, inSize * 8);
                }

                byte[] outBuffer = outSize > 0 ? new byte[outSize * 8] : null;

                // Use SafeFileHandle for execute calls
                bool result = DeviceIoControl(
                    _safeHandle,
                    ControlCode.Execute,
                    totalInput,
                    (uint)totalInput.Length,
                    outBuffer,
                    (uint)(outBuffer?.Length ?? 0),
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (result)
                {
                    if (outputArgs != null && outBuffer != null && bytesReturned > 0)
                    {
                        int elementsReturned = (int)Math.Min(bytesReturned / 8, (uint)outSize);
                        Buffer.BlockCopy(outBuffer, 0, outputArgs, 0, elementsReturned * 8);
                    }
                    LogManager.LogTrace("Executed {0}: in={1}, out={2}", functionName, inSize, bytesReturned / 8);
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    LogManager.LogError("pawnio_execute({0}) failed. Win32 error: {1}", functionName, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Exception executing function {0}: {1}", functionName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Disconnects from PawnIO driver.
        /// </summary>
        public void Disconnect()
        {
            _moduleLoaded = false;

            if (_safeHandle != null && !_safeHandle.IsInvalid)
            {
                try
                {
                    _safeHandle.Close();
                    LogManager.LogInformation("Disconnected from PawnIO driver");
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Error closing PawnIO safe handle: {ex.Message}");
                }
                _safeHandle = null;
            }

            if (_rawHandle != IntPtr.Zero && _rawHandle.ToInt64() != -1)
            {
                try
                {
                    CloseHandle(_rawHandle);
                    LogManager.LogInformation("Closed PawnIO raw handle");
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Error closing PawnIO raw handle: {ex.Message}");
                }
                _rawHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }

        ~PawnIOWrapper()
        {
            Dispose(false);
        }
    }
}
