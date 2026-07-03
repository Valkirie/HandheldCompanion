using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices.AYANEO;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Devices.MSI;
using HandheldCompanion.Devices.OneXPlayer;
using HandheldCompanion.Devices.Zotac;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Models;
using HandheldCompanion.Sensors;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Windows.Devices.Sensors;
using WindowsInput.Events;
using static HandheldCompanion.Devices.IDevice;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices;

[Flags]
public enum DeviceCapabilities : ushort
{
    None = 0,
    InternalSensor = 1,
    ExternalSensor = 2,
    FanControl = 4,
    DynamicLighting = 8,
    DynamicLightingBrightness = 16,
    DynamicLightingSecondLEDColor = 32,
    BatteryChargeLimit = 64,
    BatteryChargeLimitPercent = 128,
    BatteryBypassCharging = 256,
    FanOverride = 512,
    OEMCPU = 1024,
    OEMGPU = 2048,
}

public enum TDPMethod
{
    Default = 0,
    OEM = 1
}

public struct ECDetails
{
    public ushort AddressStatusCommandPort; // Address of the register, In EC communication, the registry address specifies the type of data or command you want to access.
    public ushort AddressDataPort;          // Address where the data needs to go to, When interacting with the EC, the data address is where you send or receive the actual data or commands you want to communicate with the EC.
    public ushort AddressFanControl;
    public ushort AddressFanDuty;
    public short FanValueMin;
    public short FanValueMax;
}

public struct HidFilter
{
    public short UsagePage;
    public short Usage;
    public HidFilter(short usagePage, short usage)
    {
        UsagePage = usagePage;
        Usage = usage;
    }
}

public struct IMUMatrix
{
    public Vector3 Axis;
    public SortedDictionary<char, char> AxisSwap;

    // Pre-computed indices for fast axis remapping: AxisRemapIndices[input] = output_axis_index
    // where 0=X, 1=Y, 2=Z. Eliminates dictionary lookups in hot sensor paths.
    private int[]? _axisRemapIndices;
    public int[] AxisRemapIndices
    {
        get
        {
            if (_axisRemapIndices == null)
                ComputeRemapIndices();
            return _axisRemapIndices!;
        }
    }

    public IMUMatrix()
    {
        Axis = new Vector3(1.0f, 1.0f, 1.0f);
        AxisSwap = new()
        {
            { 'X', 'X' },
            { 'Y', 'Y' },
            { 'Z', 'Z' }
        };
        _axisRemapIndices = new[] { 0, 1, 2 }; // X?0, Y?1, Z?2 (identity mapping)
    }

    /// <summary>
    /// Computes the AxisRemapIndices from the AxisSwap dictionary.
    /// Maps each input axis position (X=0, Y=1, Z=2) to its remapped output axis.
    /// </summary>
    private void ComputeRemapIndices()
    {
        _axisRemapIndices = new int[3];
        // Map input positions: 0=X, 1=Y, 2=Z
        _axisRemapIndices[0] = AxisSwap['X'] switch { 'Y' => 1, 'Z' => 2, _ => 0 }; // X input ? output axis
        _axisRemapIndices[1] = AxisSwap['Y'] switch { 'X' => 0, 'Z' => 2, _ => 1 }; // Y input ? output axis
        _axisRemapIndices[2] = AxisSwap['Z'] switch { 'X' => 0, 'Y' => 1, _ => 2 }; // Z input ? output axis
    }
}

public abstract class IDevice
{
    public delegate void KeyPressedEventHandler(IDevice sender, ButtonFlags button);
    public event KeyPressedEventHandler? KeyPressed;
    public delegate void KeyReleasedEventHandler(IDevice sender, ButtonFlags button);
    public event KeyReleasedEventHandler? KeyReleased;
    public delegate void OpenedEventHandler(IDevice sender);
    public event OpenedEventHandler? Opened;
    public delegate void ClosedEventHandler(IDevice sender);
    public event ClosedEventHandler? Closed;

    public delegate void CapabilitiesChangedEventHandler(IDevice sender, DeviceCapabilities capabilities);
    public event CapabilitiesChangedEventHandler? CapabilitiesChanged;

    public static readonly Guid BetterBatteryGuid = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
    public static readonly Guid BetterPerformanceGuid = new Guid("3af9B8d9-7c97-431d-ad78-34a8bfea439f");
    public static readonly Guid BestPerformanceGuid = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

    protected static OpenLibSys? openLibSys;
    protected object updateLock = new();

