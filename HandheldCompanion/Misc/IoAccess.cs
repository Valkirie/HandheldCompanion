using LibreHardwareMonitor.Hardware.Motherboard.Lpc;
using LibreHardwareMonitor.PawnIo;
using System;

public sealed class IoAccess : IDisposable
{
    private readonly LpcIo _pio = new();     // raw port I/O (SelectSlot lives here)
    private LpcPort? _sio;

    public void EnsureSuperIo(ushort indexPort, ushort dataPort)
    {
        // Create once (or reuse)
        _sio ??= new LpcPort(indexPort, dataPort);

        // Map index port to PawnIO slot
        int slot = indexPort switch
        {
            0x2E => 0, // index 0x2E / data 0x2F
            0x4E => 1, // index 0x4E / data 0x4F
            _ => -1
        };

        if (slot >= 0)
            _pio.SelectSlot(slot);
    }

    // Raw EC ports (0x66/0x62)
    public byte InB(ushort port) => _pio.ReadPort(port);
    public void OutB(ushort port, byte value) => _pio.WritePort(port, value);

    // Super-IO passthrough (index/data)
    public void SioEnterIT87() => _sio?.IT87Enter();
    public void SioExitIT87() => _sio?.IT87Exit();
    public void SioWritePort(ushort port, byte value) => _sio?.WriteIoPort(port, value);
    public byte SioReadPort(ushort port) => _sio is null ? (byte)0 : _sio.ReadIoPort(port);

    public void Close()
    {
        _sio?.Close();
        _pio.Close();
    }
    public void Dispose() => Close();
}