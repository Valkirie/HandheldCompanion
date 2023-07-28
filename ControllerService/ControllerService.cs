using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Platforms;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using ControllerService.Properties;
using ControllerService.Targets;
using Microsoft.Extensions.Hosting;
using Nefarius.Utilities.DeviceManagement.PnP;
using Nefarius.ViGEm.Client;
using static ControllerCommon.Managers.PowerManager;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerService;

public class ControllerService : IHostedService
{
    public delegate void UpdatedEventHandler();

    // controllers vars
    public static ViGEmClient vClient;
    public static ViGEmTarget vTarget;

    // devices vars
    public static IDevice CurrentDevice;

    public static string CurrentPath, CurrentPathDep;
    public static string CurrentTag;
    public static int CurrentOverlayStatus = 2;

    // sensor vars
    private static SensorFamily SensorSelection;
    private static int SensorPlacement;
    private static bool SensorPlacementUpsideDown;

    // profile vars
    public static Profile currentProfile = new();
    public static PlatformType currentPlatform;

    // settings vars
    public Configuration configuration;
    private readonly bool DSUEnabled;
    private readonly string DSUip;
    private readonly int DSUport;

    private readonly DSUServer DSUServer;
    private HIDmode HIDmode = HIDmode.NoController;
    private HIDstatus HIDstatus = HIDstatus.Disconnected;
    private SerialUSBIMU sensor;

    public ControllerService(IHostApplicationLifetime lifetime)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

        // paths
        CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
        CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

        // settings
        // todo: move me to a specific class
        configuration = ConfigurationManager.OpenExeConfiguration("ControllerService.exe");

        HIDmode = Enum.Parse<HIDmode>(configuration.AppSettings.Settings["HIDmode"].Value);
        HIDstatus = Enum.Parse<HIDstatus>(configuration.AppSettings.Settings["HIDstatus"].Value);

        DSUEnabled = bool.Parse(configuration.AppSettings.Settings["DSUEnabled"].Value);
        DSUip = configuration.AppSettings.Settings["DSUip"].Value;
        DSUport = int.Parse(configuration.AppSettings.Settings["DSUport"].Value);

        SensorSelection = Enum.Parse<SensorFamily>(configuration.AppSettings.Settings["SensorSelection"].Value);
        SensorPlacement = int.Parse(configuration.AppSettings.Settings["SensorPlacement"].Value);
        SensorPlacementUpsideDown = bool.Parse(configuration.AppSettings.Settings["SensorPlacementUpsideDown"].Value);

        // verifying ViGEm is installed
        try
        {
            vClient = new ViGEmClient();
        }
        catch (Exception)
        {
            LogManager.LogCritical("ViGEm is missing. Please get it from: {0}",
                "https://github.com/ViGEm/ViGEmBus/releases");
            throw new InvalidOperationException();
        }

        // initialize PipeServer
        PipeServer.Connected += OnClientConnected;
        PipeServer.Disconnected += OnClientDisconnected;
        PipeServer.ClientMessage += OnClientMessage;

        // initialize manager(s)
        DeviceManager.UsbDeviceRemoved += GenericDeviceRemoved;
        DeviceManager.Start();

        // initialize device
        CurrentDevice = IDevice.GetDefault();
        CurrentDevice.PullSensors();

        // as of 06/20/2023, Bosch BMI320/BMI323 is crippled
        string currentDeviceType = CurrentDevice.GetType().Name;
        switch (currentDeviceType)
        {
            case "AYANEOAIRPlus":
            case "ROGAlly":
                {
                    LogManager.LogInformation("Restarting: {0}", CurrentDevice.InternalSensorName);

                    if (CurrentDevice.RestartSensor())
                    {
                        // give the device some breathing space once restarted
                        Thread.Sleep(500);

                        LogManager.LogInformation("Successfully restarted: {0}", CurrentDevice.InternalSensorName);
                    }
                    else
                        LogManager.LogError("Failed to restart: {0}", CurrentDevice.InternalSensorName);
                }
                break;
        }