    private static IDevice? device;

    protected int vendorId;
    protected int[] productIds = [];
    protected Dictionary<int, HidDevice> hidDevices = [];
    protected Dictionary<int, HidFilter> hidFilters = [];

    public IMUMatrix AcceleroMatrix;
    public IMUMatrix GyroMatrix;

    public GamepadMotion GamepadMotion;

    public DeviceCapabilities Capabilities = DeviceCapabilities.None;
    public LEDLevel DynamicLightingCapabilities = LEDLevel.None;
    public List<LEDPreset> LEDPresets { get; protected set; } = [];
    public List<BatteryBypassPreset> BatteryBypassPresets { get; protected set; } = [];

    public int BatteryBypassMin = 50;   // Arbitrary
    public int BatteryBypassMax = 100;  // Shouldn't it be 90% ?
    public int BatteryBypassStep = 10;

    protected const byte EC_OBF = 0x01;  // Output Buffer Full
    protected const byte EC_IBF = 0x02;  // Input Buffer Full
    protected const byte EC_DATA = 0x62; // Data Port
    protected const byte EC_SC = 0x66;   // Status/Command Port
    protected const byte RD_EC = 0x80;   // Read Embedded Controller
    protected const byte WR_EC = 0x81;   // Write Embedded Controller

    // device configurable TDP (down, up)
    public double[] cTDP = { 10, 25 };

    // device GfxClock frequency limits
    public double[] GfxClock = { 100, 1800 };
    public uint CpuClock = 6000;

    // device nominal TDP (slow, slow, fast)
    public double[] nTDP = { 15, 15, 20 };

    // device maximum operating temperature
    public double Tjmax = 95;

    // power profile(s)
    public List<PowerProfile> DevicePowerProfiles = [];

    public List<double[]> fanPresets = new()
    {
        { new double[] { 20, 20, 20, 20, 20, 25, 30, 40, 70, 70,  100 } },  // Quiet
        { new double[] { 20, 20, 20, 30, 40, 50, 70, 80, 90, 100, 100 } },  // Default
        { new double[] { 40, 40, 40, 40, 40, 50, 70, 80, 90, 100, 100 } },  // Aggressive
    };

    // trigger specific settings
    public List<KeyboardChord> OEMChords = [];

    // UI
    protected FontFamily GlyphFontFamily = new("PromptFont");
    protected const string defaultGlyph = "\u2753";

    protected bool UseOpenLib = false;
    public ECDetails ECDetails;

    public string ExternalSensorName = string.Empty;
    public string InternalSensorName = string.Empty;

    public string ProductIllustration = "device_generic";
    public string ProductModel = "default";

    // key press delay to use for certain scenarios
    public short KeyPressDelay = 200;

    // LibreHardwareMonitor
    public bool CpuMonitor = true;
    public bool GpuMonitor = false;
    public bool MemoryMonitor = true;
    public bool BatteryMonitor = false;

    protected bool DeviceOpen = false;
    public virtual bool IsOpen => DeviceOpen;

    [DllImport("Kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    protected static extern bool GetPhysicallyInstalledSystemMemory(out ulong TotalMemoryInKilobytes);
    protected uint physicalInstalledRamGB = 16;

    public Dictionary<Type, Hotkey> DeviceHotkeys = new();

    public IDevice()
    {
        GamepadMotion = new(ProductIllustration, CalibrationMode.Manual  /*| CalibrationMode.SensorFusion */);

        // initialize IMU matrices with default values
        AcceleroMatrix = new();
        GyroMatrix = new();

        // add default power profile
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileDefaultName, Properties.Resources.PowerProfileDefaultDescription)
        {
            Default = true,
            Guid = Guid.Empty,
            OSPowerMode = OSPowerMode.BetterPerformance,
            TDPOverrideValues = new double[] { this.nTDP[0], this.nTDP[1], this.nTDP[2] }
        });

        // default hotkeys
        DeviceHotkeys[typeof(DesktopLayoutCommands)] = new Hotkey() { command = new DesktopLayoutCommands(), IsPinned = true, ButtonFlags = ButtonFlags.HOTKEY_RESERVED0 };
        DeviceHotkeys[typeof(QuickToolsCommands)] = new Hotkey() { command = new QuickToolsCommands(), IsPinned = true, ButtonFlags = ButtonFlags.HOTKEY_RESERVED1 };
        DeviceHotkeys[typeof(MainWindowCommands)] = new Hotkey() { command = new MainWindowCommands(), IsPinned = true, ButtonFlags = ButtonFlags.HOTKEY_RESERVED2 };
        DeviceHotkeys[typeof(OnScreenKeyboardCommands)] = new Hotkey() { command = new OnScreenKeyboardCommands(), IsPinned = true, ButtonFlags = ButtonFlags.HOTKEY_RESERVED3 };

        // prepare hotkeys
        DeviceHotkeys[typeof(DesktopLayoutCommands)].inputsChord.ButtonState[ButtonFlags.LeftStickClick] = true;
        DeviceHotkeys[typeof(DesktopLayoutCommands)].inputsChord.ButtonState[ButtonFlags.RightStickClick] = true;
    }

