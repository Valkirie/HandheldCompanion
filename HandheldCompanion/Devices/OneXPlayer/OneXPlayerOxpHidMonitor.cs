using hidapi;
using hidapi.Native;
using System;
using HandheldCompanion.Shared;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Devices;

public enum OxpHidInitProfile
{
    None = 0,
    X1 = 1,
    X1Mini = 2,
    Apex = 3,
    X2 = 4,
}

internal sealed class OneXPlayerOxpHidMonitor : IDisposable
{
    public const ushort VID = 0x1A86;
    public const ushort PID = 0xFE00;
    // OneXPlayer X2 exposes a second CH34x control chip that older models don't have;
    // the paddle/OEM button reports live on either chip's vendor interface (MI_02).
    public const ushort PID_X2 = 0x1305;
    public const int InterfaceNumber = 0x02;
    public const ushort InputReportLength = 64;

    private const byte FrameMarker = 0x3F;
    private const byte ButtonCommandId = 0xB2;
    private const byte StatusCommandId = 0xB8;
    private const byte VibrationCommandId = 0xB3;

    private HidDevice? _hidDevice;
    // Actual input-report length of the opened interface. X1 = 64; X2's FE00 MI_02 = 65
    // (numbered report / leading report-ID byte), so it must not be hardcoded.
    private int _reportLength = InputReportLength;
    private int _outputReportLength = InputReportLength;
    private readonly bool[] _buttonStates = new bool[0x25];
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private Task? _initializationTask;
    private OxpHidInitProfile _initProfile = OxpHidInitProfile.None;
    private bool _reinitializeRequested;

    public event Action<byte, bool>? ButtonChanged;

    public bool IsOpen => _hidDevice is not null && _hidDevice.IsDeviceValid;

    public bool Open(OxpHidInitProfile initProfile)
    {
        ushort[] candidatePids = initProfile == OxpHidInitProfile.X2 ? [PID, PID_X2] : [PID];

        foreach (ushort pid in candidatePids)
        {
            if (TryOpenVendorInterface(VID, pid, initProfile))
                return true;
        }

        LogManager.LogWarning("No OneXPlayer vendor HID interface (MI_{0:X2}) could be opened", InterfaceNumber);
        return false;
    }

    private bool TryOpenVendorInterface(ushort vid, ushort pid, OxpHidInitProfile initProfile)
    {
        IntPtr devEnum = HidApiNative.hid_enumerate(vid, pid);
        if (devEnum == IntPtr.Zero)
            return false;

        try
        {
            IntPtr deviceInfo = devEnum;
            while (deviceInfo != IntPtr.Zero)
            {
                HidDeviceInfo hidDeviceInfo = new(deviceInfo);

                // Only the vendor interface (MI_02) carries the button-remap protocol.
                if (hidDeviceInfo.InterfaceNumber == InterfaceNumber)
                {
                    // Open with the interface's ACTUAL report length (X2 MI_02 is 65, not 64);
                    // otherwise OpenDevice() rejects it on its report-length check.
                    ushort inputReportLength;
                    try { inputReportLength = HidDevice.GetInputReportByteLength(hidDeviceInfo.Path); }
                    catch { inputReportLength = 0; }
                    if (inputReportLength == 0)
                        inputReportLength = InputReportLength;

                    ushort outputReportLength;
                    try { outputReportLength = HidDevice.GetOutputReportByteLength(hidDeviceInfo.Path); }
                    catch { outputReportLength = 0; }
                    if (outputReportLength == 0)
                        outputReportLength = inputReportLength;

                    try
                    {
                        HidDevice candidate = new(vid, pid, inputReportLength, -1, hidDeviceInfo.Path, outputReportLength);
                        // The constructor does NOT open the device; OpenDevice() opens the handle.
                        if (candidate.OpenDevice(hidDeviceInfo.Path))
                        {
                            _hidDevice = candidate;
                            _initProfile = initProfile;
                            _reportLength = inputReportLength;
                            _outputReportLength = outputReportLength;

                            LogManager.LogInformation("Opened OneXPlayer vendor interface VID=0x{0:X4} PID=0x{1:X4} MI_{2:X2}", vid, pid, InterfaceNumber);

                            CancellationTokenSource cancellation = new();
                            _cancellationTokenSource = cancellation;
                            InitializeProfile(cancellation.Token);
                            _readTask = Task.Run(() => ReadLoop(cancellation.Token));
                            return true;
                        }

                        candidate.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning("Exception opening OneXPlayer vendor interface: {0}", ex.Message);
                    }
                }

                deviceInfo = hidDeviceInfo.NextDevicePtr;
            }
        }
        finally
        {
            HidApiNative.hid_free_enumeration(devEnum);
        }

        return false;
    }

