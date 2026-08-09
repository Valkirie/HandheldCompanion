using System;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HandheldCompanion.Targets.Viiper
{
    /// <summary>
    /// P/Invoke declarations for the libviiper CGo shared library.
    /// Ported from ViiperController reference implementation.
    /// </summary>
    internal static class LibViiper
    {
        private const string DllName = "libviiper";

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int viiper_init(string listenAddr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void viiper_shutdown();

        // -----------------------------------------------------------------------
        // Bus management
        // -----------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_bus_create(uint busId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_bus_remove(uint busId);

        // -----------------------------------------------------------------------
        // Device management
        // -----------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int viiper_device_add(uint busId, string typeName, out uint deviceId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int viiper_device_add_ex(uint busId, string typeName, ushort vid, ushort pid, out uint deviceId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_device_remove(uint busId, uint deviceId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_device_attach(uint busId, uint deviceId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr viiper_list_device_types();

        // -----------------------------------------------------------------------
        // Input state
        // -----------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_device_set_input(uint busId, uint deviceId, byte[] data, int len);

        // -----------------------------------------------------------------------
        // Feedback callback
        // -----------------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FeedbackCallback(uint busId, uint deviceId, IntPtr data, int len, IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int viiper_device_set_feedback_callback(
            uint busId, uint deviceId, FeedbackCallback cb, IntPtr userData);

        // -----------------------------------------------------------------------
        // Error info / Memory management
        // -----------------------------------------------------------------------

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr viiper_last_error();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void viiper_free_string(IntPtr s);

        /// <summary>
        /// Returns the last error message as a managed string, or null if no error.
        /// Frees the native string after copying.
        /// </summary>
        public static string GetLastError()
        {
            var ptr = viiper_last_error();
            if (ptr == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringAnsi(ptr);
            }
            finally
            {
                viiper_free_string(ptr);
            }
        }

        /// <summary>
        /// Returns the list of supported device types, or empty on error.
        /// </summary>
        public static string[] GetDeviceTypes()
        {
            var ptr = viiper_list_device_types();
            if (ptr == IntPtr.Zero) return new string[0];
            try
            {
                var json = Marshal.PtrToStringAnsi(ptr);
                if (string.IsNullOrEmpty(json)) return new string[0];
                return JsonSerializer.Deserialize<string[]>(json) ?? new string[0];
            }
            finally
            {
                viiper_free_string(ptr);
            }
        }
    }
}