    public virtual bool Open()
    {
        if (IsOpen)
            return true;

        LogManager.LogInformation("OpenLibSys initialization: {0}", UseOpenLib);
        if (UseOpenLib)
        {
            bool success = OpenLibSys();
            if (!success)
            {
                LogManager.LogError("Failed to initialize OpenLibSys");
                return false;
            }
        }

        // set flag
        DeviceOpen = true;

        return DeviceOpen;
    }

    private bool OpenLibSys()
    {
        if (openLibSys != null) return true;

        try
        {
            // initialize OpenLibSys
            openLibSys = new OpenLibSys();
            return openLibSys.InitializeOls();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't initialize OpenLibSys. ErrorCode: {0}", ex.Message);
            Close();
            return false;
        }
    }

    public virtual void OpenEvents()
    {
        // raise opened event
        Opened?.Invoke(this);

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        switch (ManagerFactory.deviceManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.deviceManager.Initialized += DeviceManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryDevices();
                break;
        }

        switch (ManagerFactory.powerProfileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPowerProfile();
                break;
        }

        // manage events
        ControllerManager.Initialized += ControllerManager_Initialized;

        // raise events
        if (ControllerManager.IsInitialized)
            ControllerManager_Initialized();
    }

    private void ControllerManager_Initialized()
    {
        // manage events
        ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

        // raise events
        Device_Inserted();
    }

    private void QueryDevices()
    {
        // manage events
        ManagerFactory.deviceManager.UsbDeviceArrived += GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved += GenericDeviceUpdated;

        // raise events
        GenericDeviceUpdated(null, Guid.Empty);
    }

    private void DeviceManager_Initialized()
    {
        QueryDevices();
    }

