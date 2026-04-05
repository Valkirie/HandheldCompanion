using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HandheldCompanion.Controllers
{
    /// <summary>
    /// P/Invoke wrapper for the OpenXinput DLL functions that get and set the
    /// XInput user index (player slot 0–N) associated with a controller device path.
    /// </summary>
    public static class OpenXInput
    {
        /// <summary>
        /// Name of the OpenXinput DLL to load. Update this to match the actual
        /// file name on disk (e.g. "OpenXinput" for OpenXinput.dll).
        /// </summary>
        private const string DllName = "Xinput1_4";

        /// <summary>
        /// True when the running <c>XInput1_4.dll</c> exposes the OpenXInput extensions
        /// (<c>OpenXInputGetUserIndex</c> / <c>OpenXInputSetUserIndex</c>).
        /// False when the standard Windows DLL is present instead.
        /// </summary>
        public static bool IsAvailable => _isAvailable.Value;

        private static readonly Lazy<bool> _isAvailable = new(() =>
        {
            try
            {
                IntPtr lib = NativeLibrary.Load("xinput1_4.dll");
                return NativeLibrary.TryGetExport(lib, "OpenXInputGetUserIndex", out _) &&
                       NativeLibrary.TryGetExport(lib, "OpenXInputSetUserIndex", out _);
            }
            catch
            {
                return false;
            }
        });

        public const uint ERROR_SUCCESS = 0;
        public const uint ERROR_INSUFFICIENT_BUFFER = 122;
        public const uint ERROR_DEVICE_NOT_CONNECTED = 1167;

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private static extern uint OpenXInputGetUserIndex(
            [MarshalAs(UnmanagedType.LPWStr)] string lpDevicePath,
            out byte pUserIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)]
        private static extern uint OpenXInputSetUserIndex(
            [MarshalAs(UnmanagedType.LPWStr)] string lpDevicePath,
            byte dwUserIndex, bool powerDownOnChange);

        [DllImport("OpenXinput.dll", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
        private static extern uint OpenXInputRefreshMappings();

        [DllImport("OpenXinput.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern uint OpenXInputGetDevicePath(
            uint dwUserIndex,
            IntPtr pDevicePath,   // NULL to query required size
            ref uint pCount);

        [DllImport("OpenXinput.dll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern uint OpenXInputGetDevicePath(
            uint dwUserIndex,
            StringBuilder pDevicePath,
            ref uint pCount);

        /// <summary>
        /// Returns the device interface path for the given XInput user index,
        /// or null if no device is assigned to that slot.
        /// </summary>
        public static string? GetDevicePath(uint userIndex)
        {
            uint count = 0;
            uint result = OpenXInputGetDevicePath(userIndex, IntPtr.Zero, ref count);

            if (result != ERROR_SUCCESS || count == 0)
                return null;

            var sb = new StringBuilder((int)count);
            result = OpenXInputGetDevicePath(userIndex, sb, ref count);

            return result == ERROR_SUCCESS ? sb.ToString() : null;
        }

        /// <summary>
        /// Returns the XInput user index (0 to max controller count minus 1)
        /// for the controller identified by <paramref name="devicePath"/>.
        /// </summary>
        /// <param name="devicePath">
        /// The device interface path of the controller (e.g. as returned by
        /// SetupDiGetDeviceInterfaceDetail or a HID enumeration).
        /// </param>
        /// <param name="userIndex">
        /// When this method returns <c>ERROR_SUCCESS</c>, contains the XInput
        /// user index assigned to the controller; otherwise the value is undefined.
        /// </param>
        /// <returns>
        /// <c>ERROR_SUCCESS</c> (0) on success, <c>ERROR_DEVICE_NOT_CONNECTED</c>
        /// if no active controller matches the path, or another Win32 error code
        /// on failure.
        /// </returns>
        public static uint GetUserIndex(string devicePath, out byte userIndex)
        {
            userIndex = byte.MaxValue;
            if (!IsAvailable)
                return uint.MaxValue;
            return OpenXInputGetUserIndex(devicePath, out userIndex);
        }

        /// <summary>
        /// Assigns the controller identified by <paramref name="devicePath"/> to the
        /// XInput slot given by <paramref name="userIndex"/>. If that slot is already
        /// occupied by another controller, the two controllers are swapped: the
        /// displaced controller is moved to the slot that was vacated by the requested
        /// device, and both controllers have their LED rings updated accordingly.
        /// </summary>
        /// <param name="devicePath">
        /// The device interface path of the controller to reassign.
        /// </param>
        /// <param name="userIndex">
        /// The target XInput user index (0 to max controller count minus 1).
        /// </param>
        /// <returns>
        /// <c>ERROR_SUCCESS</c> (0) on success, <c>ERROR_DEVICE_NOT_CONNECTED</c>
        /// if no active controller matches the path, <c>ERROR_BAD_ARGUMENTS</c> if
        /// the target slot is out of range, or another Win32 error code on failure.
        /// </returns>
        public static uint SetUserIndex(string devicePath, byte userIndex, bool powerDownOnChange)
        {
            if (!IsAvailable)
                return uint.MaxValue;
            return OpenXInputSetUserIndex(devicePath, userIndex, powerDownOnChange);
        }

        public static uint RefreshMappings()
        {
            return OpenXInputRefreshMappings();
        }
    }
}