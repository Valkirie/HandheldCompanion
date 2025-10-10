using LibreHardwareMonitor.Hardware.Motherboard.Lpc;
using LibreHardwareMonitor.Hardware.Motherboard.Lpc.EC;
using LibreHardwareMonitor.PawnIo;
using System;

namespace HandheldCompanion
{
    public class OpenLibSys : IDisposable
    {
        // --- State ---
        private readonly object _ioLock = new();
        private LpcIo? _pio;                 // raw port I/O (inb/outb)
        private LpcPort? _lpc;               // SIO config sequences based on (registerPort,valuePort)
        private WindowsEmbeddedControllerIO? _ec;
        private ushort _regPort;             // last-entered register port (0x2E or 0x4E)
        private ushort _valPort;             // last-entered value port (0x2F or 0x4F)
        private bool _entered;

        /// <summary>
        /// Mirrors the old InitializeOls() entrypoint used by callers.
        /// Now a light-weight initializer for PawnIO; returns true on success.
        /// </summary>
        public bool InitializeOls()
        {
            lock (_ioLock)
            {
                if (_pio != null && _ec != null)
                    return true;

                try
                {
                    // LpcIo is a tiny userspace shim that talks to the PawnIO kernel helper.
                    // No extra service install or WinRing0 needed.
                    _pio = new LpcIo();

                    // Init LHM EC I/O
                    _ec = new WindowsEmbeddedControllerIO();

                    return true;
                }
                catch { /* ignore */ }

                return false;
            }
        }

        /// <summary>
        /// Read 8-bit from an IO port (replacement for WinRing0 ReadIoPortByte).
        /// </summary>
        public byte ReadIoPortByte(ushort port)
        {
            lock (_ioLock)
            {
                // PawnIO has slot selection concept; pick based on the super-IO register port convention.
                // For simple port I/O, this is generally not required, but we keep a sane default:
                _pio?.SelectSlot(port == 0x2e ? 0 : 1);
                return _pio?.ReadPort(port) ?? 0;
            }
        }

        /// <summary>
        /// Write 8-bit to an IO port (replacement for WinRing0 WriteIoPortByte).
        /// </summary>
        public void WriteIoPortByte(ushort port, byte value)
        {
            lock (_ioLock)
            {
                _pio?.SelectSlot(port == 0x2e ? 0 : 1);
                _pio?.WritePort(port, value);
            }
        }

        /// <summary>
        /// Enter Super I/O configuration space.
        /// Keeps the same signature used by IDevice/AYANEO paths.
        /// </summary>
        public void EnterSuperIoConfig(ushort registerPort, ushort valuePort)
        {
            lock (_ioLock)
            {
                // If we are already in config mode for the same ports, do nothing.
                if (_entered && _lpc != null && _regPort == registerPort && _valPort == valuePort)
                    return;

                _lpc = new LpcPort(registerPort, valuePort);
                _regPort = registerPort;
                _valPort = valuePort;

                // Use the correct unlock sequence for the typical vendors by port pair:
                // - IT87 family commonly uses 0x4E/0x4F
                // - Winbond/Nuvoton/Fintek commonly use 0x2E/0x2F
                if (registerPort == 0x4E && valuePort == 0x4F)
                {
                    _lpc.IT87Enter();
                }
                else if (registerPort == 0x2E && valuePort == 0x2F)
                {
                    _lpc.WinbondNuvotonFintekEnter();
                }
                else
                {
                    // Fallback: try IT87 first, then Winbond/Nuvoton style if needed.
                    // This preserves compatibility for unusual SIO mappings.
                    try { _lpc.IT87Enter(); _entered = true; return; }
                    catch { /* ignore and try second */ }

                    _lpc.WinbondNuvotonFintekEnter();
                }

                _entered = true;
            }
        }

        /// <summary>
        /// Exit Super I/O configuration space.
        /// </summary>
        public void ExitSuperIoConfig(ushort registerPort, ushort valuePort)
        {
            lock (_ioLock)
            {
                if (!_entered || _lpc == null)
                    return;

                // IT87Exit is intentionally a no-op (per LpcPort implementation).
                // Winbond/Nuvoton/Fintek require 0xAA write to exit.
                if (_regPort == 0x2E && _valPort == 0x2F)
                {
                    _lpc.WinbondNuvotonFintekExit();
                }
                else
                {
                    _lpc.IT87Exit(); // no-op; safe for IT87 path
                }

                _entered = false;
            }
        }

        public void Dispose()
        {
            lock (_ioLock)
            {
                try
                {
                    if (_entered && _lpc != null)
                    {
                        // Best effort to exit if still inside config
                        try
                        {
                            if (_regPort == 0x2E && _valPort == 0x2F)
                                _lpc.WinbondNuvotonFintekExit();
                            else
                                _lpc.IT87Exit();
                        }
                        catch { /* ignore dispose-time errors */ }
                    }
                }
                finally
                {
                    _entered = false;
                    _lpc = null;

                    if (_pio != null)
                    {
                        try { _pio.Close(); } catch { /* ignore */ }
                        _pio = null;
                    }

                    if (_ec is not null)
                    {
                        try { _ec.Dispose(); } catch { /* ignore */ }
                        _ec = null;
                    }
                }
            }
        }

        public void EcWriteByte(byte register, byte data)
        {
            lock (_ioLock)
                _ec?.WriteByte(register, data);
        }

        public byte EcReadByte(byte register)
        {
            lock (_ioLock)
                return _ec?.ReadByte(register) ?? 0;
        }

        public void EcWriteCommand(byte value)
        {
            lock (_ioLock)
                _ec?.WriteIOPort(WindowsEmbeddedControllerIO.Port.Command, value);
        }

        public void EcWriteData(byte value)
        {
            lock (_ioLock)
                _ec?.WriteIOPort(WindowsEmbeddedControllerIO.Port.Data, value);
        }

        public byte EcReadStatus()
        {
            lock (_ioLock)
                return _ec?.ReadIOPort(WindowsEmbeddedControllerIO.Port.Command) ?? 0;
        }

        public bool EcIsReady()
        {
            // Ready when InputBufferFull is 0
            byte status = EcReadStatus();
            return (status & (byte)WindowsEmbeddedControllerIO.Status.InputBufferFull) == 0;
        }
    }
}