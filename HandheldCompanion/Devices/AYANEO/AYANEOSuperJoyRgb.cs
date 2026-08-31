using HandheldCompanion.Shared;
using HidLibrary;
using System;
using System.Windows.Media;
using NativeHidDevice = hidapi.HidDevice;

namespace HandheldCompanion.Devices.AYANEO;

internal sealed class AYANEOSuperJoyRgb
{
    public const int VendorId = 0x1C4F;
    public const int ProductId = 0x0002;

    private const short UsagePage = unchecked((short)0xFF00);
    private const short Usage = 0x0001;
    private const byte ConfigCommand = 0x21;
    private const byte ConfigSubcommand = 0x09;
    private const byte ModeSolid = 0x01;
    private const byte ModeOff = 0xFF;
    private const byte MediumVibration = 0x20;
    private const int PacketLength = 64;
    private const int HidReportLength = PacketLength + 1;
    private const int CommandOffset = 2;
    private const int SubcommandOffset = 3;
    private const int ChecksumDataOffset = 6;
    private const int RightStickOffset = 7;
    private const int LeftStickOffset = 11;
    private const int VibrationOffset = 23;
    private const int ConfigMarkerOffset = 31;
    private const int TransactionAttempts = 3;
    private const int TransactionTimeoutMilliseconds = 300;

    // HidLibrary selects and tracks the interface; hidapi handles timed I/O.
    private HidDevice? hidInterface;
    private NativeHidDevice? device;

    public bool IsOpen => device is { IsDeviceValid: true }
        && hidInterface is { IsConnected: true };

    public static bool MatchesInterface(HidDevice hidDevice)
    {
        return hidDevice.Capabilities.UsagePage == UsagePage
            && hidDevice.Capabilities.Usage == Usage
            && hidDevice.Capabilities.InputReportByteLength == HidReportLength
            && hidDevice.Capabilities.OutputReportByteLength == HidReportLength;
    }

    public bool Open(HidDevice hidDevice)
    {
        Close();
        hidInterface = hidDevice;

        if (!hidDevice.IsConnected)
        {
            Close();
            return false;
        }

        NativeHidDevice openDevice = new(
            (ushort)VendorId,
            (ushort)ProductId,
            HidReportLength,
            -1);
        device = openDevice;

        try
        {
            if (!openDevice.OpenDevice(hidDevice.DevicePath))
            {
                LogManager.LogWarning("SuperJoy stick RGB open failed");
                Close();
                return false;
            }
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("SuperJoy stick RGB open failed: {0}", ex.Message);
            Close();
            return false;
        }

        LogManager.LogInformation("SuperJoy stick RGB opened ({0})", hidDevice.DevicePath);
        return true;
    }

    public void Close()
    {
        NativeHidDevice? openDevice = device;
        HidDevice? openInterface = hidInterface;
        device = null;
        hidInterface = null;

        if (openDevice is null && openInterface is null)
            return;

        try
        {
            openDevice?.Dispose();
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("SuperJoy stick RGB close failed: {0}", ex.Message);
        }

        try
        {
            openInterface?.Dispose();
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("SuperJoy stick RGB interface close failed: {0}", ex.Message);
        }
    }

    public void SetSticks(Color left, Color right, int brightness, bool enabled)
    {
        Transact(BuildConfigPacket(left, right, brightness, enabled));
    }

    private static byte[] BuildConfigPacket(Color left, Color right, int brightness, bool enabled)
    {
        byte[] packet = new byte[PacketLength];
        packet[CommandOffset] = ConfigCommand;
        packet[SubcommandOffset] = ConfigSubcommand;
        packet[VibrationOffset] = MediumVibration;
        packet[ConfigMarkerOffset] = 0x01;

        PatchStick(packet, RightStickOffset, right, brightness, enabled);
        PatchStick(packet, LeftStickOffset, left, brightness, enabled);

        ushort checksum = 0;
        for (int offset = ChecksumDataOffset; offset < packet.Length; offset++)
            checksum += packet[offset];

        packet[0] = (byte)checksum;
        packet[1] = (byte)(checksum >> 8);
        return packet;
    }

    private static void PatchStick(byte[] packet, int offset, Color color, int brightness, bool enabled)
    {
        int level = enabled ? Math.Clamp(brightness, 0, 100) : 0;
        packet[offset] = enabled ? ModeSolid : ModeOff;
        packet[offset + 1] = Scale(color.R, level);
        packet[offset + 2] = Scale(color.G, level);
        packet[offset + 3] = Scale(color.B, level);
    }

    private static byte Scale(byte component, int level)
    {
        return (byte)(component * level / 100);
    }

    private void Transact(byte[] packet)
    {
        NativeHidDevice? openDevice = device;
        HidDevice? openInterface = hidInterface;
        if (openDevice is null
            || !openDevice.IsDeviceValid
            || openInterface is null
            || !openInterface.IsConnected)
        {
            return;
        }

        try
        {
            byte[] request = new byte[HidReportLength];
            byte[] response = new byte[HidReportLength];
            Buffer.BlockCopy(packet, 0, request, 1, packet.Length);

            for (int attempt = 0; attempt < TransactionAttempts; attempt++)
            {
                openDevice.Write(request);
                int responseLength = openDevice.Read(response, TransactionTimeoutMilliseconds);
                if (responseLength == PacketLength
                    && response[SubcommandOffset] == ConfigSubcommand)
                {
                    return;
                }
            }

            LogManager.LogWarning("SuperJoy stick RGB transaction failed");
        }
        catch (Exception ex)
        {
            LogManager.LogError("SuperJoy stick RGB transaction failed: {0}", ex.Message);
            Close();
        }
    }
}
