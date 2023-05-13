using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace hidapi.Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct HidDeviceInfoStruct
    {
		/** Platform-specific device path */
		public IntPtr path;
		/** Device Vendor ID */
		public ushort vendor_id;
		/** Device Product ID */
		public ushort product_id;
		/** Serial Number */
		public IntPtr serial_number;
		/** Device Release Number in binary-coded decimal,
			also known as Device Version Number */
		public ushort release_number;
		/** Manufacturer String */
		public IntPtr manufacturer_string;
		/** Product string */
		public IntPtr product_string;
		/** Usage Page for this Device/Interface
			(Windows/Mac only). */
		public ushort usage_page;
		/** Usage for this Device/Interface
			(Windows/Mac only).*/
		public ushort usage;
		/** The USB interface which this logical device
			represents. Valid on both Linux implementations
			in all cases, and valid on the Windows implementation
			only if the device contains more than one interface. */
		public int interface_number;

		/** Pointer to the next device */
		public IntPtr next;
    }
}