    protected virtual void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
    }

    protected virtual void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    { }

    protected virtual void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    protected virtual void QueryPowerProfile()
    {
        // manage events
        // handled by PowerProfileManager, because of complex power profile order logic
        // ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

        // apply current profile once the device is open and ready
        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    protected virtual void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    public virtual void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        // apply profile Fan mode
        switch (profile.FanProfile.fanMode)
        {
            default:
            case FanMode.Hardware:
                SetFanControl(false, profile.OEMPowerMode);
                break;
            case FanMode.Software:
                SetFanControl(true, profile.OEMPowerMode);
                break;
        }
    }

    public virtual void Close()
    {
        // disable fan control
        SetFanControl(false);

        // Close openLib
        openLibSys?.Dispose();
        openLibSys = null;

        // set flag
        DeviceOpen = false;

        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
        // handled by PowerProfileManager, because of complex power profile order logic
        // ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;

        ControllerManager.Initialized -= ControllerManager_Initialized;
        ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        ManagerFactory.deviceManager.Initialized -= DeviceManager_Initialized;
        ManagerFactory.deviceManager.UsbDeviceArrived -= GenericDeviceUpdated;
        ManagerFactory.deviceManager.UsbDeviceRemoved -= GenericDeviceUpdated;

        Closed?.Invoke(this);
    }

    public virtual void Initialize(bool FirstStart, bool NewUpdate)
    { }

    protected virtual void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool WasPowerCycling)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Inserted(true);
    }

    protected virtual void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
    {
        if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
            Device_Removed();
    }

    protected virtual void Device_Inserted(bool reScan = false)
    { }

    protected virtual void Device_Removed()
    { }

    private void GenericDeviceUpdated(PnPDevice? device, Guid IntefaceGuid)
    {
        // todo: improve me
        PullSensors();
    }

    public IEnumerable<ButtonFlags> OEMButtons => OEMChords.Where(chord => !chord.silenced).SelectMany(chord => chord.state.Buttons).Distinct();

    public virtual bool IsSupported => true;

    public Layout DefaultLayout { get; set; } = LayoutTemplate.DefaultLayout.Layout;

    public string ManufacturerName = string.Empty;
    public string ProductName = string.Empty;
    public string SystemName = string.Empty;
    public string SystemModel = string.Empty;
    public string Version = string.Empty;
    public string Processor = string.Empty;
    public int NumberOfCores = 0;
    public string DeviceType = "Handheld";

    public static IDevice GetCurrent()
    {
        if (device is not null)
            return device;

        var ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
        var ProductName = MotherboardInfo.Product;
        var SystemName = MotherboardInfo.SystemName;
        var SystemModel = MotherboardInfo.SystemModel;
        var Version = MotherboardInfo.Version;
        var Processor = MotherboardInfo.ProcessorName;
        var NumberOfCores = MotherboardInfo.NumberOfCores;

        switch (ManufacturerName)
        {
            case "SHENZHEN MEIGAO ELECTRONIC EQUIPMENT CO.,LTD":
                {
                    switch (ProductName)
                    {
                        case "HPPAC":
                            device = new MinisforumV3();
                            break;
                    }
                }
                break;

            case "AYN":
                {
                    switch (ProductName)
                    {
                        case "Loki MiniPro":
                            device = new LokiMiniPro();
                            break;
                        case "Loki Zero":
                            device = new LokiZero();
                            break;
                        case "Loki Max":
                            switch (Processor)
                            {
                                case "AMD Ryzen 5 6600U with Radeon Graphics":
                                    device = new LokiMax6600U();
                                    break;
                                case "AMD Ryzen 7 6800U with Radeon Graphics":
                                    device = new LokiMax6800U();
                                    break;
                            }
                            break;
                    }
                }
                break;

            case "AOKZOE":
                {
                    switch (ProductName)
                    {
                        case "AOKZOE A1 AR07":
                            device = new AOKZOEA1();
                            break;
                        case "AOKZOE A1 Pro":
                            device = new AOKZOEA1Pro();
                            break;
                        case "AOKZOE A1X":
                            device = new AOKZOEA1X();
                            break;
                        case "AOKZOE A2 Pro":
                            device = new AOKZOEA2();
                            break;
                    }
                }
                break;

            case "AYADEVICE":
            case "AYANEO":
                {
                    switch (ProductName)
                    {
                        case "AIR":
                            device = new AYANEOAIR();
                            break;
                        case "AIR Pro":
                            device = new AYANEOAIRPro();
                            break;
                        case "AIR 1S":
                        case "AIR 1S Limited":
                            device = new AYANEOAIR1S();
                            break;
                        case "AIR Lite":
                            device = new AYANEOAIRLite();
                            break;
                        case "AYA NEO FOUNDER":
                        case "AYANEO 2021":
                            device = new AYANEO2021();
                            break;
                        case "AYANEO 2021 Pro":
                        case "AYANEO 2021 Pro Retro Power":
                            device = new AYANEO2021Pro();
                            break;
                        case "KUN":
                            device = new AYANEOKUN();
                            break;
                        case "AS01":
                            device = new AYANEOSlide();
                            break;
                        case "NEXT Pro":
                        case "NEXT Advance":
                        case "NEXT":
                            device = new AYANEONEXT();
                            break;
                        case "NEXT Lite":
                            device = Processor.Contains("4500U")
                                ? new AYANEONEXTLite4500U()
                                : new AYANEONEXTLite();
                            break;
                        case "AYANEO 2":
                        case "GEEK":
                            device = new AYANEO2();
                            break;
                        case "AB05-AMD":
                            device = new AYANEOAIRPlusAMD();
                            break;
                        case "AB05-Mendocino":
                            device = new AYANEOAIRPlusAMDMendocino();
                            break;
                        case "AB05-Intel":
                            device = new AYANEOAIRPlusIntel();
                            break;
                        case "AYANEO 2S":
                        case "GEEK 1S":
                            device = new AYANEO2S();
                            break;
                        case "FLIP KB":
                            device = new AYANEOFlipKB();
                            break;
                        case "FLIP DS":
                            device = new AYANEOFlipDS();
                            break;
                        case "FLIP 1S DS":
                            device = new AYANEOFlip1SDS();
                            break;
                        case "FLIP 1S KB":
                            device = new AYANEOFlip1SKB();
                            break;
                    }
                }
                break;

            case "MYSTEN LABS, INC.":
                {
                    switch (ProductName)
                    {
                        case "SuiPlay0X1":
                            device = new SuiPlay0X1();
                            break;
                    }
                }
                break;

            case "GPD":
                {
                    switch (ProductName)
                    {
                        case "WIN2":
                            device = new GPDWin2();
                            break;
                        case "G1617-01":
                            switch (Processor)
                            {
                                case "AMD Ryzen 5 7640U w/ Radeon 760M Graphics":
                                    device = new GPDWinMini_7640U();
                                    break;
                                case "AMD Ryzen 7 7840U w/ Radeon 780M Graphics":
                                    device = new GPDWinMini();
                                    break;
                                case "AMD Ryzen 7 8840U w/ Radeon 780M Graphics":
                                    device = new GPDWinMini_8840U();
                                    break;
                            }
                            break;
                        case "G1617-02":
                            device = new GPDWinMini_HX370();
                            break;
                        case "G1618-03":
                            device = new GPDWin3();
                            break;
                        case "G1618-04":
                            switch (Processor)
                            {
                                case "AMD Ryzen 7 6800U with Radeon Graphics":
                                    device = new GPDWin4();
                                    break;
                                case "AMD Ryzen 5 7640U w/ Radeon 760M Graphics":
                                    device = new GPDWin4_2023_7640U();
                                    break;
                                case "AMD Ryzen 7 7840U w/ Radeon 780M Graphics":
                                    device = new GPDWin4_2023();
                                    break;
                                case "AMD Ryzen 5 8640U w/ Radeon 760M Graphics":
                                    device = new GPDWin4_2024_8640U();
                                    break;
                                case "AMD Ryzen 7 8840U w/ Radeon 780M Graphics":
                                    device = new GPDWin4_2024();
                                    break;
                                case "AMD Ryzen AI 9 HX 370 w/ Radeon 890M":
                                    device = new GPDWin4_2024_HX370();
                                    break;
                            }
                            break;
                        case "G1618-05":
                            device = new GPDWin5();
                            break;
                        case "G1619-03":
                            device = new GPDWinMax2Intel();
                            break;
                        case "G1619-04":
                        case "G1619-05":
                            switch (Processor)
                            {
                                case "AMD Ryzen 7 6800U with Radeon Graphics":
                                    device = new GPDWinMax2_2022_6800U();
                                    break;
                                case "AMD Ryzen 5 7640U w/ Radeon 760M Graphics":
                                    device = new GPDWinMax2_2023_7640U();
                                    break;
                                case "AMD Ryzen 7 7840U w/ Radeon 780M Graphics":
                                    device = new GPDWinMax2_2023_7840U();
                                    break;
                                case "AMD Ryzen 5 8640U w/ Radeon 760M Graphics":
                                    device = new GPDWinMax2_2024_8640U();
                                    break;
                                default:
                                case "AMD Ryzen 7 8840U w/ Radeon 780M Graphics":
                                    device = new GPDWinMax2_2024_8840U();
                                    break;
                                case "AMD Ryzen AI 9 HX 370 w/ Radeon 890M":
                                    device = new GPDWinMax2_2024_HX370();
                                    break;
                            }
                            break;
                    }
                }
                break;

            case "ONE-NETBOOK TECHNOLOGY CO., LTD.":
            case "ONE-NETBOOK":
                {
                    switch (ProductName)
                    {
                        case "ONEXPLAYER X1 i":
                            device = new OneXPlayerX1Intel();
                            break;
                        case "ONEXPLAYER X1 A":
                        case "ONEXPLAYER X1z":
                            device = new OneXPlayerX1AMD();
                            break;
                        case "ONEXPLAYER X1 mini":
                            device = new OneXPlayerX1Mini();
                            break;
                        case "ONEXPLAYER X1Pro":
                            device = new OneXPlayerX1Pro();
                            break;
                        case "ONEXPLAYER APEX":
                        case "ONEXPLAYERAPEX":
                            device = new OneXPlayerApex();
                            break;
                        case "ONEXPLAYER G1 i":
                            device = new OneXPlayerG1Intel();
                            break;
                        case "ONEXPLAYER G1 A":
                            device = new OneXPlayerG1AMD();
                            break;
                        case "ONEXPLAYER F1":
                            {
                                switch (Version)
                                {
                                    default:
                                    case "Default string":
                                        device = new OneXPlayerOneXFly();
                                        break;
                                }
                                break;
                            }
                        case "ONEXPLAYER F1Pro":
                            {
                                switch (Version)
                                {
                                    default:
                                    case "Default string":
                                        device = new OneXPlayerOneXFlyF1Pro();
                                        break;
                                }
                                break;
                            }
                        case "ONE XPLAYER":
                        case "ONEXPLAYER Mini Pro":
                            {
                                switch (Version)
                                {
                                    default:
                                    case "V01":
                                        device = new OneXPlayerMiniAMD();
                                        break;
                                    case "1002-C":
                                        device = new OneXPlayerMiniIntel();
                                        break;
                                    case "V03":
                                        device = new OneXPlayerMiniPro();
                                        break;
                                }
                                break;
                            }
                        case "ONEXPLAYER mini A07":
                            device = new OneXPlayerMiniAMD();
                            break;
                        case "ONEXPLAYER 2 ARP23":
                            {
                                switch (Version)
                                {
                                    default:
                                    case "Ver.1.0":
                                        device = new OneXPlayer2();
                                        break;
                                }
                                break;
                            }
                        case "ONEXPLAYER 2 PRO ARP23P":
                        case "ONEXPLAYER 2 PRO ARP23P EVA-01":
                            switch (Version)
                            {
                                default:
                                case "Version 1.0":
                                    device = new OneXPlayer2Pro();
                                    break;
                            }
                            break;
                    }
                }
                break;

            case "ASUSTEK COMPUTER INC.":
                {
                    switch (ProductName)
                    {
                        case "RC71L":
                            device = new ROGAlly();
                            break;
                        case "RC72LA":
                            device = new ROGAllyX();
                            break;
                        case "RC73YA":
                            device = new XboxROGAlly();
                            break;
                        case "RC73XA":
                            device = new XboxROGAllyX();
                            break;
                    }
                }
                break;

            case "VALVE":
                {
                    switch (ProductName)
                    {
                        case "Jupiter":
                        case "Galileo":
                            device = new SteamDeck();
                            break;
                    }
                }
                break;

            case "LENOVO":
                {
                    switch (SystemModel)
                    {
                        case "83E1":    // Legion Go
                            device = new LegionGoTablet();
                            break;
                        case "83N0":    // Legion Go 2
                        case "83N1":
                            device = new LegionGoTablet2();
                            break;
                        case "83L3":    // Legion Go S Z2 Go
                            device = new LegionGoSZ2();
                            break;
                        case "83N6":    // Legion Go S Z1E
                        case "83Q2":
                        case "83Q3":
                            device = new LegionGoSZ1();
                            break;
                    }
                }
                break;

            case "MICRO-STAR INTERNATIONAL CO., LTD.":
                {
                    switch (ProductName)
                    {
                        case "MS-1T41":
                            device = new ClawA1M();
                            break;
                        case "MS-1T42": // Claw 7 AI+ A2VM
                        case "MS-1T52": // Claw 8 AI+ A2VM
                            device = new ClawA2VM();
                            break;
                        case "MS-1T8K": // Claw A8
                            device = new ClawBZ2EM();
                            break;
                        case "MS-1T91": // Claw 8 EX AI+ CG3EM
                            device = new ClawCG3EM();
                            break;
                    }
                }
                break;

            case "PC PARTNER LIMITED":
            case "ZOTAC":
                {
                    switch (ProductName)
                    {
                        case "G0A1W":
                            device = new GamingZone();
                            break;
                    }
                }
                break;
        }

        // update sentry device context
        SentrySdk.ConfigureScope(scope =>
        {
            scope.Contexts.Device.Model = SystemModel;
            scope.Contexts.Device.Manufacturer = ManufacturerName;
            scope.Contexts.Device.Name = ProductName;
            scope.Contexts.Device.CpuDescription = Processor;
            scope.Contexts.Device.DeviceType = device is not null ? device.DeviceType : "Desktop";
        });

        LogManager.LogInformation("{0} from {1}", ProductName, ManufacturerName);

        if (device is null)
        {
            device = new DefaultDevice();
            LogManager.LogWarning("Device not yet supported. The behavior of the application will be unpredictable");
        }

        // update device details
        device.ManufacturerName = ManufacturerName;
        device.ProductName = ProductName;
        device.SystemName = SystemName;
        device.SystemModel = SystemModel;
        device.Version = Version;
        device.Processor = Processor;
        device.NumberOfCores = NumberOfCores;

        return device;
    }

    public bool HasMotionSensor()
    {
        return Capabilities.HasFlag(DeviceCapabilities.InternalSensor) || Capabilities.HasFlag(DeviceCapabilities.ExternalSensor);
    }

    public virtual bool IsReady()
    {
        return true;
    }

    protected async Task WaitUntilReady()
    {
        while (!IsReady())
            await Task.Delay(250).ConfigureAwait(false);
    }

    public void PullSensors()
    {
        Gyrometer gyrometer = Gyrometer.GetDefault();
        Accelerometer accelerometer = Accelerometer.GetDefault();

        if (gyrometer != null || accelerometer != null)
        {
            // pick the non-null sensor's DeviceId
            string rawId = (gyrometer != null) ? gyrometer.DeviceId : accelerometer.DeviceId;
            string deviceId = CommonUtils.Between(rawId, @"\\?\", @"#{")?.Replace("#", @"\") ?? rawId;

            USBDeviceInfo? sensorInfo = GetUSBDevice(deviceId);
            if (sensorInfo != null)
                InternalSensorName = sensorInfo.Name;

            Capabilities |= DeviceCapabilities.InternalSensor;
        }
        else if (Capabilities.HasFlag(DeviceCapabilities.InternalSensor))
        {
            Capabilities &= ~DeviceCapabilities.InternalSensor;
        }

        SerialUSBIMU? USB = SerialUSBIMU.GetCurrent();
        if (USB is not null)
        {
            ExternalSensorName = USB.GetName();

            Capabilities |= DeviceCapabilities.ExternalSensor;
        }
        else if (Capabilities.HasFlag(DeviceCapabilities.ExternalSensor))
        {
            Capabilities &= ~DeviceCapabilities.ExternalSensor;
        }

        CapabilitiesChanged?.Invoke(this, Capabilities);
    }

    public virtual void SetFanDuty(double percent)
    {
        if (ECDetails.AddressFanDuty == 0)
            return;

        if (!UseOpenLib || !IsOpen)
            return;

        var duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin;
        var data = Convert.ToByte(duty);

        ECRamDirectWriteByte(ECDetails.AddressFanDuty, ECDetails, data);
    }

    public virtual void SetFanControl(bool enable, int mode = 0)
    {
        if (ECDetails.AddressFanControl == 0)
            return;

        if (!UseOpenLib || !IsOpen)
            return;

        var data = Convert.ToByte(enable);
        ECRamDirectWriteByte(ECDetails.AddressFanControl, ECDetails, data);
    }

    public virtual float ReadFanDuty()
    {
        if (ECDetails.AddressFanControl == 0)
            return 0;

        // todo: implement me
        return 0;
    }

    public virtual bool SetLedStatus(bool status)
    {
        return true;
    }

    public virtual bool SetLedBrightness(int brightness)
    {
        return true;
    }

    public virtual bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
    {
        return true;
    }

    public virtual bool SetLEDPreset(LEDPreset? preset)
    {
        return true;
    }

    public virtual byte ECRamReadByte(ushort register)
    {
        if (!UseOpenLib || !IsOpen)
            return 0;

        try
        {
            return openLibSys?.ReadIoPortByte(register) ?? 0;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't read byte from address {0} using OpenLibSys. ErrorCode: {1}", register, ex.Message);
            return 0;
        }
    }

    public virtual bool ECRamWriteByte(ushort register, byte data)
    {
        if (!UseOpenLib || !IsOpen)
            return false;

        try
        {
            openLibSys?.WriteIoPortByte(register, data);
            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't write byte to address {0} using OpenLibSys. ErrorCode: {1}", register, ex.Message);
            return false;
        }
    }

    public virtual byte ECRamDirectReadByte(ushort register, ECDetails details)
    {
        if (!UseOpenLib || !IsOpen)
            return 0;

        var addr_upper = (byte)((register >> 8) & byte.MaxValue);
        var addr_lower = (byte)(register & byte.MaxValue);

        try
        {
            openLibSys?.EnterSuperIoConfig(details.AddressStatusCommandPort, details.AddressDataPort);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x11);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, addr_upper);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x10);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, addr_lower);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x12);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);

            return openLibSys?.ReadIoPortByte(details.AddressDataPort) ?? 0;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't read to port using OpenLibSys. ErrorCode: {0}", ex.Message);
            return 0;
        }
        finally
        {
            openLibSys?.ExitSuperIoConfig(details.AddressStatusCommandPort, details.AddressDataPort);
        }
    }

    public virtual bool ECRamDirectWriteByte(ushort register, ECDetails details, byte data)
    {
        if (!UseOpenLib || !IsOpen)
            return false;

        byte addr_upper = (byte)((register >> 8) & byte.MaxValue);
        byte addr_lower = (byte)(register & byte.MaxValue);

        try
        {
            openLibSys?.EnterSuperIoConfig(details.AddressStatusCommandPort, details.AddressDataPort);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x11);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, addr_upper);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x10);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, addr_lower);

            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2E);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, 0x12);
            openLibSys?.WriteIoPortByte(details.AddressStatusCommandPort, 0x2F);
            openLibSys?.WriteIoPortByte(details.AddressDataPort, data);

            return true;
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't write to port using OpenLibSys. ErrorCode: {0}", ex.Message);
            return false;
        }
        finally
        {
            openLibSys?.ExitSuperIoConfig(details.AddressStatusCommandPort, details.AddressDataPort);
        }
    }

    protected virtual void EcWriteByte(byte register, byte data)
    {
        if (!UseOpenLib || !IsOpen)
            return;

        openLibSys?.EcWriteByte(register, data);
    }

    protected virtual byte EcReadByte(byte register)
    {
        if (!UseOpenLib || !IsOpen)
            return 0;

        return openLibSys?.EcReadByte(register) ?? 0;
    }

    public virtual void set_long_limit(int limit)
    { }

    public virtual void set_short_limit(int limit)
    { }

    public virtual void set_min_gfxclk_freq(uint clock)
    { }

    public virtual void set_max_gfxclk_freq(uint clock)
    { }

    public virtual void set_gfx_clk(uint clock)
    { }

    protected void KeyPress(ButtonFlags button)
    {
        KeyPressed?.Invoke(this, button);
    }

    protected void KeyRelease(ButtonFlags button)
    {
        KeyReleased?.Invoke(this, button);
    }

    protected void KeyPressAndRelease(ButtonFlags button, short delay)
    {
        Task.Run(async () =>
        {
            KeyPress(button);
            await Task.Delay(delay).ConfigureAwait(false); // Avoid blocking the synchronization context
            KeyRelease(button);
        });
    }

    public bool HasKey()
    {
        foreach (KeyboardChord pair in OEMChords.Where(a => !a.silenced))
        {
            IEnumerable<KeyCode> chords = pair.chords.SelectMany(chord => chord.Value);
            if (chords.Any())
                return true;
        }

        return false;
    }

    public static IEnumerable<HidDevice> GetHidDevices(int vendorId, int[] deviceIds, int minFeatures = 1)
    {
        HidDevice[] HidDeviceList = HidDevices.Enumerate(vendorId, deviceIds).ToArray();
        foreach (HidDevice device in HidDeviceList)
            if (device.IsConnected && device.Capabilities.FeatureReportByteLength >= minFeatures)
                yield return device;
    }

    public static IEnumerable<HidDevice> GetHidDevices(int vendorId, int deviceId, int minFeatures = 1)
    {
        return GetHidDevices(vendorId, new int[] { deviceId }, minFeatures);
    }

    public static byte[] WithReportID(byte[] payload, byte reportID = 0x00, int reportLen = 64)
    {
        var buffer = new byte[1 + reportLen];
        buffer[0] = reportID;
        int len = Math.Min(payload.Length, reportLen);
        Buffer.BlockCopy(payload, 0, buffer, 1, len);
        return buffer;
    }

    public string GetButtonName(ButtonFlags button)
    {
        return EnumUtils.GetDescriptionFromEnumValue(button, GetType().Name);
    }

    public GlyphIconInfo GetGlyphIconInfo(ButtonFlags button, int fontIconSize = 14)
    {
        string? glyph = GetGlyph(button);
        return new GlyphIconInfo
        {
            Name = GetButtonName(button),
            Glyph = glyph is not null ? glyph : defaultGlyph,
            FontSize = fontIconSize,
            FontFamily = GlyphFontFamily,
            Color = Colors.White
        };
    }

    public virtual string GetFontFamily(ButtonFlags button)
    {
        return "PromptFont";
    }

    public virtual string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2780";
            case ButtonFlags.OEM2:
                return "\u2781";
            case ButtonFlags.OEM3:
                return "\u2782";
            case ButtonFlags.OEM4:
                return "\u2783";
            case ButtonFlags.OEM5:
                return "\u2784";
            case ButtonFlags.OEM6:
                return "\u2785";
            case ButtonFlags.OEM7:
                return "\u2786";
            case ButtonFlags.OEM8:
                return "\u2787";
            case ButtonFlags.OEM9:
                return "\u2788";
            case ButtonFlags.OEM10:
                return "\u2789";
        }

        return defaultGlyph;
    }
}
