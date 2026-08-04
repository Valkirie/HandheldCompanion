using System;
using System.Globalization;
using System.Linq;
using System.Management;

namespace HandheldCompanion.Devices;

internal sealed class OneXPlayerWmiEc : IDisposable
{
    private readonly ManagementObject _instance;

    public OneXPlayerWmiEc()
    {
        using ManagementObjectSearcher searcher = new("root\\WMI", "SELECT * FROM SuRwECRegInterface");
        using ManagementObjectCollection instances = searcher.Get();
        _instance = instances.Cast<ManagementObject>().FirstOrDefault()
            ?? throw new InvalidOperationException("SuRwECRegInterface is unavailable");
    }

    public byte ReadByte(ushort register)
    {
        object?[] arguments = [PackAddress(register), null];
        _instance.InvokeMethod("ReadECReg", arguments);

        string response = GetResponse(arguments, "ReadECReg");
        string[] fields = response.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length < 2 || ParseHexByte(fields[0]) != 0)
            throw new InvalidOperationException($"ReadECReg failed: {response}");

        return ParseHexByte(fields[1]);
    }

    public void WriteByte(ushort register, byte value)
    {
        object?[] arguments = [PackAddress(register) | ((uint)value << 16), null];
        _instance.InvokeMethod("WriteECReg", arguments);

        string response = GetResponse(arguments, "WriteECReg");
        string[] fields = response.Split(',', StringSplitOptions.TrimEntries);
        if (fields.Length == 0 || ParseHexByte(fields[0]) != 0)
            throw new InvalidOperationException($"WriteECReg failed: {response}");
    }

    public void Dispose() => _instance.Dispose();

    private static uint PackAddress(ushort register)
    {
        byte bank = (byte)(register >> 8);
        byte offset = (byte)register;
        return bank | ((uint)offset << 8);
    }

    private static string GetResponse(object?[] arguments, string method)
    {
        return arguments.Length > 1 && arguments[1] is string response
            ? response
            : throw new InvalidOperationException($"{method} returned no response");
    }

    private static byte ParseHexByte(string value)
    {
        string normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        if (byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte result))
            return result;

        throw new InvalidOperationException($"Invalid EC response byte: {value}");
    }
}
