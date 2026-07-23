using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Shared;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace HandheldCompanion.Processors.AMD
{
    /// <summary>
    /// AMD SMU response status codes.
    /// </summary>
    public enum SmuStatus : uint
    {
        OK = 0x01,
        Failed = 0xFF,
        UnknownCmd = 0xFE,
        CmdRejectedPrereq = 0xFD,
        CmdRejectedBusy = 0xFC
    }

    /// AMD CPU codenames as returned by RyzenSMU module ioctl_get_code_name.
    /// These values must match the module version being used.
    /// </summary>
    public enum CpuCodeName : uint
    {
        Undefined = 0xFFFFFFFF, // -1 in module
        Colfax = 0,
        Renoir = 1,
        Picasso = 2,
        Matisse = 3,
        Threadripper = 4,
        CastlePeak = 5,
        RavenRidge = 6,
        RavenRidge2 = 7,
        SummitRidge = 8,
        PinnacleRidge = 9,
        Rembrandt = 10,
        Vermeer = 11,
        Vangogh = 12,
        Cezanne = 13,
        Milan = 14,
        Dali = 15,
        Raphael = 16,
        GraniteRidge = 17,
        Naples = 18,
        FireFlight = 19,
        Rome = 20,
        Chagall = 21,
        Lucienne = 22,
        Phoenix = 23,
        Phoenix2 = 24,
        Mendocino = 25,
        Genoa = 26,
        StormPeak = 27,
        DragonRange = 28,
        Mero = 29,
        HawkPoint = 30,
        StrixPoint = 31,
        StrixHalo = 32,
        KrackanPoint = 33,
        KrackanPoint2 = 34,
        Turin = 35,
        TurinD = 36,
        Bergamo = 37,
        ShimadaPeak = 38,
    }

    /// <summary>
    /// Service for AMD SMU communication via PawnIO RyzenSMU module.
    /// Provides TDP control functionality similar to RyzenAdj but without WinRing0.
    /// </summary>
    public class RyzenSmuService : IDisposable
    {
        public readonly record struct SystemPowerLimit(uint PowerLimit, uint TemperatureLimit);

        public enum CpuSubsystem
        {
            Cpu,
            Gpu,
            Soc,
            Fclk,
            Vcn,
            Lclk,
        }

        private static readonly PawnIONotInstalledNotification PawnIONotInstalledNotification = new();
        private readonly PawnIOWrapper _pawnIO;
        private bool _disposed;
        private bool _initialized;
        private CpuCodeName _cpuCodeName;
        private uint _smuVersion;

        private uint MP1_ADDR_CMD;
        private uint MP1_ADDR_RSP;
        private uint MP1_ADDR_ARGS;

        private const int SMU_RETRIES_MAX = 8096;

        /// <summary>
        /// Gets whether the service is initialized and ready.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Gets the detected CPU CpuCodeName.
        /// </summary>
        public CpuCodeName CpuCodeName => _cpuCodeName;

        /// <summary>
        /// Gets the SMU firmware version.
        /// </summary>
        public uint SmuVersion => _smuVersion;
        private SmuMailboxType? _mailboxType;

        private enum SmuMailboxType
        {
            MP1,
            PSMU
        }

        public RyzenSmuService()
        {
            _pawnIO = new PawnIOWrapper();
        }

        /// <summary>
        /// Initializes the RyzenSMU service.
        /// Connects to PawnIO driver and loads the RyzenSMU module.
        /// </summary>
        /// <param name="ryzenSmuModulePath">Path to the RyzenSMU.amx module file.</param>
        /// <returns>True if initialization successful.</returns>
        public bool Initialize(string? ryzenSmuModulePath = null)
        {
            if (_initialized)
                return true;

            try
            {
                // Connect to PawnIO driver
                if (!_pawnIO.Connect())
                {
                    if (!_pawnIO.IsInstalled() &&
                        ManagerFactory.notificationManager.Notifications.TryGetSnapshot(out Notification[] notifications, 100) &&
                        !notifications.Any(n => n is PawnIONotInstalledNotification))
                        ManagerFactory.notificationManager.Add(PawnIONotInstalledNotification);

                    LogManager.LogError("Failed to connect to PawnIO driver. Is PawnIO installed?");
                    return false;
                }

                // Get and log version
                Version? version = _pawnIO.GetVersion();
                if (version is not null)
                    LogManager.LogInformation("PawnIO driver version: {0}", version.ToString());

                // Load RyzenSMU module
                bool moduleLoaded = false;

                if (!string.IsNullOrEmpty(ryzenSmuModulePath))
                {
                    // Use explicitly provided path
                    if (_pawnIO.LoadModule(ryzenSmuModulePath))
                    {
                        moduleLoaded = true;
                    }
                    else
                    {
                        LogManager.LogError("Failed to load RyzenSMU module from specified file");
                    }
                }

                // Try embedded resource first (like ZenStates-Core)
                if (!moduleLoaded)
                {
                    const string embeddedResourceName = "HandheldCompanion.Resources.PawnIO.RyzenSMU.bin";
                    if (_pawnIO.LoadModuleFromResource(Assembly.GetExecutingAssembly(), embeddedResourceName))
                    {
                        moduleLoaded = true;
                        LogManager.LogInformation("Successfully loaded RyzenSMU module from embedded resource");
                    }
                    else
                    {
                        LogManager.LogWarning("Failed to load embedded resource, will try file paths...");
                    }
                }

                // Fallback: try to find the module in common file locations
                if (!moduleLoaded)
                {
                    string[] searchPaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RyzenSMU.bin"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PawnIO", "Modules", "RyzenSMU.bin"),
                    };

                    foreach (string path in searchPaths)
                    {
                        if (File.Exists(path))
                        {
                            LogManager.LogInformation("Found RyzenSMU module at: {0}", path);
                            if (_pawnIO.LoadModule(path))
                            {
                                moduleLoaded = true;
                                break;
                            }
                            else
                            {
                                LogManager.LogWarning("Module found but failed to load from: {0}", path);
                            }
                        }
                    }
                }

                if (!moduleLoaded)
                {
                    LogManager.LogError("RyzenSMU module could not be loaded from embedded resource or any file location");
                    return false;
                }

                // Get mutex
                PciBusMutex.Open();

                // Get CPU codename
                if (!GetCodeName(out _cpuCodeName))
                {
                    LogManager.LogWarning("Failed to get CPU codename, but continuing...");
                }
                else
                {
                    LogManager.LogInformation("Detected CPU: {0}", _cpuCodeName);
                }

                // Get CPU addresses
                GetSmuMailboxAddresses(_cpuCodeName, out MP1_ADDR_CMD, out MP1_ADDR_RSP, out MP1_ADDR_ARGS);

                // Decide whether we need mailbox-based SMU commands, and if so whether MP1 or PSMU is valid on this machine.
                if (!TrySelectWorkingMailbox(_cpuCodeName, out MP1_ADDR_CMD, out MP1_ADDR_RSP, out MP1_ADDR_ARGS, out _mailboxType))
                {
                    _mailboxType = null;
                    LogManager.LogWarning("Failed to validate any working SMU mailbox (MP1/PSMU). Will fall back to ioctl_send_smu_command.");
                }
                else if (_mailboxType.HasValue)
                {
                    LogManager.LogTrace("Using {0} mailbox. CMD={1}, RSP={2}, ARGS={3}", _mailboxType.Value, $"0x{MP1_ADDR_CMD:X}", $"0x{MP1_ADDR_RSP:X}", $"0x{MP1_ADDR_ARGS:X}");
                }

                // Get SMU version
                if (!GetSmuVersion(out _smuVersion))
                {
                    LogManager.LogWarning("Failed to get SMU version, but continuing...");
                }
                else
                {
                    LogManager.LogTrace("SMU version: {0}", $"0x{_smuVersion:X8}");
                }

                _initialized = true;
                LogManager.LogInformation("RyzenSMU service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Exception initializing RyzenSMU service: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets the CPU CpuCodeName.
        /// </summary>
        public bool GetCodeName(out CpuCodeName codeName)
        {
            codeName = CpuCodeName.Undefined;

            ulong[] output = new ulong[1];
            if (_pawnIO.ExecuteFunction("ioctl_get_code_name", null, output))
            {
                codeName = (CpuCodeName)output[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the SMU firmware version.
        /// </summary>
        public bool GetSmuVersion(out uint version)
        {
            version = 0;

            ulong[] output = new ulong[1];
            if (_pawnIO.ExecuteFunction("ioctl_get_smu_version", null, output))
            {
                version = (uint)output[0];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sends a raw SMU command.
        /// </summary>
        /// <param name="command">SMU command ID.</param>
        /// <param name="args">Up to 6 command arguments.</param>
        /// <param name="response">Response arguments (6 values).</param>
        /// <returns>SMU status code.</returns>
        public SmuStatus SendIotclCommand(uint command, uint[] args, out uint[] response)
        {
            response = new uint[6];

            if (!_initialized)
            {
                LogManager.LogError("RyzenSMU service not initialized");
                return SmuStatus.Failed;
            }

            if (!PciBusMutex.Wait(5000))
            {
                LogManager.LogError("Failed to acquire global PCI mutex (Global\\Access_PCI)");
                return SmuStatus.Failed;
            }

            try
            {
                // Input: command + 6 args = 7 values
                ulong[] input = new ulong[7];
                input[0] = command;
                for (int i = 0; i < 6 && args != null && i < args.Length; i++)
                    input[i + 1] = args[i];

                // Output: the module returns 6 response args
                ulong[] output = new ulong[6];

                if (_pawnIO.ExecuteFunction("ioctl_send_smu_command", input, output))
                {
                    for (int i = 0; i < 6; i++)
                        response[i] = (uint)output[i];

                    LogManager.LogTrace("SMU command {0} executed. Response: {1}", $"0x{command:X2}", string.Join(", ", response));

                    return SmuStatus.OK;
                }

                return SmuStatus.Failed;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Exception sending SMU command: {0}", ex.Message);
                return SmuStatus.Failed;
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        /// <summary>
        /// Gets PSMU mailbox addresses following RyzenAdj constants (PSMU_C2PMSG_*).
        /// </summary>
        public static void GetPsmuMailboxAddresses(CpuCodeName codeName, out uint cmd, out uint rsp, out uint args)
        {
            // PSMU default (set 1)
            cmd = 0x03B10A20;
            rsp = 0x03B10A80;
            args = 0x03B10A88;

            switch (codeName)
            {
                case CpuCodeName.ShimadaPeak:
                    cmd = 0x03B10924;
                    rsp = 0x03B10970;
                    args = 0x03B10A40;
                    break;

                // RyzenAdj PSMU set 2 (DragonRange  FireRange)
                case CpuCodeName.DragonRange:
                    // case CpuCodeName.FireRange:
                    cmd = 0x03B10524;
                    rsp = 0x03B10570;
                    args = 0x03B10A40;
                    break;
            }
        }

        /// <summary>
        /// Runtime detection of a working mailbox type (MP1 or PSMU) for the current CPU.
        /// Mirrors RyzenAdj's approach: choose a candidate address set by family, then validate using SMU_TEST_MSG.
        /// </summary>
        private bool TrySelectWorkingMailbox(
            CpuCodeName codeName,
            out uint cmd,
            out uint rsp,
            out uint args,
            out SmuMailboxType? selectedType)
        {
            selectedType = null;

            // 1) Prefer MP1 first on families where RyzenAdj uses MP1 for power limits (matches Phoenix behavior).
            GetSmuMailboxAddresses(codeName, out var mp1Cmd, out var mp1Rsp, out var mp1Args);
            if (TryMailbox(mp1Cmd, mp1Rsp, mp1Args))
            {
                cmd = mp1Cmd;
                rsp = mp1Rsp;
                args = mp1Args;
                selectedType = SmuMailboxType.MP1;
                return true;
            }

            // 2) Fallback to PSMU.
            GetPsmuMailboxAddresses(codeName, out var psmuCmd, out var psmuRsp, out var psmuArgs);
            if (TryMailbox(psmuCmd, psmuRsp, psmuArgs))
            {
                cmd = psmuCmd;
                rsp = psmuRsp;
                args = psmuArgs;
                selectedType = SmuMailboxType.PSMU;
                return true;
            }

            cmd = 0;
            rsp = 0;
            args = 0;
            return false;
        }

        /// <summary>
        /// Lightweight mailbox probe using SMU_TEST_MSG (0x1) similar to RyzenAdj smu_service_test().
        /// </summary>
        private bool TryMailbox(uint cmd, uint rsp, uint argsBase)
        {
            if (!ValidateMailbox(cmd, rsp, argsBase))
                return false;

            if (!PciBusMutex.Wait(5000))
                return false;

            try
            {
                if (!WaitForMailboxReadyNoLock(rsp))
                    return false;

                // Clear response
                if (!WriteSmuRegisterNoLock(rsp, 0))
                    return false;

                // Write a known marker in arg0 and verify basic R/W works.
                // RyzenAdj uses 0x47.
                if (!WriteSmuRegisterNoLock(argsBase, 0x47))
                    return false;

                if (!ReadSmuRegisterNoLock(argsBase, out var echo) || echo != 0x47)
                    return false;

                // Send SMU_TEST_MSG
                if (!WriteSmuRegisterNoLock(cmd, 0x1))
                    return false;

                if (!WaitForMailboxReadyNoLock(rsp))
                    return false;

                return ReadSmuRegisterNoLock(rsp, out var status) && status == (uint)SmuStatus.OK;
            }
            catch
            {
                return false;
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        public static void GetSmuMailboxAddresses(CpuCodeName codeName, out uint cmd, out uint rsp, out uint args)
        {
            // Default (safe-ish) values: set 2 (covers most mobile/APU parts in PawnIO)
            cmd = 0x03B10A20;
            rsp = 0x03B10A80;
            args = 0x03B10A88;

            switch (codeName)
            {
                // For Vangogh and Rembrandt (+ Mendocino / Phoenix / HawkPoint in that file)
                // MP1_C2PMSG_MESSAGE_ADDR_2  0x3B10528
                // MP1_C2PMSG_RESPONSE_ADDR_2 0x3B10578
                // MP1_C2PMSG_ARG_BASE_2      0x3B10998
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                    cmd = 0x03B10528;
                    rsp = 0x03B10578;
                    args = 0x03B10998;
                    break;

                // For Strix Point (also KrackanPoint + StrixHalo in that file)
                // MP1_C2PMSG_MESSAGE_ADDR_3  0x3B10928
                // MP1_C2PMSG_RESPONSE_ADDR_3 0x3B10978
                // MP1_C2PMSG_ARG_BASE_3      0x3B10998
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                case CpuCodeName.ShimadaPeak:
                    cmd = 0x03B10928;
                    rsp = 0x03B10978;
                    args = 0x03B10998;
                    break;

                // For DragonRange and FireRange
                // MP1_C2PMSG_MESSAGE_ADDR_4  0x3B10530
                // MP1_C2PMSG_RESPONSE_ADDR_4 0x3B1057C
                // MP1_C2PMSG_ARG_BASE_4      0x3B109C4
                case CpuCodeName.DragonRange:
                    // case CpuCodeName.FireRange:
                    cmd = 0x03B10530;
                    rsp = 0x03B1057C;
                    args = 0x03B109C4;
                    break;

                // Default MP1 layout
                // MP1_C2PMSG_MESSAGE_ADDR_1  0x3B10528
                // MP1_C2PMSG_RESPONSE_ADDR_1 0x3B10564
                // MP1_C2PMSG_ARG_BASE_1      0x3B10998
                default:
                    cmd = 0x03B10528;
                    rsp = 0x03B10564;
                    args = 0x03B10998;
                    break;
            }
        }

        /// <summary>
        /// Sends a raw SMU command via MP1 mailbox (for TDP commands on HawkPoint/Strix).
        /// Implements the SMU protocol directly using register read/write.
        /// </summary>
        public SmuStatus SendMp1Command(uint command, uint[] args, out uint[] response)
        {
            response = new uint[6];

            if (!_initialized)
            {
                LogManager.LogError("RyzenSMU service not initialized");
                return SmuStatus.Failed;
            }

            if (!ValidateMailbox(MP1_ADDR_CMD, MP1_ADDR_RSP, MP1_ADDR_ARGS))
            {
                LogManager.LogError("Invalid SMU mailbox configuration");
                return SmuStatus.Failed;
            }

            if (!PciBusMutex.Wait(5000))
            {
                LogManager.LogError("Failed to acquire global PCI mutex (Global\\Access_PCI)");
                return SmuStatus.Failed;
            }

            try
            {
                LogManager.LogTrace("Sending SMU command {0} via mailbox ({1}) (CMD={2}, RSP={3}) with arg: {4}",
                    $"0x{command:X2}",
                    _mailboxType.HasValue ? _mailboxType.Value.ToString() : "Unknown",
                    $"0x{MP1_ADDR_CMD:X}",
                    $"0x{MP1_ADDR_RSP:X}",
                    string.Join(',', args));

                if (!WaitForMailboxReadyNoLock(MP1_ADDR_RSP))
                {
                    LogManager.LogError("SMU mailbox did not become ready before command write");
                    return SmuStatus.Failed;
                }

                // Step 2: Write zero to the RSP register
                if (!WriteSmuRegisterNoLock(MP1_ADDR_RSP, 0))
                {
                    LogManager.LogError("Failed to clear MP1 RSP register");
                    return SmuStatus.Failed;
                }

                // Step 3: Write the arguments into the argument registers
                uint maxValidArgAddress = uint.MaxValue - (uint)(response.Length * 4);
                for (int i = 0; i < 6; i++)
                {
                    if (MP1_ADDR_ARGS > maxValidArgAddress)
                        continue;

                    uint argValue = (args != null && i < args.Length) ? args[i] : 0;
                    if (!WriteSmuRegisterNoLock(MP1_ADDR_ARGS + (uint)(i * 4), argValue))
                    {
                        LogManager.LogError("Failed to write MP1 arg[{0}]", i);
                        return SmuStatus.Failed;
                    }
                }

                // Step 4: Write the command to the CMD register
                if (!WriteSmuRegisterNoLock(MP1_ADDR_CMD, command))
                {
                    LogManager.LogError("Failed to write MP1 CMD register");
                    return SmuStatus.Failed;
                }

                // Step 5: Wait until the RSP register is non-zero
                if (!WaitForMailboxReadyNoLock(MP1_ADDR_RSP) || !ReadSmuRegisterNoLock(MP1_ADDR_RSP, out uint rspValue))
                {
                    LogManager.LogError("MP1 SMU timeout (RSP stayed 0 after command)");
                    return SmuStatus.Failed;
                }

                // Step 6: Check response status
                if (rspValue != 0x01) // SMU_OK
                {
                    LogManager.LogWarning("MP1 SMU returned status {0}", $"0x{rspValue:X2}");
                    return (SmuStatus)rspValue;
                }

                // Step 7: Read back the argument registers
                for (int i = 0; i < 6; i++)
                {
                    if (MP1_ADDR_ARGS > maxValidArgAddress)
                        continue;

                    if (!ReadSmuRegisterNoLock(MP1_ADDR_ARGS + (uint)(i * 4), out response[i]))
                    {
                        LogManager.LogError("Failed to read MP1 response arg[{0}]", i);
                        return SmuStatus.Failed;
                    }
                }

                LogManager.LogTrace("SMU MP1 command {0} response: [{1}]", $"0x{command:X2}", string.Join(',', response));

                return SmuStatus.OK;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Exception sending SMU MP1 command: {0}", ex.Message);
                return SmuStatus.Failed;
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        /// <summary>
        /// Sends a TDP-related SMU command, automatically using MP1 mailbox for CPUs that require it.
        /// </summary>
        private SmuStatus SendCommand(uint command, uint[] args, out uint[] response)
        {
            if (_mailboxType.HasValue)
                return SendMp1Command(command, args, out response);
            else
                return SendIotclCommand(command, args, out response);
        }

        /// <summary>
        /// Reads SMU register value.
        /// </summary>
        public bool ReadSmuRegister(uint address, out uint value)
        {
            value = 0;

            if (!PciBusMutex.Wait(5000))
                return false;

            try
            {
                return ReadSmuRegisterNoLock(address, out value);
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        /// <summary>
        /// Writes SMU register value.
        /// </summary>
        public bool WriteSmuRegister(uint address, uint value)
        {
            if (!PciBusMutex.Wait(5000))
                return false;

            try
            {
                return WriteSmuRegisterNoLock(address, value);
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        private bool ReadSmuRegisterNoLock(uint address, out uint value)
        {
            value = 0;

            ulong[] input = new ulong[] { address };
            ulong[] output = new ulong[1];

            if (_pawnIO.ExecuteFunction("ioctl_read_smu_register", input, output))
            {
                value = (uint)output[0];
                return true;
            }

            return false;
        }

        private bool WriteSmuRegisterNoLock(uint address, uint value)
        {
            ulong[] input = new ulong[] { address, value };
            return _pawnIO.ExecuteFunction("ioctl_write_smu_register", input, null);
        }

        private static bool ValidateMailbox(uint cmd, uint rsp, uint argsBase)
        {
            return cmd != 0 && rsp != 0 && argsBase != 0;
        }

        private bool WaitForMailboxReadyNoLock(uint responseAddress)
        {
            uint response = 0;
            int timeout = SMU_RETRIES_MAX;
            bool readSucceeded;

            do
                readSucceeded = ReadSmuRegisterNoLock(responseAddress, out response);
            while ((!readSucceeded || response == 0) && --timeout > 0);

            return timeout != 0 && response > 0;
        }

        #region Setters

        /// <summary>
        /// Sets all TDP limits at once (STAPM, Fast, Slow) in watts.
        /// </summary>
        /// <param name="stapmWatts">STAPM limit in watts.</param>
        /// <param name="fastWatts">Fast/SPPL limit in watts.</param>
        /// <param name="slowWatts">Slow/SPL limit in watts.</param>
        public bool SetAllLimits(int stapmWatts, int fastWatts, int slowWatts)
        {
            LogManager.LogInformation("Setting TDP limits via PawnIO: STAPM={0}W, Fast={1}W, Slow={2}W", stapmWatts, fastWatts, slowWatts);

            bool success = true;

            // Convert to milliwatts
            success &= SetStapmLimit((uint)(stapmWatts));
            success &= SetFastLimit((uint)(fastWatts));
            success &= SetSlowLimit((uint)(slowWatts));

            if (success)
            {
                LogManager.LogInformation("TDP limits set successfully");
            }
            else
            {
                LogManager.LogError("Failed to set one or more TDP limits");
            }

            return success;
        }

        public bool SetStapmLimit(uint limitW)
        {
            uint cmdId = GetSetStapmCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            return SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == limitMw;
        }

        public bool SetFastLimit(uint limitW)
        {
            uint cmdId = GetSetFastCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            return SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == limitMw;
        }

        public bool SetSlowLimit(uint limitW)
        {
            uint cmdId = GetSetSlowCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            return SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == limitMw;
        }

        public bool SetTctlTemp(uint tempC)
        {
            uint cmdId = GetSetTctlCommand();
            if (cmdId == 0)
                return false;

            return SendCommand(cmdId, new uint[] { tempC }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == tempC;
        }

        public bool SetChtcTemp(uint tempC)
        {
            uint cmdId = GetSetChtcCommand();
            if (cmdId == 0)
                return false;

            return SendCommand(cmdId, new uint[] { tempC }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == tempC;
        }

        public bool SetApuSkinTemp(uint tempC)
        {
            uint cmdId = GetSetApuSkinTempCommand();
            if (cmdId == 0)
                return false;

            tempC *= 256;
            return SendCommand(cmdId, new uint[] { tempC }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == tempC;
        }

        public bool SetPboScalar(uint scalar)
        {
            uint cmdId = GetSetPboScalarCommand();
            if (cmdId == 0)
                return false;

            uint pboEnableCommand = GetSetPboEnableCommand();
            if (pboEnableCommand != 0 && _mailboxType == SmuMailboxType.MP1)
            {
                SendMp1Command(pboEnableCommand, Array.Empty<uint>(), out _);
            }

            scalar *= 100;
            return SendCommand(cmdId, new[] { scalar }, out uint[] resp) == SmuStatus.OK;
        }

        public bool SetGpuPsmMargin(int margin)
        {
            uint cmdId = GetSetGpuPsmMarginCommand();
            if (cmdId == 0)
                return false;

            return SendCommand(cmdId, new[] { EncodePsmMargin(margin) }, out uint[] resp) == SmuStatus.OK;
        }

        public bool SetCpuSubsystemFrequencyLimit(CpuSubsystem subsystem, uint frequency, bool maximum = true)
        {
            uint cmdId = GetCpuSubsystemFrequencyCommand(subsystem, maximum);
            if (cmdId == 0)
                return false;

            return SendCommand(cmdId, new[] { frequency }, out uint[] resp) == SmuStatus.OK;
        }

        // curve offset helper
        public static uint EncodeCurveOffset(int steps) => (uint)(steps & 0xFFFFF);

        public bool SetCoAll(int value)
        {
            uint cmdId = GetSetCoAllCommand();
            if (cmdId == 0) return false;

            uint encodedValue = EncodeCurveOffset(value);
            return SendIotclCommand(cmdId, new[] { encodedValue }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == encodedValue;
        }

        public bool SetCoPer(int value)
        {
            uint cmdId = GetSetCoPerCommand();
            if (cmdId == 0) return false;

            uint encodedValue = EncodeCurveOffset(value);
            return SendIotclCommand(cmdId, new[] { encodedValue }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == encodedValue;
        }

        public bool SetCoGfx(int value)
        {
            uint cmdId = GetSetCoGfxCommand();
            if (cmdId == 0) return false;

            uint encodedValue = EncodeCurveOffset(value);
            return SendIotclCommand(cmdId, new[] { encodedValue }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == encodedValue;
        }

        public bool SetMinGfxClkFreq(uint value)
        {
            uint cmdId = GetSetMinGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == value;
        }

        public bool SetMaxGfxClkFreq(uint value)
        {
            uint cmdId = GetSetMaxGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == value;
        }

        public bool SetGfxClk(uint value)
        {
            uint cmdId = GetSetGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out uint[] response) == SmuStatus.OK && response.Any() && response[0] == value;
        }

        public static uint EncodePsmMargin(int margin)
        {
            int offset = margin < 0 ? 0x100000 : 0;
            return (uint)(offset + margin) & 0xFFFF;
        }

        #endregion Setters

        #region Getters

        public bool TryGetStapmLimit(out float stapmWatts)
        {
            stapmWatts = 0f;

            uint cmdId = GetSetStapmCommand();
            if (cmdId == 0)
                return false;

            // "Get" pattern: issue the same Get/Set command with a zero argument and read back response[0].
            // Expected unit here is mW (same as what we send for Set*Limit).
            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            uint mw = resp[0];
            if (mw == 0)
                return false;

            stapmWatts = mw / 1000.0f;
            return true;
        }

        public bool TryGetFastLimit(out float fastWatts)
        {
            fastWatts = 0f;

            uint cmdId = GetSetFastCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            uint mw = resp[0];
            if (mw == 0)
                return false;

            fastWatts = mw / 1000.0f;
            return true;
        }

        public bool TryGetSlowLimit(out float slowWatts)
        {
            slowWatts = 0f;

            uint cmdId = GetSetSlowCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            uint mw = resp[0];
            if (mw == 0)
                return false;

            slowWatts = mw / 1000.0f;
            return true;
        }

        public bool TryGetTctlTemp(out uint tempC)
        {
            tempC = 0;

            uint cmdId = GetGetTctlCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            tempC = resp[0];
            return tempC > 0;
        }

        public bool TryGetChtcTemp(out uint tempC)
        {
            tempC = 0;

            uint cmdId = GetGetChtcCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            tempC = resp[0];
            return tempC > 0;
        }

        public bool TryGetApuSkinTemp(out uint tempC)
        {
            tempC = 0;

            uint cmdId = GetSetApuSkinTempCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, new uint[] { 0 }, out uint[] resp);
            if (status != SmuStatus.OK || resp == null || resp.Length == 0)
                return false;

            tempC = resp[0];
            return tempC > 0;
        }

        public bool TryGetGpuPsmMargin(out uint margin)
        {
            margin = 0;

            uint cmdId = GetGpuPsmMarginCommand();
            if (cmdId == 0)
                return false;

            var status = SendCommand(cmdId, Array.Empty<uint>(), out uint[] response);
            if (status != SmuStatus.OK || response.Length == 0)
                return false;

            margin = response[0];
            return true;
        }

        public bool TryGetSystemPowerLimit(out SystemPowerLimit systemPowerLimit)
        {
            systemPowerLimit = default;

            uint cmdId = GetSystemConfiguredPowerLimitCommand();
            if (cmdId == 0)
                return false;

            if (SendCommand(cmdId, Array.Empty<uint>(), out uint[] response) != SmuStatus.OK || response.Length == 0)
                return false;

            uint packed = response[0];
            uint powerLimit = (packed & 0x00FF0000) >> 16;
            uint temperatureLimit = packed & 0xFF;

            if (powerLimit == 0)
                return false;

            systemPowerLimit = new SystemPowerLimit(powerLimit, temperatureLimit);
            return true;
        }

        #endregion Getters

        public bool CanSetTDP() => GetSetFastCommand() != 0;
        public bool CanSetGfxClk() => GetSetGfxClkCommand() != 0 || GetSetMinGfxClkCommand() != 0;
        public bool CanSetCoAll() => GetSetCoAllCommand() != 0;
        public bool CanSetCoPer() => GetSetCoPerCommand() != 0;
        public bool CanSetCoGfx() => GetSetCoGfxCommand() != 0;
        public bool CanSetPboScalar() => GetSetPboScalarCommand() != 0;
        public bool CanSetTctlTemp() => GetSetTctlCommand() != 0;
        public bool CanSetChtcTemp() => GetSetChtcCommand() != 0;
        public bool CanSetApuSkinTemp() => GetSetApuSkinTempCommand() != 0;
        public bool CanSetCpuSubsystemFrequency(CpuSubsystem subsystem, bool maximum = true) => GetCpuSubsystemFrequencyCommand(subsystem, maximum) != 0;

        public bool CanGetTctlTemp() => GetGetTctlCommand() != 0;
        public bool CanGetChtcTemp() => GetGetChtcCommand() != 0;

        #region Commands

        private uint GetSetStapmCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x1A;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x14;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x4F;
            }

            return 0;
        }

        private uint GetSetPboScalarCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x7C;

                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    return 0;

                case CpuCodeName.Matisse:
                case CpuCodeName.CastlePeak:
                    return 0x2F;

                case CpuCodeName.Vermeer:
                case CpuCodeName.Chagall:
                case CpuCodeName.Milan:
                    return 0x2F;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x49;

                case CpuCodeName.Vangogh:
                    return 0;

                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                    return 0x63;

                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                    return 0x63;

                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x63;

                case CpuCodeName.DragonRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x2F;

                case CpuCodeName.ShimadaPeak:
                    return 0x5B;
            }

            return 0;
        }

        private uint GetSetPboEnableCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Matisse:
                case CpuCodeName.CastlePeak:
                case CpuCodeName.Vermeer:
                case CpuCodeName.Chagall:
                case CpuCodeName.Milan:
                    return 0x33;
            }

            return 0;
        }

        private uint GetGpuPsmMarginCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                    return 0x30;

                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                    return 0x20;

                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                case CpuCodeName.DragonRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                case CpuCodeName.ShimadaPeak:
                    return 0xD7;
            }

            return 0;
        }

        private uint GetSetGpuPsmMarginCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x59;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x53;

                case CpuCodeName.Vangogh:
                    return 0;

                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                    return 0xB7;

                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                    return 0x1F;

                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x1F;

                case CpuCodeName.DragonRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0xA7;

                case CpuCodeName.ShimadaPeak:
                    return 0;
            }

            return 0;
        }

        private uint GetSystemConfiguredPowerLimitCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Vangogh:
                    return 0x54;

                case CpuCodeName.DragonRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                case CpuCodeName.ShimadaPeak:
                    return 0x23;
            }

            return 0;
        }

        private uint GetCpuSubsystemFrequencyCommand(CpuSubsystem subsystem, bool maximum)
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return subsystem switch
                    {
                        CpuSubsystem.Cpu => maximum ? 0x66u : 0x67u,
                        CpuSubsystem.Gpu => maximum ? 0x68u : 0x69u,
                        CpuSubsystem.Soc => maximum ? 0x6Au : 0x6Bu,
                        CpuSubsystem.Fclk => maximum ? 0x6Cu : 0x6Du,
                        CpuSubsystem.Vcn => maximum ? 0x6Eu : 0x6Fu,
                        CpuSubsystem.Lclk => maximum ? 0x70u : 0x71u,
                        _ => 0u,
                    };

                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0;

                case CpuCodeName.Vangogh:
                    return subsystem switch
                    {
                        CpuSubsystem.Gpu => maximum ? 0x30u : 0x31u,
                        CpuSubsystem.Soc => maximum ? 0x32u : 0x21u,
                        CpuSubsystem.Fclk => maximum ? 0x33u : 0x12u,
                        CpuSubsystem.Vcn => maximum ? 0x34u : 0x28u,
                        CpuSubsystem.Lclk => maximum ? 0x14u : 0x23u,
                        _ => 0u,
                    };
            }

            return 0;
        }

        private uint GetSetFastCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x1B;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x15;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x3E;
            }

            return 0;
        }

        private uint GetSetSlowCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x1C;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x16;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x5F;
            }

            return 0;
        }

        private uint GetSetTctlCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x1F;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x19;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x3F;
            }

            return 0;
        }

        private uint GetGetTctlCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x33;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0; // No read command for these families

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x59;
            }

            return 0;
        }

        private uint GetGetChtcCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x56;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                    return 0x37;

                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x37;

                case CpuCodeName.DragonRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x59;
            }

            return 0;
        }

        private uint GetSetChtcCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x63;
            }

            return 0;
        }

        private uint GetSetApuSkinTempCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x38;

                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                    return 0x33;

                case CpuCodeName.KrackanPoint:
                case CpuCodeName.KrackanPoint2:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                    return 0x33;
            }

            return 0;
        }

        private uint GetSetCoAllCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x55;

                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                case CpuCodeName.Mendocino:
                    return 0x4C;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x07;
            }

            return 0;
        }

        private uint GetSetCoPerCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x54;

                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                case CpuCodeName.Mendocino:
                    return 0x4B;

                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x06;
            }

            return 0;
        }

        private uint GetSetCoGfxCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                    return 0x64;

                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                    return 0xB7;
            }

            return 0;
        }

        private uint GetSetMinGfxClkCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x47;
            }

            return 0;
        }

        private uint GetSetMaxGfxClkCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                case CpuCodeName.Picasso:
                case CpuCodeName.Dali:
                    return 0x46;
            }

            return 0;
        }

        private uint GetSetGfxClkCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                case CpuCodeName.Cezanne:
                case CpuCodeName.Vangogh:
                case CpuCodeName.Rembrandt:
                case CpuCodeName.Mendocino:
                case CpuCodeName.Phoenix:
                case CpuCodeName.Phoenix2:
                case CpuCodeName.HawkPoint:
                case CpuCodeName.KrackanPoint:
                case CpuCodeName.StrixPoint:
                case CpuCodeName.StrixHalo:
                case CpuCodeName.DragonRange:
                //case CpuCodeName.FireRange:
                case CpuCodeName.Raphael:
                case CpuCodeName.GraniteRidge:
                    return 0x89;
            }

            return 0;
        }

        #endregion Commands

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
                _pawnIO?.Dispose();
                PciBusMutex.Close();
            }

            _initialized = false;
            _disposed = true;
        }

        ~RyzenSmuService()
        {
            Dispose(false);
        }
    }
}