    public void Close()
    {
        CancellationTokenSource? cancellation = _cancellationTokenSource;
        cancellation?.Cancel();

        WaitForTask(_readTask);
        WaitForTask(_initializationTask);

        _readTask = null;
        _initializationTask = null;
        cancellation?.Dispose();
        _cancellationTokenSource = null;
        _reinitializeRequested = false;
        _initProfile = OxpHidInitProfile.None;

        Array.Clear(_buttonStates, 0, _buttonStates.Length);

        if (_hidDevice is not null)
        {
            try
            {
                _hidDevice.Close();
            }
            catch
            { }
            finally
            {
                _hidDevice = null;
            }
        }
    }

    public void Dispose()
    {
        Close();
    }

    private static void WaitForTask(Task? task)
    {
        try { task?.Wait(250); }
        catch { }
    }

    private void ReadLoop(CancellationToken cancellationToken)
    {
        HidDevice? device = _hidDevice;
        if (device is null)
            return;

        int reportLength = _reportLength;
        byte[] report = new byte[reportLength];

        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead;
            try
            {
                bytesRead = device.Read(report, 10);
            }
            catch
            {
                break;
            }

            if (bytesRead <= 0)
                continue;

            if (_reinitializeRequested)
            {
                InitializeProfile(cancellationToken);
                _reinitializeRequested = false;
            }

            // Button reports are framed B2 3F ... 3F B2. hidapi returns 64 payload bytes on both
            // X1 (report length 64) and X2 (65, leading report-ID byte stripped), so parse using
            // the actual bytesRead rather than the interface's declared report length.
            if (bytesRead < 14)
                continue;

            if (report[1] != FrameMarker || report[bytesRead - 2] != FrameMarker)
                continue;

            if (report[0] == StatusCommandId)
            {
                HandleStatusReport(report);
                continue;
            }

            if (report[0] != ButtonCommandId)
                continue;

            byte buttonId = report[6];
            if (buttonId >= _buttonStates.Length)
                continue;

            bool pressed = report[12] == 0x01;
            if (_buttonStates[buttonId] == pressed)
                continue;

            _buttonStates[buttonId] = pressed;
            ButtonChanged?.Invoke(buttonId, pressed);
        }
    }

    private void HandleStatusReport(byte[] report)
    {
        if (_initProfile != OxpHidInitProfile.X1Mini)
            return;

        if (report[3] == 0xFE)
            _reinitializeRequested = true;
    }

    private void InitializeProfile(CancellationToken cancellationToken)
    {
        switch (_initProfile)
        {
            case OxpHidInitProfile.X1:
                WriteCommand(0xB4, BuildRemapPage1(0x01));
                Thread.Sleep(50);
                WriteCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
                break;
            case OxpHidInitProfile.X1Mini:
                WriteCommand(0xB4, BuildRemapPage1(0x01));
                Thread.Sleep(50);
                WriteCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
                Thread.Sleep(50);
                WriteCommand(VibrationCommandId, BuildMotorLevelPayload(0x05));
                break;
            case OxpHidInitProfile.Apex:
                WriteCommand(0xB4, BuildRemapPage1(0x01));
                Thread.Sleep(100);
                WriteCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
                Thread.Sleep(100);
                WriteCommand(0xB2, [0x01, 0x1F, 0x40, 0x03, 0x02, 0x03, 0x00, 0x00, 0x00, 0x01]);
                Thread.Sleep(200);
                WriteCommand(0xB2, [0x00, 0x01, 0x02]);
                break;
            case OxpHidInitProfile.X2:
                InitializeX2(cancellationToken);
                break;
        }
    }

    private void InitializeX2(CancellationToken cancellationToken)
    {
        WriteCommand(0xB4, BuildRemapPage1(0x01));
        Thread.Sleep(50);
        WriteCommand(0xB4, BuildRemapPage2(0x01, 0x67, 0x66));
        _initializationTask = EnableX2InterceptAsync(cancellationToken);
    }

    private async Task EnableX2InterceptAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(4000, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            WriteCommand(0xB2, [0x01, 0x1F, 0x40, 0x03, 0x02, 0x03, 0x00, 0x00, 0x00, 0x01]);
        }
        catch (OperationCanceledException)
        { }
        catch (Exception ex)
        {
            LogManager.LogWarning("X2 intercept initialization failed: {0}", ex.Message);
        }
    }

    private static byte[] BuildMotorLevelPayload(byte level)
    {
        return [0x01, 0x05, level];
    }

    private void WriteCommand(byte commandId, byte[] payload)
    {
        _hidDevice?.Write(BuildCommand(commandId, payload));
    }

    // Report-length aware: the trailing markers must use the output report's final bytes or the
    // firmware rejects the command when input and output report lengths differ.
    private byte[] BuildCommand(byte commandId, byte[] payload, byte index = 0x01)
    {
        int length = _outputReportLength;
        byte[] command = new byte[length];
        command[0] = commandId;
        command[1] = FrameMarker;
        command[2] = index;

        int count = Math.Min(payload.Length, length - 5);
        Array.Copy(payload, 0, command, 3, count);

        command[length - 2] = FrameMarker;
        command[length - 1] = commandId;
        return command;
    }

    private static byte[] BuildRemapPage1(byte preset)
    {
        return
        [
            0x02, 0x38, 0x20, 0x01, preset,
            0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
            0x02, 0x01, 0x02, 0x00, 0x00, 0x00,
            0x03, 0x01, 0x03, 0x00, 0x00, 0x00,
            0x04, 0x01, 0x04, 0x00, 0x00, 0x00,
            0x05, 0x01, 0x05, 0x00, 0x00, 0x00,
            0x06, 0x01, 0x06, 0x00, 0x00, 0x00,
            0x07, 0x01, 0x07, 0x00, 0x00, 0x00,
            0x08, 0x01, 0x08, 0x00, 0x00, 0x00,
            0x09, 0x01, 0x09, 0x00, 0x00, 0x00,
        ];
    }

    private static byte[] BuildRemapPage2(byte preset, byte m1KeyCode, byte m2KeyCode)
    {
        return
        [
            0x02, 0x38, 0x20, 0x02, preset,
            0x0A, 0x01, 0x0A, 0x00, 0x00, 0x00,
            0x0B, 0x01, 0x0B, 0x00, 0x00, 0x00,
            0x0C, 0x01, 0x0C, 0x00, 0x00, 0x00,
            0x0D, 0x01, 0x0D, 0x00, 0x00, 0x00,
            0x0E, 0x01, 0x0E, 0x00, 0x00, 0x00,
            0x0F, 0x01, 0x0F, 0x00, 0x00, 0x00,
            0x10, 0x01, 0x10, 0x00, 0x00, 0x00,
            0x22, 0x02, 0x01, m1KeyCode, 0x00, 0x00,
            0x23, 0x02, 0x01, m2KeyCode, 0x00, 0x00,
        ];
    }
}
