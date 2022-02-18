using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    public static class ImageUtilities
    {
        public static Icon GetRegisteredIcon(string filePath)
        {
            var shinfo = new SHfileInfo();
            Win32.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), Win32.SHGFI_ICON | Win32.SHGFI_SMALLICON);
            return Icon.FromHandle(shinfo.hIcon);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SHfileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }


    internal sealed class Win32
    {
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0; // large
        public const uint SHGFI_SMALLICON = 0x1; // small

        [DllImport("shell32.dll")]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHfileInfo psfi, uint cbSizeFileInfo, uint uFlags);
    }
}
