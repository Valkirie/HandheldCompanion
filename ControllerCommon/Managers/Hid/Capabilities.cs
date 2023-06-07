using System;
using System.Runtime.InteropServices;

namespace ControllerCommon.Managers.Hid;

[StructLayout(LayoutKind.Sequential)]
public struct Capabilities
{
    public short Usage;
    public short UsagePage;
    public short InputReportByteLength;
    public short OutputReportByteLength;
    public short FeatureReportByteLength;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
    public short[] Reserved;

    public short NumberLinkCollectionNodes;
    public short NumberInputButtonCaps;
    public short NumberInputValueCaps;
    public short NumberInputDataIndices;
    public short NumberOutputButtonCaps;
    public short NumberOutputValueCaps;
    public short NumberOutputDataIndices;
    public short NumberFeatureButtonCaps;
    public short NumberFeatureValueCaps;
    public short NumberFeatureDataIndices;
}

public static class GetCapabilities
{
    [DllImport("hid.dll")]
    internal static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, ref IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, ref Capabilities capabilities);

    [DllImport("hid.dll")]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    public static Capabilities? Get(IntPtr handle)
    {
        var capabilities = new Capabilities();
        var preparsedDataPointer = IntPtr.Zero;

        if (HidD_GetPreparsedData(handle, ref preparsedDataPointer))
        {
            HidP_GetCaps(preparsedDataPointer, ref capabilities);
            HidD_FreePreparsedData(preparsedDataPointer);
            return capabilities;
        }

        return null;
    }
}