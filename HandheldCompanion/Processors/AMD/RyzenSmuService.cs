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
        public bool Initialize(string ryzenSmuModulePath = null)
        {
            if (_initialized)
                return true;

            try
            {
                LogManager.LogInformation("Initializing RyzenSMU service via PawnIO...");

                // Connect to PawnIO driver
                if (!_pawnIO.Connect())
                {
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
                    LogManager.LogInformation("Attempting to load RyzenSMU module from embedded resource...");
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

                    LogManager.LogInformation("Searching for RyzenSMU module in {0} file locations...", searchPaths.Length);
                    foreach (var path in searchPaths)
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
                    LogManager.LogInformation("Using {0} mailbox. CMD={1}, RSP={2}, ARGS={3}", _mailboxType.Value, $"0x{MP1_ADDR_CMD:X}", $"0x{MP1_ADDR_RSP:X}", $"0x{MP1_ADDR_ARGS:X}");
                }

                // Get SMU version
                if (!GetSmuVersion(out _smuVersion))
                {
                    LogManager.LogWarning("Failed to get SMU version, but continuing...");
                }
                else
                {
                    LogManager.LogInformation("SMU version: {0}", $"0x{_smuVersion:X8}");
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

                    LogManager.LogDebug("SMU command {0} executed. Response: {1}", $"0x{command:X2}", string.Join(", ", response));

                    return SmuStatus.OK;
                }

                LogManager.LogError("Failed to execute SMU command {0}", $"0x{command:X2}");
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
            try
            {
                // Clear response
                if (!WriteSmuRegister(rsp, 0))
                    return false;

                // Write a known marker in arg0 and verify basic R/W works.
                // RyzenAdj uses 0x47.
                if (!WriteSmuRegister(argsBase, 0x47))
                    return false;

                if (!ReadSmuRegister(argsBase, out var echo) || echo != 0x47)
                    return false;

                // Send SMU_TEST_MSG
                if (!WriteSmuRegister(cmd, 0x1))
                    return false;

                // Wait for response to become non-zero and indicate OK.
                for (int i = 0; i < SMU_RETRIES_MAX; i++)
                {
                    if (!ReadSmuRegister(rsp, out var r))
                        return false;

                    if (r == 0)
                        continue;

                    return r == (uint)SmuStatus.OK;
                }

                return false;
            }
            catch
            {
                return false;
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

            try
            {
                LogManager.LogDebug("Sending SMU command {0} via mailbox ({1}) (CMD={2}, RSP={3}) with arg: {4}",
                    $"0x{command:X2}",
                    _mailboxType.HasValue ? _mailboxType.Value.ToString() : "Unknown",
                    $"0x{MP1_ADDR_CMD:X}",
                    $"0x{MP1_ADDR_RSP:X}",
                    string.Join(',', args));

                // Step 1: Check if RSP register is non-zero (SMU ready)
                // Some CPUs start with RSP=0, so we don't fail if it's 0 initially
                uint rspValue = 0;
                if (ReadSmuRegister(MP1_ADDR_RSP, out rspValue))
                {
                    LogManager.LogDebug("Initial MP1 RSP value: {0}", $"0x{rspValue:X8}");
                }
                else
                {
                    LogManager.LogWarning("Failed to read initial MP1 RSP register, continuing anyway...");
                }

                // Step 2: Write zero to the RSP register
                if (!WriteSmuRegister(MP1_ADDR_RSP, 0))
                {
                    LogManager.LogError("Failed to clear MP1 RSP register");
                    return SmuStatus.Failed;
                }

                // Step 3: Write the arguments into the argument registers
                for (int i = 0; i < 6; i++)
                {
                    uint argValue = (args != null && i < args.Length) ? args[i] : 0;
                    if (!WriteSmuRegister(MP1_ADDR_ARGS + (uint)(i * 4), argValue))
                    {
                        LogManager.LogError("Failed to write MP1 arg[{0}]", i);
                        return SmuStatus.Failed;
                    }
                }

                // Step 4: Write the command to the CMD register
                if (!WriteSmuRegister(MP1_ADDR_CMD, command))
                {
                    LogManager.LogError("Failed to write MP1 CMD register");
                    return SmuStatus.Failed;
                }

                // Step 5: Wait until the RSP register is non-zero
                rspValue = 0;
                for (int i = 0; i < SMU_RETRIES_MAX; i++)
                {
                    if (!ReadSmuRegister(MP1_ADDR_RSP, out rspValue))
                    {
                        LogManager.LogError("Failed to read MP1 RSP register (waiting for response)");
                        return SmuStatus.Failed;
                    }
                    if (rspValue != 0)
                        break;
                }
                if (rspValue == 0)
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
                    if (!ReadSmuRegister(MP1_ADDR_ARGS + (uint)(i * 4), out response[i]))
                    {
                        LogManager.LogError("Failed to read MP1 response arg[{0}]", i);
                        return SmuStatus.Failed;
                    }
                }

                LogManager.LogDebug("SMU MP1 command {0} response: [{1}]", $"0x{command:X2}", string.Join(',', response));

                return SmuStatus.OK;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Exception sending SMU MP1 command: {0}", ex.Message);
                return SmuStatus.Failed;
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
                ulong[] input = new ulong[] { address };
                ulong[] output = new ulong[1];

                if (_pawnIO.ExecuteFunction("ioctl_read_smu_register", input, output))
                {
                    value = (uint)output[0];
                    return true;
                }

                return false;
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
                ulong[] input = new ulong[] { address, value };
                return _pawnIO.ExecuteFunction("ioctl_write_smu_register", input, null);
            }
            finally
            {
                PciBusMutex.Release();
            }
        }

        public bool SetStapmLimit(uint limitW)
        {
            uint cmdId = GetSetStapmCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            if (SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any())
                return (response[0] == limitMw);

            return false;
        }

        public bool SetFastLimit(uint limitW)
        {
            uint cmdId = GetSetFastCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            if (SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any())
                return (response[0] == limitMw);

            return false;
        }

        public bool SetSlowLimit(uint limitW)
        {
            uint cmdId = GetSetSlowCommand();
            if (cmdId == 0) return false;

            // expected value is mW
            uint limitMw = limitW * 1000;

            if (SendCommand(cmdId, new uint[] { limitMw }, out uint[] response) == SmuStatus.OK && response.Any())
                return (response[0] == limitMw);

            return false;
        }

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

        public static uint EncodeCurveOffset(int steps) => (uint)(steps & 0xFFFFF);

        public bool SetCoAll(int value)
        {
            uint cmdId = GetSetCoAllCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { EncodeCurveOffset(value) }, out _) == SmuStatus.OK;
        }

        public bool SetCoPer(int value)
        {
            uint cmdId = GetSetCoPerCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { EncodeCurveOffset(value) }, out _) == SmuStatus.OK;
        }

        public bool SetCoGfx(int value)
        {
            uint cmdId = GetSetCoGfxCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { EncodeCurveOffset(value) }, out _) == SmuStatus.OK;
        }

        public bool SetMinGfxClkFreq(uint value)
        {
            uint cmdId = GetSetMinGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out _) == SmuStatus.OK;
        }

        public bool SetMaxGfxClkFreq(uint value)
        {
            uint cmdId = GetSetMaxGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out _) == SmuStatus.OK;
        }

        public bool SetGfxClk(uint value)
        {
            uint cmdId = GetSetGfxClkCommand();
            if (cmdId == 0) return false;

            return SendIotclCommand(cmdId, new[] { value }, out _) == SmuStatus.OK;
        }

        public bool CanSetTDP() => GetSetFastCommand() != 0;
        public bool CanSetGfxClk() => GetSetGfxClkCommand() != 0 || GetSetMinGfxClkCommand() != 0;
        public bool CanSetCoAll() => GetSetCoAllCommand() != 0;
        public bool CanSetCoPer() => GetSetCoPerCommand() != 0;
        public bool CanSetCoGfx() => GetSetCoGfxCommand() != 0;

        private uint GetSetStapmCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    //case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
                    return 0x1A;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                //case CpuCodeName.Cezanne:
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

        private uint GetSetFastCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.RavenRidge:
                case CpuCodeName.RavenRidge2:
                    //case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
                    return 0x1B;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                //case CpuCodeName.Cezanne:
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
                    //case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
                    return 0x1C;

                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                //case CpuCodeName.Cezanne:
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

        private uint GetSetCoAllCommand()
        {
            switch (_cpuCodeName)
            {
                case CpuCodeName.Renoir:
                case CpuCodeName.Lucienne:
                    //case CpuCodeName.Cezanne:
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
                    //case CpuCodeName.Cezanne:
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
                    //case CpuCodeName.Cezanne:
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
                    //case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
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
                    //case CpuCodeName.Picasso:
                    //case CpuCodeName.Dali:
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
                //case CpuCodeName.Cezanne:
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _pawnIO?.Dispose();
                    PciBusMutex.Close();
                }

                _initialized = false;
                _disposed = true;
            }
        }

        ~RyzenSmuService()
        {
            Dispose(false);
        }
    }
}