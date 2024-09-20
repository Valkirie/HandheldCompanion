using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion.IGCL
{
    // Define the wrapper class for IGCL
    public static class IGCLBackend
    {
        // Define the types used by the C++ functions
        [StructLayout(LayoutKind.Sequential)]
        struct ctl_init_args_t
        {
            public int AppVersion;
            public int flags;
            public int Size;
            public int Version;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_device_adapter_handle_t
        {
            public IntPtr handle;
        }

        public enum ctl_result_t
        {
            CTL_RESULT_SUCCESS = 0x00000000,                ///< success
            CTL_RESULT_SUCCESS_STILL_OPEN_BY_ANOTHER_CALLER = 0x00000001,   ///< success but still open by another caller
            CTL_RESULT_ERROR_SUCCESS_END = 0x0000FFFF,      ///< "Success group error code end value, not to be used
                                                            ///< "
            CTL_RESULT_ERROR_GENERIC_START = 0x40000000,    ///< Generic error code starting value, not to be used
            CTL_RESULT_ERROR_NOT_INITIALIZED = 0x40000001,  ///< Result not initialized
            CTL_RESULT_ERROR_ALREADY_INITIALIZED = 0x40000002,  ///< Already initialized
            CTL_RESULT_ERROR_DEVICE_LOST = 0x40000003,      ///< Device hung, reset, was removed, or driver update occurred
            CTL_RESULT_ERROR_OUT_OF_HOST_MEMORY = 0x40000004,   ///< Insufficient host memory to satisfy call
            CTL_RESULT_ERROR_OUT_OF_DEVICE_MEMORY = 0x40000005, ///< Insufficient device memory to satisfy call
            CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS = 0x40000006, ///< Access denied due to permission level
            CTL_RESULT_ERROR_NOT_AVAILABLE = 0x40000007,    ///< Resource was removed
            CTL_RESULT_ERROR_UNINITIALIZED = 0x40000008,    ///< Library not initialized
            CTL_RESULT_ERROR_UNSUPPORTED_VERSION = 0x40000009,  ///< Generic error code for unsupported versions
            CTL_RESULT_ERROR_UNSUPPORTED_FEATURE = 0x4000000a,  ///< Generic error code for unsupported features
            CTL_RESULT_ERROR_INVALID_ARGUMENT = 0x4000000b, ///< Generic error code for invalid arguments
            CTL_RESULT_ERROR_INVALID_API_HANDLE = 0x4000000c,   ///< API handle in invalid
            CTL_RESULT_ERROR_INVALID_NULL_HANDLE = 0x4000000d,  ///< Handle argument is not valid
            CTL_RESULT_ERROR_INVALID_NULL_POINTER = 0x4000000e, ///< Pointer argument may not be nullptr
            CTL_RESULT_ERROR_INVALID_SIZE = 0x4000000f,     ///< Size argument is invalid (e.g., must not be zero)
            CTL_RESULT_ERROR_UNSUPPORTED_SIZE = 0x40000010, ///< Size argument is not supported by the device (e.g., too large)
            CTL_RESULT_ERROR_UNSUPPORTED_IMAGE_FORMAT = 0x40000011, ///< Image format is not supported by the device
            CTL_RESULT_ERROR_DATA_READ = 0x40000012,        ///< Data read error
            CTL_RESULT_ERROR_DATA_WRITE = 0x40000013,       ///< Data write error
            CTL_RESULT_ERROR_DATA_NOT_FOUND = 0x40000014,   ///< Data not found error
            CTL_RESULT_ERROR_NOT_IMPLEMENTED = 0x40000015,  ///< Function not implemented
            CTL_RESULT_ERROR_OS_CALL = 0x40000016,          ///< Operating system call failure
            CTL_RESULT_ERROR_KMD_CALL = 0x40000017,         ///< Kernel mode driver call failure
            CTL_RESULT_ERROR_UNLOAD = 0x40000018,           ///< Library unload failure
            CTL_RESULT_ERROR_ZE_LOADER = 0x40000019,        ///< Level0 loader not found
            CTL_RESULT_ERROR_INVALID_OPERATION_TYPE = 0x4000001a,   ///< Invalid operation type
            CTL_RESULT_ERROR_NULL_OS_INTERFACE = 0x4000001b,///< Null OS interface
            CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE = 0x4000001c,  ///< Null OS adapter handle
            CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE = 0x4000001d,///< Null display output handle
            CTL_RESULT_ERROR_WAIT_TIMEOUT = 0x4000001e,     ///< Timeout in Wait function
            CTL_RESULT_ERROR_PERSISTANCE_NOT_SUPPORTED = 0x4000001f,///< Persistance not supported
            CTL_RESULT_ERROR_PLATFORM_NOT_SUPPORTED = 0x40000020,   ///< Platform not supported
            CTL_RESULT_ERROR_UNKNOWN_APPLICATION_UID = 0x40000021,  ///< Unknown Appplicaion UID in Initialization call 
            CTL_RESULT_ERROR_INVALID_ENUMERATION = 0x40000022,  ///< The enum is not valid
            CTL_RESULT_ERROR_FILE_DELETE = 0x40000023,      ///< Error in file delete operation
            CTL_RESULT_ERROR_RESET_DEVICE_REQUIRED = 0x40000024,///< The device requires a reset.
            CTL_RESULT_ERROR_FULL_REBOOT_REQUIRED = 0x40000025, ///< The device requires a full reboot.
            CTL_RESULT_ERROR_LOAD = 0x40000026,             ///< Library load failure
            CTL_RESULT_ERROR_UNKNOWN = 0x4000FFFF,          ///< Unknown or internal error
            CTL_RESULT_ERROR_RETRY_OPERATION = 0x40010000,  ///< Operation failed, retry previous operation again
            CTL_RESULT_ERROR_GENERIC_END = 0x4000FFFF,      ///< "Generic error code end value, not to be used
                                                            ///< "
            CTL_RESULT_ERROR_CORE_START = 0x44000000,       ///< Core error code starting value, not to be used
            CTL_RESULT_ERROR_CORE_OVERCLOCK_NOT_SUPPORTED = 0x44000001, ///< The Overclock is not supported.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_VOLTAGE_OUTSIDE_RANGE = 0x44000002, ///< The Voltage exceeds the acceptable min/max.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_FREQUENCY_OUTSIDE_RANGE = 0x44000003,   ///< The Frequency exceeds the acceptable min/max.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_POWER_OUTSIDE_RANGE = 0x44000004,   ///< The Power exceeds the acceptable min/max.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_TEMPERATURE_OUTSIDE_RANGE = 0x44000005, ///< The Power exceeds the acceptable min/max.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_IN_VOLTAGE_LOCKED_MODE = 0x44000006,///< The Overclock is in voltage locked mode.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_RESET_REQUIRED = 0x44000007,///< It indicates that the requested change will not be applied until the
                                                                        ///< device is reset.
            CTL_RESULT_ERROR_CORE_OVERCLOCK_WAIVER_NOT_SET = 0x44000008,///< The $OverclockWaiverSet function has not been called.
            CTL_RESULT_ERROR_CORE_END = 0x0440FFFF,         ///< "Core error code end value, not to be used
                                                            ///< "
            CTL_RESULT_ERROR_3D_START = 0x60000000,         ///< 3D error code starting value, not to be used
            CTL_RESULT_ERROR_3D_END = 0x6000FFFF,           ///< "3D error code end value, not to be used
                                                            ///< "
            CTL_RESULT_ERROR_MEDIA_START = 0x50000000,      ///< Media error code starting value, not to be used
            CTL_RESULT_ERROR_MEDIA_END = 0x5000FFFF,        ///< "Media error code end value, not to be used
                                                            ///< "
            CTL_RESULT_ERROR_DISPLAY_START = 0x48000000,    ///< Display error code starting value, not to be used
            CTL_RESULT_ERROR_INVALID_AUX_ACCESS_FLAG = 0x48000001,  ///< Invalid flag for Aux access
            CTL_RESULT_ERROR_INVALID_SHARPNESS_FILTER_FLAG = 0x48000002,///< Invalid flag for Sharpness
            CTL_RESULT_ERROR_DISPLAY_NOT_ATTACHED = 0x48000003, ///< Error for Display not attached
            CTL_RESULT_ERROR_DISPLAY_NOT_ACTIVE = 0x48000004,   ///< Error for display attached but not active
            CTL_RESULT_ERROR_INVALID_POWERFEATURE_OPTIMIZATION_FLAG = 0x48000005,   ///< Error for invalid power optimization flag
            CTL_RESULT_ERROR_INVALID_POWERSOURCE_TYPE_FOR_DPST = 0x48000006,///< DPST is supported only in DC Mode
            CTL_RESULT_ERROR_INVALID_PIXTX_GET_CONFIG_QUERY_TYPE = 0x48000007,  ///< Invalid query type for pixel transformation get configuration
            CTL_RESULT_ERROR_INVALID_PIXTX_SET_CONFIG_OPERATION_TYPE = 0x48000008,  ///< Invalid operation type for pixel transformation set configuration
            CTL_RESULT_ERROR_INVALID_SET_CONFIG_NUMBER_OF_SAMPLES = 0x48000009, ///< Invalid number of samples for pixel transformation set configuration
            CTL_RESULT_ERROR_INVALID_PIXTX_BLOCK_ID = 0x4800000a,   ///< Invalid block id for pixel transformation
            CTL_RESULT_ERROR_INVALID_PIXTX_BLOCK_TYPE = 0x4800000b, ///< Invalid block type for pixel transformation
            CTL_RESULT_ERROR_INVALID_PIXTX_BLOCK_NUMBER = 0x4800000c,   ///< Invalid block number for pixel transformation
            CTL_RESULT_ERROR_INSUFFICIENT_PIXTX_BLOCK_CONFIG_MEMORY = 0x4800000d,   ///< Insufficient memery allocated for BlockConfigs
            CTL_RESULT_ERROR_3DLUT_INVALID_PIPE = 0x4800000e,   ///< Invalid pipe for 3dlut
            CTL_RESULT_ERROR_3DLUT_INVALID_DATA = 0x4800000f,   ///< Invalid 3dlut data
            CTL_RESULT_ERROR_3DLUT_NOT_SUPPORTED_IN_HDR = 0x48000010,   ///< 3dlut not supported in HDR
            CTL_RESULT_ERROR_3DLUT_INVALID_OPERATION = 0x48000011,  ///< Invalid 3dlut operation
            CTL_RESULT_ERROR_3DLUT_UNSUCCESSFUL = 0x48000012,   ///< 3dlut call unsuccessful
            CTL_RESULT_ERROR_AUX_DEFER = 0x48000013,        ///< AUX defer failure
            CTL_RESULT_ERROR_AUX_TIMEOUT = 0x48000014,      ///< AUX timeout failure
            CTL_RESULT_ERROR_AUX_INCOMPLETE_WRITE = 0x48000015, ///< AUX incomplete write failure
            CTL_RESULT_ERROR_I2C_AUX_STATUS_UNKNOWN = 0x48000016,   ///< I2C/AUX unkonown failure
            CTL_RESULT_ERROR_I2C_AUX_UNSUCCESSFUL = 0x48000017, ///< I2C/AUX unsuccessful
            CTL_RESULT_ERROR_LACE_INVALID_DATA_ARGUMENT_PASSED = 0x48000018,///< Lace Incorrrect AggressivePercent data or LuxVsAggressive Map data
                                                                            ///< passed by user
            CTL_RESULT_ERROR_EXTERNAL_DISPLAY_ATTACHED = 0x48000019,///< External Display is Attached hence fail the Display Switch
            CTL_RESULT_ERROR_CUSTOM_MODE_STANDARD_CUSTOM_MODE_EXISTS = 0x4800001a,  ///< Standard custom mode exists
            CTL_RESULT_ERROR_CUSTOM_MODE_NON_CUSTOM_MATCHING_MODE_EXISTS = 0x4800001b,  ///< Non custom matching mode exists
            CTL_RESULT_ERROR_CUSTOM_MODE_INSUFFICIENT_MEMORY = 0x4800001c,  ///< Custom mode insufficent memory
            CTL_RESULT_ERROR_ADAPTER_ALREADY_LINKED = 0x4800001d,   ///< Adapter is already linked
            CTL_RESULT_ERROR_ADAPTER_NOT_IDENTICAL = 0x4800001e,///< Adapter is not identical for linking
            CTL_RESULT_ERROR_ADAPTER_NOT_SUPPORTED_ON_LDA_SECONDARY = 0x4800001f,   ///< Adapter is LDA Secondary, so not supporting requested operation
            CTL_RESULT_ERROR_SET_FBC_FEATURE_NOT_SUPPORTED = 0x48000020,///< Set FBC Feature not supported
            CTL_RESULT_ERROR_DISPLAY_END = 0x4800FFFF,      ///< "Display error code end value, not to be used
                                                            ///< "
            CTL_RESULT_MAX
        }

        // Define a handle type for the ctl_api_handle_t
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_api_handle_t
        {
            public IntPtr handle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_retro_scaling_caps_t
        {
            public uint Size;
            public byte Version;
            public ctl_retro_scaling_type_flags_t SupportedRetroScaling;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_retro_scaling_settings_t
        {
            public uint Size;
            public byte Version;
            [MarshalAs(UnmanagedType.U1)]
            public bool Get;
            [MarshalAs(UnmanagedType.U1)]
            public bool Enable;
            public ctl_retro_scaling_type_flags_t RetroScalingType;
        }

        public enum ctl_retro_scaling_type_flags_t : uint
        {
            CTL_RETRO_SCALING_TYPE_FLAG_INTEGER = 1,
            CTL_RETRO_SCALING_TYPE_FLAG_NEAREST_NEIGHBOUR = 2,
            CTL_RETRO_SCALING_TYPE_FLAG_MAX = 0x80000000
        }

        // Define the scaling type flags as an enum
        public enum ctl_scaling_type_flag_t : uint
        {
            CTL_SCALING_TYPE_FLAG_IDENTITY = 1,                    // No scaling is applied and display manages scaling itself when possible
            CTL_SCALING_TYPE_FLAG_CENTERED = 2,                    // Source is not scaled but place in the center of the target display
            CTL_SCALING_TYPE_FLAG_STRETCHED = 4,                   // Source is stretched to fit the target size
            CTL_SCALING_TYPE_FLAG_ASPECT_RATIO_CENTERED_MAX = 8,   // The aspect ratio is maintained with the source centered
            CTL_SCALING_TYPE_FLAG_CUSTOM = 16,                      // None of the standard types match this .Additional parameters are required which should be set via a private driver interface
            CTL_SCALING_TYPE_FLAG_MAX = 0x80000000
        }

        // Define the scaling caps struct
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_scaling_caps_t
        {
            public uint Size;                                  // [in] size of this structure
            public byte Version;                               // [in] version of this structure
            public ctl_scaling_type_flag_t SupportedScaling;   // [out] Supported scaling types. Refer ::ctl_scaling_type_flag_t
        }

        // Define the delegate type for the done function pointer
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void doneDelegate(IntPtr thisobj);

        // Define the scaling settings struct
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_scaling_settings_t
        {
            public uint Size;                               // [in] size of this structure
            public byte Version;                            // [in] version of this structure
            [MarshalAs(UnmanagedType.U1)]
            public bool Enable;                             // [in,out] State of the scaler
            public ctl_scaling_type_flag_t ScalingType;    // [in,out] Requested scaling types
            public uint CustomScalingX;                     // [in,out] Custom Scaling X resolution
            public uint CustomScalingY;                     // [in,out] Custom Scaling Y resolution
            [MarshalAs(UnmanagedType.U1)]
            public bool HardwareModeSet;                    // [in] Flag to indicate hardware modeset should be done
        }

        [Flags]
        public enum ctl_sharpness_filter_type_flag_t : uint
        {
            CTL_SHARPNESS_FILTER_TYPE_FLAG_NON_ADAPTIVE = 1,   // Non-adaptive sharpness
            CTL_SHARPNESS_FILTER_TYPE_FLAG_ADAPTIVE = 2,   // Adaptive sharpness
            CTL_SHARPNESS_FILTER_TYPE_FLAG_MAX = 0x80000000
        }

        // Property range details, a generic struct to hold min/max/step size
        // information of various feature properties
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_property_range_info_t
        {
            public float min_possible_value;                       // [out] Minimum possible value
            public float max_possible_value;                       // [out] Maximum possible value
            public float step_size;                                // [out] Step size possible
            public float default_value;                            // [out] Default value
        }

        // Sharpness filter properties
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_sharpness_filter_properties_t
        {
            public ctl_sharpness_filter_type_flag_t FilterType;   // [out] Filter type. Refer ctl_sharpness_filter_type_flag_t
            public ctl_property_range_info_t FilterDetails;        // [out] Min, max & step size information
        }

        // Various sharpness filter types
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_sharpness_caps_t
        {
            public uint Size;                                  // [in] size of this structure
            public byte Version;                                // [in] version of this structure
            public ctl_sharpness_filter_type_flag_t SupportedFilterFlags; // [out] Supported sharpness filters for a given display output. Refer
                                                                          // ctl_sharpness_filter_type_flag_t
            public byte NumFilterTypes;                         // [out] Number of elements in filter properties array
            public IntPtr pFilterProperty; // [in,out] Array of filter properties structure describing supported
                                           // filter capabilities. Caller should provide a pre-allocated memory for
                                           // this.
        }

        // Current sharpness setting
        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_sharpness_settings_t
        {
            public uint Size;                                  // [in] size of this structure
            public byte Version;                                // [in] version of this structure
            [MarshalAs(UnmanagedType.U1)]
            public bool Enable;                                    // [in,out] Current or new state of sharpness setting
            public ctl_sharpness_filter_type_flag_t FilterType;   // [in,out] Current or new filter to be set. Refer
                                                                  // ctl_sharpness_filter_type_flag_t
            public float Intensity;                                // [in,out] Setting intensity to be applied
        }

        [Flags]
        public enum ctl_device_type_t : uint
        {
            CTL_DEVICE_TYPE_GRAPHICS = 1,                   // Graphics Device type
            CTL_DEVICE_TYPE_SYSTEM = 2,                     // System Device type
            CTL_DEVICE_TYPE_MAX = 0
        }

        [Flags]
        public enum ctl_supported_functions_flag_t : uint
        {
            CTL_SUPPORTED_FUNCTIONS_FLAG_DISPLAY = (1 << 0),  // [out] Is Display supported
            CTL_SUPPORTED_FUNCTIONS_FLAG_3D = (1 << 1),       // [out] Is 3D supported
            CTL_SUPPORTED_FUNCTIONS_FLAG_MEDIA = (1 << 2),    // [out] Is Media supported
            CTL_SUPPORTED_FUNCTIONS_FLAG_MAX = 0x80000000
        }

        public struct ctl_firmware_version_t
        {
            public ulong major_version;                         // [out] Major version
            public ulong minor_version;                         // [out] Minor version
            public ulong build_number;                          // [out] Build number
        }

        [Flags]
        public enum ctl_adapter_properties_flag_t : uint
        {
            CTL_ADAPTER_PROPERTIES_FLAG_INTEGRATED = (1 << 0),    // [out] Is Integrated Graphics adapter
            CTL_ADAPTER_PROPERTIES_FLAG_LDA_PRIMARY = (1 << 1),   // [out] Is Primary (Lead) adapter in a Linked Display Adapter (LDA) chain
            CTL_ADAPTER_PROPERTIES_FLAG_LDA_SECONDARY = (1 << 2), // [out] Is Secondary (Linked) adapter in a Linked Display Adapter (LDA) chain
            CTL_ADAPTER_PROPERTIES_FLAG_MAX = 0x80000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_adapter_bdf_t
        {
            public byte bus;                                    // [out] PCI Bus Number
            public byte device;                                 // [out] PCI device number
            public byte function;                               // [out] PCI function
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ctl_device_adapter_properties_t
        {
            public uint Size;                                  // [in] size of this structure
            public byte Version;                                // [in] version of this structure
            public IntPtr pDeviceID;                                // [in,out] OS specific Device ID
            public uint device_id_size;                        // [in] size of the device ID
            public ctl_device_type_t device_type;                  // [out] Device Type
            public ctl_supported_functions_flag_t supported_subfunction_flags; // [out] Supported functions
            public ulong driver_version;                        // [out] Driver version
            public ctl_firmware_version_t firmware_version;        // [out] Firmware version
            public uint pci_vendor_id;                         // [out] PCI Vendor ID
            public uint pci_device_id;                         // [out] PCI Device ID
            public uint rev_id;                                // [out] PCI Revision ID
            public uint num_eus_per_sub_slice;                 // [out] Number of EUs per sub-slice
            public uint num_sub_slices_per_slice;              // [out] Number of sub-slices per slice
            public uint num_slices;                            // [out] Number of slices
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 100)]
            public string name;             // [out] Device name
            public ctl_adapter_properties_flag_t graphics_adapter_properties; // [out] Graphics Adapter Properties
            public uint Frequency;                             // [out] Clock frequency for this device. Supported only for Version > 0
            public ushort pci_subsys_id;                         // [out] PCI SubSys ID, Supported only for Version > 1
            public ushort pci_subsys_vendor_id;                  // [out] PCI SubSys Vendor ID, Supported only for Version > 1
            public ctl_adapter_bdf_t adapter_bdf;                  // [out] Pci Bus, Device, Function. Supported only for Version > 1
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 112)]
            public string reserved;           // [out] Reserved
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ctl_telemetry_data
        {
            // GPU TDP
            public bool GpuEnergySupported;
            public double GpuEnergyValue;

            // GPU Voltage
            public bool GpuVoltageSupported;
            public double GpuVoltagValue; // Note: Typo in the original C++ code, should be "GpuVoltageValue" instead of "GpuVoltagValue"

            // GPU Core Frequency
            public bool GpuCurrentClockFrequencySupported;
            public double GpuCurrentClockFrequencyValue;

            // GPU Core Temperature
            public bool GpuCurrentTemperatureSupported;
            public double GpuCurrentTemperatureValue;

            // GPU Usage
            public bool GlobalActivitySupported;
            public double GlobalActivityValue;

            // Render Engine Usage
            public bool RenderComputeActivitySupported;
            public double RenderComputeActivityValue;

            // Media Engine Usage
            public bool MediaActivitySupported;
            public double MediaActivityValue;

            // VRAM Power Consumption
            public bool VramEnergySupported;
            public double VramEnergyValue;

            // VRAM Voltage
            public bool VramVoltageSupported;
            public double VramVoltageValue;

            // VRAM Frequency
            public bool VramCurrentClockFrequencySupported;
            public double VramCurrentClockFrequencyValue;

            // VRAM Read Bandwidth
            public bool VramReadBandwidthSupported;
            public double VramReadBandwidthValue;

            // VRAM Write Bandwidth
            public bool VramWriteBandwidthSupported;
            public double VramWriteBandwidthValue;

            // VRAM Temperature
            public bool VramCurrentTemperatureSupported;
            public double VramCurrentTemperatureValue;

            // Fanspeed (n Fans)
            public bool FanSpeedSupported;
            public double FanSpeedValue;
        }

        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        // Define the function delegates with the same signatures as the functions in the DLL
        private delegate ctl_result_t InitializeIgclDelegate();
        private delegate void CloseIgclDelegate();
        private delegate IntPtr EnumerateDevicesDelegate(ref uint pAdapterCount);
        private delegate ctl_result_t GetDevicePropertiesDelegate(ctl_device_adapter_handle_t hDevice, ref ctl_device_adapter_properties_t StDeviceAdapterProperties);
        private delegate ctl_result_t GetRetroScalingCapsDelegate(ctl_device_adapter_handle_t hDevice, ref ctl_retro_scaling_caps_t RetroScalingCaps);
        private delegate ctl_result_t GetRetroScalingSettingsDelegate(ctl_device_adapter_handle_t hDevice, ref ctl_retro_scaling_settings_t RetroScalingSettings);
        private delegate ctl_result_t SetRetroScalingSettingsDelegate(ctl_device_adapter_handle_t hDevice, ctl_retro_scaling_settings_t retroScalingSettings);
        private delegate ctl_result_t GetScalingCapsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ref ctl_scaling_caps_t ScalingCaps);
        private delegate ctl_result_t GetScalingSettingsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ref ctl_scaling_settings_t ScalingSetting);
        private delegate ctl_result_t SetScalingSettingsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ctl_scaling_settings_t scalingSettings);
        private delegate ctl_result_t GetSharpnessCapsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ref ctl_sharpness_caps_t SharpnessCaps);
        private delegate ctl_result_t GetSharpnessSettingsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ref ctl_sharpness_settings_t GetSharpness);
        private delegate ctl_result_t SetSharpnessSettingsDelegate(ctl_device_adapter_handle_t hDevice, uint idx, ctl_sharpness_settings_t SetSharpness);
        private delegate ctl_result_t GetTelemetryDataDelegate(ctl_device_adapter_handle_t hDevice, ref ctl_telemetry_data TelemetryData);

        // Define the function pointers
        private static InitializeIgclDelegate? InitializeIgcl;
        private static CloseIgclDelegate? CloseIgcl;
        private static EnumerateDevicesDelegate? EnumerateDevices;
        private static GetDevicePropertiesDelegate? GetDeviceProperties;
        private static GetRetroScalingCapsDelegate? GetRetroScalingCaps;
        private static GetRetroScalingSettingsDelegate? GetRetroScalingSettings;
        private static SetRetroScalingSettingsDelegate? SetRetroScalingSettings;
        private static GetScalingCapsDelegate? GetScalingCaps;
        private static GetScalingSettingsDelegate? GetScalingSettings;
        private static SetScalingSettingsDelegate? SetScalingSettings;
        private static GetSharpnessCapsDelegate? GetSharpnessCaps;
        private static GetSharpnessSettingsDelegate? GetSharpnessSettings;
        private static SetSharpnessSettingsDelegate? SetSharpnessSettings;
        private static GetTelemetryDataDelegate? GetTelemetryData;

        public static IntPtr[] devices = new IntPtr[1] { IntPtr.Zero };
        private static IntPtr pDll = IntPtr.Zero;

        // for this support library
        public enum IGCLStatus
        {
            NO_ERROR = 0,
            DLL_NOT_FOUND = 1,
            DLL_INCORRECT_VERSION = 2,
            DLL_INITIALIZE_ERROR = 3,
            DLL_INITIALIZE_SUCCESS = 4
        }

        private const string dllName = "IGCL_Wrapper.dll";
        private static IGCLStatus status = IGCLStatus.NO_ERROR;

        static IGCLBackend()
        {
            if (pDll == IntPtr.Zero)
            {
                pDll = LoadLibrary(dllName);
                if (pDll == IntPtr.Zero)
                {
                    status = IGCLStatus.DLL_NOT_FOUND;
                }

                if (status == IGCLStatus.NO_ERROR)
                {
                    try
                    {
                        // Get the function pointers
                        InitializeIgcl = (InitializeIgclDelegate)GetDelegate("IntializeIgcl", typeof(InitializeIgclDelegate));
                        CloseIgcl = (CloseIgclDelegate)GetDelegate("CloseIgcl", typeof(CloseIgclDelegate));
                        EnumerateDevices = (EnumerateDevicesDelegate)GetDelegate("EnumerateDevices", typeof(EnumerateDevicesDelegate));
                        GetDeviceProperties = (GetDevicePropertiesDelegate)GetDelegate("GetDeviceProperties", typeof(GetDevicePropertiesDelegate));
                        GetRetroScalingCaps = (GetRetroScalingCapsDelegate)GetDelegate("GetRetroScalingCaps", typeof(GetRetroScalingCapsDelegate));
                        GetRetroScalingSettings = (GetRetroScalingSettingsDelegate)GetDelegate("GetRetroScalingSettings", typeof(GetRetroScalingSettingsDelegate));
                        SetRetroScalingSettings = (SetRetroScalingSettingsDelegate)GetDelegate("SetRetroScalingSettings", typeof(SetRetroScalingSettingsDelegate));
                        GetScalingCaps = (GetScalingCapsDelegate)GetDelegate("GetScalingCaps", typeof(GetScalingCapsDelegate));
                        GetScalingSettings = (GetScalingSettingsDelegate)GetDelegate("GetScalingSettings", typeof(GetScalingSettingsDelegate));
                        SetScalingSettings = (SetScalingSettingsDelegate)GetDelegate("SetScalingSettings", typeof(SetScalingSettingsDelegate));
                        GetSharpnessCaps = (GetSharpnessCapsDelegate)GetDelegate("GetSharpnessCaps", typeof(GetSharpnessCapsDelegate));
                        GetSharpnessSettings = (GetSharpnessSettingsDelegate)GetDelegate("GetSharpnessSettings", typeof(GetSharpnessSettingsDelegate));
                        SetSharpnessSettings = (SetSharpnessSettingsDelegate)GetDelegate("SetSharpnessSettings", typeof(SetSharpnessSettingsDelegate));
                        GetTelemetryData = (GetTelemetryDataDelegate)GetDelegate("GetTelemetryData", typeof(GetTelemetryDataDelegate));

                        status = IGCLStatus.DLL_INITIALIZE_SUCCESS;
                    }
                    catch
                    {
                        status = IGCLStatus.DLL_INITIALIZE_ERROR;

                        InitializeIgcl = null;
                        CloseIgcl = null;
                        EnumerateDevices = null;
                        GetDeviceProperties = null;
                        GetRetroScalingCaps = null;
                        GetRetroScalingSettings = null;
                        SetRetroScalingSettings = null;
                        GetScalingCaps = null;
                        GetScalingSettings = null;
                        SetScalingSettings = null;
                        GetSharpnessCaps = null;
                        GetSharpnessSettings = null;
                        SetSharpnessSettings = null;
                        GetTelemetryData = null;
                    }
                }
            }
        }

        private static Delegate GetDelegate(string procName, Type delegateType)
        {
            IntPtr ptr = GetProcAddress(pDll, procName);
            if (ptr != IntPtr.Zero)
            {
                var d = Marshal.GetDelegateForFunctionPointer(ptr, delegateType);
                return d;
            }

            var result = Marshal.GetHRForLastWin32Error();
            throw Marshal.GetExceptionForHR(result);
        }

        public static bool Initialize()
        {
            if (status == IGCLStatus.DLL_INITIALIZE_SUCCESS)
            {
                ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;

                // Call Init and check the result
                Result = InitializeIgcl();
                return Result == ctl_result_t.CTL_RESULT_SUCCESS;
            }

            return false;
        }

        public static void Terminate()
        {
            if (status == IGCLStatus.DLL_INITIALIZE_SUCCESS)
            {
                CloseIgcl();
            }
        }

        public static int GetDeviceIdx(string deviceName)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            uint adapterCount = 0;

            // Get the number of Intel devices
            IntPtr hDevices = EnumerateDevices(ref adapterCount);
            if (hDevices == IntPtr.Zero)
                return -1;

            // Convert the device handles to an array of IntPtr
            devices = new IntPtr[adapterCount];
            Marshal.Copy(hDevices, devices, 0, (int)adapterCount);
            if (devices.Length == 0)
                return -1;

            for (int idx = 0; idx < devices.Length; idx++)
            {
                ctl_device_adapter_properties_t StDeviceAdapterProperties = new();
                ctl_device_adapter_handle_t hDevice = new()
                {
                    handle = devices[idx]
                };

                Result = GetDeviceProperties(hDevice, ref StDeviceAdapterProperties);
                if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                    continue;

                if (deviceName.Equals(StDeviceAdapterProperties.name) || adapterCount == 1)
                    return idx;
            }

            return -1;
        }

        internal static bool HasGPUScalingSupport(nint deviceIdx, uint displayIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_scaling_caps_t ScalingCaps = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetScalingCaps(hDevice, displayIdx, ref ScalingCaps);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return ScalingCaps.SupportedScaling >= 0;
        }

        internal static bool GetGPUScaling(nint deviceIdx, uint displayIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_scaling_settings_t ScalingSettings = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetScalingSettings(hDevice, displayIdx, ref ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return ScalingSettings.Enable;
        }

        internal static bool SetGPUScaling(nint deviceIdx, uint displayIdx, bool enabled = true)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_scaling_settings_t ScalingSettings = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetScalingSettings(hDevice, displayIdx, ref ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // skip if not needeed
            if (ScalingSettings.Enable == enabled)
                return true;

            // fill custom scaling details
            ScalingSettings.Enable = enabled;

            Result = SetScalingSettings(hDevice, displayIdx, ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // check if value was properly applied
            Result = GetScalingSettings(hDevice, displayIdx, ref ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return ScalingSettings.Enable == enabled;
        }

        internal static bool SetImageSharpening(nint deviceIdx, uint displayIdx, bool enable)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_sharpness_settings_t GetSharpness = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // skip if not needeed
            if (GetSharpness.Enable == enable)
                return true;

            // if disabled, we need to set intensity to 0 first (while enabled)
            switch (enable)
            {
                default:
                case false:
                    GetSharpness.Intensity = 0;
                    Result = SetSharpnessSettings(hDevice, displayIdx, GetSharpness);
                    if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                        return false;
                    break;
            }

            // fill custom scaling details
            GetSharpness.Enable = enable;

            Result = SetSharpnessSettings(hDevice, displayIdx, GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // check if value was properly applied
            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return GetSharpness.Enable == enable;
        }

        internal static bool SetScalingMode(nint deviceIdx, uint displayIdx, int mode)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_scaling_settings_t ScalingSettings = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetScalingSettings(hDevice, displayIdx, ref ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // 0: aspect
            // 1: full
            // 2: center
            ctl_scaling_type_flag_t ScalingType = ctl_scaling_type_flag_t.CTL_SCALING_TYPE_FLAG_IDENTITY;
            switch (mode)
            {
                case 0:
                    ScalingType = ctl_scaling_type_flag_t.CTL_SCALING_TYPE_FLAG_ASPECT_RATIO_CENTERED_MAX;
                    break;
                case 1:
                    ScalingType = ctl_scaling_type_flag_t.CTL_SCALING_TYPE_FLAG_STRETCHED;
                    break;
                case 2:
                    ScalingType = ctl_scaling_type_flag_t.CTL_SCALING_TYPE_FLAG_CENTERED;
                    break;
            }

            // skip if not needeed
            if (ScalingSettings.ScalingType == ScalingType)
                return true;

            // fill custom scaling details
            ScalingSettings.ScalingType = ScalingType;

            Result = SetScalingSettings(hDevice, displayIdx, ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // check if value was properly applied
            Result = GetScalingSettings(hDevice, displayIdx, ref ScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return ScalingSettings.ScalingType == ScalingType;
        }

        internal static bool GetImageSharpening(nint deviceIdx, uint displayIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_sharpness_settings_t GetSharpness = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return GetSharpness.Enable;
        }

        internal static int GetImageSharpeningSharpness(nint deviceIdx, uint displayIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return 0;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            ctl_sharpness_settings_t GetSharpness = new();

            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return 0;

            return (int)GetSharpness.Intensity;
        }

        internal static bool SetImageSharpeningSharpness(nint deviceIdx, uint displayIdx, int sharpness)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_sharpness_settings_t GetSharpness = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // skip if not needeed
            if (GetSharpness.Intensity == sharpness)
                return true;

            // fill custom scaling details
            GetSharpness.Intensity = sharpness;

            Result = SetSharpnessSettings(hDevice, displayIdx, GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // check if value was properly applied
            Result = GetSharpnessSettings(hDevice, displayIdx, ref GetSharpness);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return GetSharpness.Intensity == sharpness;
        }

        internal static bool HasIntegerScalingSupport(nint deviceIdx, uint displayIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_retro_scaling_caps_t RetroScalingCaps = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetRetroScalingCaps(hDevice, ref RetroScalingCaps);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return RetroScalingCaps.SupportedRetroScaling >= 0;
        }

        internal static bool GetIntegerScaling(nint deviceIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_retro_scaling_settings_t RetroScalingSettings = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetRetroScalingSettings(hDevice, ref RetroScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return RetroScalingSettings.Enable;
        }

        internal static bool SetIntegerScaling(nint deviceIdx, bool enabled, byte type)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_retro_scaling_settings_t RetroScalingSettings = new();
            ctl_retro_scaling_type_flags_t RetroScalingType = (ctl_retro_scaling_type_flags_t)(type + 1);

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return false;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetRetroScalingSettings(hDevice, ref RetroScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // skip if not needeed
            if (RetroScalingSettings.Enable == enabled && RetroScalingSettings.RetroScalingType == RetroScalingType)
                return true;

            // fill custom scaling details
            RetroScalingSettings.Enable = enabled;
            RetroScalingSettings.RetroScalingType = RetroScalingType;

            Result = SetRetroScalingSettings(hDevice, RetroScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            // check if value was properly applied
            Result = GetRetroScalingSettings(hDevice, ref RetroScalingSettings);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return false;

            return RetroScalingSettings.Enable == enabled;
        }

        public static ctl_telemetry_data GetTelemetry(nint deviceIdx)
        {
            ctl_result_t Result = ctl_result_t.CTL_RESULT_SUCCESS;
            ctl_telemetry_data TelemetryData = new();

            IntPtr device = devices[deviceIdx];
            if (device == IntPtr.Zero)
                return TelemetryData;

            ctl_device_adapter_handle_t hDevice = new()
            {
                handle = device
            };

            Result = GetTelemetryData(hDevice, ref TelemetryData);
            if (Result != ctl_result_t.CTL_RESULT_SUCCESS)
                return TelemetryData;

            return TelemetryData;
        }
    }
}