        // initialize DSUClient
        DSUServer = new DSUServer(DSUip, DSUport);
        DSUServer.Started += OnDSUStarted;
        DSUServer.Stopped += OnDSUStopped;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // start master timer
        TimerManager.Start();

        // start DSUClient
        if (DSUEnabled)
            DSUServer.Start();

        // start Pipe Server
        PipeServer.Open();

        // start Power Manager
        SystemStatusChanged += OnSystemStatusChanged;
        Start(true);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // stop master timer
        TimerManager.Stop();

        // stop listening from controller
        IMU.Stop();

        // update virtual controller
        SetControllerMode(HIDmode.NoController);

        // stop Power Manager
        Stop();

        // stop DSUClient
        DSUServer?.Stop();

        // stop Pipe Server
        PipeServer.Close();

        // stop System Manager
        DeviceManager.Stop();

        return Task.CompletedTask;
    }

    public static event UpdatedEventHandler ForegroundUpdated;

    private void GenericDeviceRemoved(PnPDevice device, DeviceEventArgs obj)
    {
        // If the USB Gyro is unplugged, close serial connection
        if (sensor is null)
            return;

        sensor.Close();

        // Stop IMU is USB Gyro was our motion source
        switch (SensorSelection)
        {
            case SensorFamily.SerialUSBIMU:
                IMU.Stop();
                break;
        }
    }

    private void SetControllerMode(HIDmode mode)
    {
        // do not disconnect if similar to previous mode
        if (HIDmode == mode && vTarget is not null)
            return;

        // disconnect current virtual controller
        if (vTarget is not null)
            vTarget.Disconnect();

        switch (mode)
        {
            case HIDmode.Xbox360Controller:
                vTarget = new Xbox360Target();
                break;
            case HIDmode.DualShock4Controller:
                vTarget = new DualShock4Target();
                break;
            default:
            case HIDmode.NoController:
                if (vTarget is not null)
                {
                    vTarget.Dispose();
                    vTarget = null;
                }

                break;
        }

        // failed to initialize controller
        if (vTarget is null)
        {
            if (mode != HIDmode.NoController)
                LogManager.LogError("Failed to initialise virtual controller with HIDmode: {0}", mode);
            return;
        }

        vTarget.Connected += OnTargetConnected;
        vTarget.Disconnected += OnTargetDisconnected;

        // update status
        SetControllerStatus(HIDstatus);

        // update current HIDmode
        HIDmode = mode;
    }

    private void SetControllerStatus(HIDstatus status)
    {
        if (vTarget is null)
            return;

        switch (status)
        {
            default:
            case HIDstatus.Connected:
                vTarget.Connect();
                break;
            case HIDstatus.Disconnected:
                vTarget.Disconnect();
                break;
        }

        // update current HIDstatus
        HIDstatus = status;
    }

    private void OnTargetDisconnected(ViGEmTarget target)
    {
        // send notification
        PipeServer.SendMessage(new PipeServerToast
        {
            title = $"{target}",
            content = Resources.ToastOnTargetDisconnected,
            image = $"HIDmode{(uint)target.HID}"
        });
    }

    private void OnTargetConnected(ViGEmTarget target)
    {
        // send virtual controller connected notification
        PipeServer.SendMessage(new PipeServerControllerConnect());

        // send notification
        PipeServer.SendMessage(new PipeServerToast
        {
            title = $"{target}",
            content = Resources.ToastOnTargetConnected,
            image = $"HIDmode{(uint)target.HID}"
        });
    }

    // deprecated
    private void OnDSUStopped(DSUServer server)
    {
        /* DSUEnabled = false;
        configuration.GetSection("Settings:DSUEnabled").Value = false.ToString();

        PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
        pipeServer.SendMessage(settings); */
    }

    // deprecated
    private void OnDSUStarted(DSUServer server)
    {
        /* DSUEnabled = true;
        configuration.GetSection("Settings:DSUEnabled").Value = true.ToString();

        PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
        pipeServer.SendMessage(settings); */
    }

    private void OnClientMessage(PipeMessage message)
    {
        switch (message.code)
        {
            case PipeCode.CLIENT_PROFILE:
                {
                    var profileMsg = (PipeClientProfile)message;
                    UpdateProfile(profileMsg.profile);

                    // unset flag
                    SystemPending = false;
                }
                break;

            case PipeCode.CLIENT_PROCESS:
            {
                var process = (PipeClientProcess)message;
                UpdateProcess(process.executable, process.platform);
            }
                break;

            case PipeCode.CLIENT_CURSOR:
            {
                var cursor = (PipeClientCursor)message;

                switch (cursor.action)
                {
                    case CursorAction.CursorUp:
                        DS4Touch.OnMouseUp(cursor.x, cursor.y, cursor.button, cursor.flags);
                        break;
                    case CursorAction.CursorDown:
                        DS4Touch.OnMouseDown(cursor.x, cursor.y, cursor.button, cursor.flags);
                        break;
                    case CursorAction.CursorMove:
                        DS4Touch.OnMouseMove(cursor.x, cursor.y, cursor.button, cursor.flags);
                        break;
                }
            }
                break;

            case PipeCode.CLIENT_SETTINGS:
            {
                var settings = (PipeClientSettings)message;
                UpdateSettings(settings.Settings);
            }
                break;

            case PipeCode.CLIENT_NAVIGATED:
            {
                var navigation = (PipeNavigation)message;
                CurrentTag = navigation.Tag;

                switch (navigation.Tag)
                {
                    case "SettingsMode0":
                        // do something
                        break;
                    case "SettingsMode1":
                        // do something
                        break;
                }
            }
                break;

            case PipeCode.CLIENT_OVERLAY:
            {
                var overlay = (PipeOverlay)message;
                CurrentOverlayStatus = overlay.Visibility;
            }
                break;

            case PipeCode.CLIENT_INPUT:
            {
                var inputMsg = (PipeClientInputs)message;

                vTarget?.UpdateInputs(inputMsg.controllerState);
                DSUServer.UpdateInputs(inputMsg.controllerState);
                DS4Touch.UpdateInputs(inputMsg.controllerState);
            }
                break;

            case PipeCode.CLIENT_MOVEMENTS:
            {
                var movements = (PipeClientMovements)message;

                IMU.UpdateMovements(movements.Movements);
            }
                break;

            case PipeCode.CLIENT_CONTROLLER_CONNECT:
            {
                var connect = (PipeClientControllerConnect)message;
                // do something?
            }
                break;

            case PipeCode.CLIENT_CONTROLLER_DISCONNECT:
            {
                var disconnect = (PipeClientControllerDisconnect)message;
                // do something ?
            }
                break;
        }
    }

    private void OnClientDisconnected()
    {
        DS4Touch.OnMouseUp(0, 0, CursorButton.TouchLeft);
        DS4Touch.OnMouseUp(0, 0, CursorButton.TouchRight);
    }

    private void OnClientConnected()
    {
        // send server settings to client
        PipeServer.SendMessage(new PipeServerSettings { Settings = GetSettings() });
    }

    internal void UpdateProfile(Profile profile)
    {
        // skip if current profile
        if (profile == currentProfile)
            return;

        // update current profile
        currentProfile = profile;
        ForegroundUpdated?.Invoke();

        LogManager.LogInformation("Profile {0} applied", profile.Name);
    }

    internal void UpdateProcess(string executable, PlatformType platform)
    {
        // skip if current platform
        if (platform == currentPlatform)
            return;

        // update current platform
        currentPlatform = platform;
        ForegroundUpdated?.Invoke();

        LogManager.LogInformation("Platform {0} detected", platform);
    }

    public void UpdateSettings(Dictionary<string, string> args)
    {
        foreach (var pair in args)
        {
            var name = pair.Key;
            var property = pair.Value;

            if (configuration.AppSettings.Settings.AllKeys.ToList().Contains(name))
            {
                configuration.AppSettings.Settings[name].Value = property;
                configuration.Save(ConfigurationSaveMode.Modified);
            }

            ApplySetting(name, property);
            LogManager.LogDebug("{0} set to {1}", name, property);
        }
    }

    private void ApplySetting(string name, string property)
    {
        switch (name)
        {
            case "HIDmode":
            {
                var value = Enum.Parse<HIDmode>(property);

                if (HIDmode == value)
                    return;

                SetControllerMode(value);
            }
                break;
            case "HIDstatus":
            {
                var value = Enum.Parse<HIDstatus>(property);

                if (HIDstatus == value)
                    return;

                SetControllerStatus(value);
            }
                break;
            case "DSUEnabled":
            {
                var value = Convert.ToBoolean(property);
                switch (value)
                {
                    case true:
                        DSUServer.Start();
                        break;
                    case false:
                        DSUServer.Stop();
                        break;
                }
            }
                break;
            case "DSUip":
            {
                var value = Convert.ToString(property);
                // DSUServer.ip = value;
            }
                break;
            case "DSUport":
            {
                var value = Convert.ToInt32(property);
                DSUServer.port = value;
            }
                break;
            case "SensorPlacement":
            {
                var value = Convert.ToInt32(property);
                SensorPlacement = value;
                sensor?.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);
            }
                break;
            case "SensorPlacementUpsideDown":
            {
                var value = Convert.ToBoolean(property);
                SensorPlacementUpsideDown = value;
                sensor?.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);
            }
                break;
            case "SensorSelection":
            {
                var value = Enum.Parse<SensorFamily>(property);

                if (SensorSelection == value)
                    return;

                // In case current selection is USG Gyro, close serial connection
                if (SensorSelection == SensorFamily.SerialUSBIMU)
                    if (sensor is not null)
                        sensor.Close();

                SensorSelection = value;

                // Establish serial port connection on selection change to USG Gyro
                if (SensorSelection == SensorFamily.SerialUSBIMU)
                {
                    sensor = SerialUSBIMU.GetDefault();

                    if (sensor is null)
                        break;

                    sensor.Open();
                    sensor.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);
                }

                IMU.Stop();
                IMU.SetSensorFamily(SensorSelection);
                IMU.Start();
            }
                break;
        }
    }

    private bool SystemPending;
    private int SystemPendingIdx;

    private async void OnSystemStatusChanged(SystemStatus status, SystemStatus prevStatus)
    {
        if (status == prevStatus)
            return;

        switch (status)
        {
            case SystemStatus.SystemReady:
                {
                    switch (prevStatus)
                    {
                        case SystemStatus.SystemBooting:
                            // cold boot
                            IMU.SetSensorFamily(SensorSelection);
                            IMU.Start();
                            break;
                        case SystemStatus.SystemPending:
                            // resume from sleep
                            // restart IMU
                            IMU.Restart(true);
                            break;
                    }

                    // check if service/system was suspended previously
                    if (vTarget is not null)
                        return;

                    // extra delay for device functions
                    while (SystemPending && SystemPendingIdx < CurrentDevice.ResumeDelay / 1000)
                    {
                        Thread.Sleep(1000);
                        SystemPendingIdx++;
                    }

                    while (vTarget is null || !vTarget.IsConnected)
                    {
                        // reset vigem
                        ResetViGEm();

                        // create new ViGEm client
                        vClient = new ViGEmClient();

                        // set controller mode
                        SetControllerMode(HIDmode);

                        Thread.Sleep(1000);
                    }

                    // start timer manager
                    TimerManager.Start();
                }
                break;

            case SystemStatus.SystemPending:
                {
                    // set flag
                    SystemPending = true;
                    SystemPendingIdx = 0;

                    // stop timer manager
                    TimerManager.Stop();

                    // clear pipes
                    PipeServer.ClearQueue();

                    // stop sensors
                    IMU.Stop();

                    // reset vigem
                    ResetViGEm();
                }
                break;
        }
    }

    private void ResetViGEm()
    {
        // dispose virtual controller
        if (vTarget is not null)
        {
            vTarget.Dispose();
            vTarget = null;
        }

        // dispose ViGEm drivers
        if (vClient is not null)
        {
            vClient.Dispose();
            vClient = null;
        }
    }

    public Dictionary<string, string> GetSettings()
    {
        Dictionary<string, string> settings = new();

        foreach (var key in configuration.AppSettings.Settings.AllKeys)
            settings.Add(key, configuration.AppSettings.Settings[key].Value);

        return settings;
    }
}