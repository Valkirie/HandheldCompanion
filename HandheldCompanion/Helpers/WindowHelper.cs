using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Helpers
{
    public static class WindowHelper
    {
        // Define a delegate that matches the EnumWindows callback signature.
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // Import EnumWindows, but note we now expect a function pointer (IntPtr) instead of a delegate.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(IntPtr lpEnumFunc, IntPtr lParam);

        // This is the callback function that will be called for each window.
        // It retrieves the list passed in via lParam, and adds the window handle to it.
        private static bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            // Retrieve the List<IntPtr> that we passed in.
            List<IntPtr> windows = (List<IntPtr>)GCHandle.FromIntPtr(lParam).Target;
            windows.Add(hWnd);
            return true; // Continue enumeration.
        }

        // Public method that returns all window handles.
        public static List<IntPtr> GetOpenWindows()
        {
            var windows = new List<IntPtr>();

            // Create a GCHandle for our list so we can pass it to unmanaged code.
            GCHandle listHandle = GCHandle.Alloc(windows);
            try
            {
                // Create the delegate instance.
                EnumWindowsProc callbackDelegate = new EnumWindowsProc(EnumWindowCallback);
                // Get a function pointer for the delegate.
                IntPtr pointer = Marshal.GetFunctionPointerForDelegate(callbackDelegate);
                // Call EnumWindows with the function pointer and a pointer to our list.
                EnumWindows(pointer, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                // Always free the GCHandle to avoid memory leaks.
                listHandle.Free();
            }
            return windows;
        }
    }
}
