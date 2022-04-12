using ControllerCommon;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ControllerService
{
    public class XInputController
    {
        #region imports
        [StructLayout(LayoutKind.Explicit)]
        public struct XInputGamepad
        {
            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(0)]
            public short wButtons;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(2)]
            public byte bLeftTrigger;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(3)]
            public byte bRightTrigger;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(4)]
            public short sThumbLX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(6)]
            public short sThumbLY;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(8)]
            public short sThumbRX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(10)]
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputVibration
        {
            [MarshalAs(UnmanagedType.I2)]
            public ushort LeftMotorSpeed;

            [MarshalAs(UnmanagedType.I2)]
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct XInputCapabilities
        {
            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(0)]
            byte Type;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(1)]
            public byte SubType;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(2)]
            public short Flags;

            [FieldOffset(4)]
            public XInputGamepad Gamepad;

            [FieldOffset(16)]
            public XInputVibration Vibration;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputCapabilitiesEx
        {
            public XInputCapabilities Capabilities;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 VID;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 PID;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 REV;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 XID;
        };

        [DllImport("xinput1_4.dll", EntryPoint = "#108")]
        public static extern int XInputGetCapabilitiesEx
        (
            int a1,            // [in] unknown, should probably be 1
            int dwUserIndex,   // [in] Index of the gamer associated with the device
            int dwFlags,       // [in] Input flags that identify the device type
            ref XInputCapabilitiesEx pCapabilities  // [out] Receives the capabilities
        );
        #endregion

        public Controller physicalController;
        public string ProductName = "XInput Controller for Windows";

        public XInputCapabilitiesEx XInputData;
        public List<string> ControllerIDs = new();

        public ViGEmTarget virtualTarget;

        public Gamepad Gamepad;
        private Gamepad prevGamepad;

        public Profile profile;
        private Profile defaultProfile;

        public Vector3 Acceleration;
        public Vector3 AccelerationRaw;
        public Vector3 Angle;
        public Vector3 AngularVelocityC;
        public Vector3 AngularVelocity;
        public Vector3 AngularVelocityRad;
        public Vector3 AngularRawC;

        public MultimediaTimer UpdateTimer;
        public double vibrationStrength = 100.0d;
        public int updateInterval = 10;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;

        public SensorFusion sensorFusion;
        public MadgwickAHRS madgwickAHRS;

        protected readonly Stopwatch stopwatch;
        public long CurrentMicroseconds;

        public double TotalMilliseconds;
        public double UpdateTimePreviousMilliseconds;
        public double DeltaSeconds;

        public DS4Touch Touch;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(XInputController controller);

        protected object updateLock = new();
        public UserIndex UserIndex;
        private readonly ILogger logger;
        private readonly PipeServer pipeServer;

        public XInputController(Controller controller, UserIndex index, ILogger logger, PipeServer pipeServer)
        {
            this.logger = logger;
            this.pipeServer = pipeServer;

            // initilize controller
            this.physicalController = controller;
            this.UserIndex = index;

            // pull data from xinput
            XInputData = new XInputCapabilitiesEx();
            if (XInputGetCapabilitiesEx(1, (int)index, 0, ref XInputData) != 0)
                logger.LogWarning($"Failed to retrive XInputData.");

            // initialize ID(s)
            UpdateIDs();

            // initialize sensor(s)
            UpdateSensors();

            // initialize vectors
            AngularVelocity = new();
            AngularVelocityRad = new();
            AngularRawC = new();
            Acceleration = new();
            Angle = new();

            // initialize sensorfusion and madgwick
            sensorFusion = new SensorFusion(logger);
            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);

            // initialize profile(s)
            profile = new();
            defaultProfile = new();

            // initialize touch
            Touch = new();

            // initialize stopwatch
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // initialize timers
            UpdateTimer = new MultimediaTimer(updateInterval);
            UpdateTimer.Tick += UpdateTimer_Ticked;
            UpdateTimer.Start();
        }

        public void UpdateSensors()
        {
            Gyrometer = new XInputGirometer(this, logger);
            Accelerometer = new XInputAccelerometer(this, logger);
            Inclinometer = new XInputInclinometer(this, logger);
        }

        private void UpdateIDs()
        {
            string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE \"%VID_0{XInputData.VID.ToString("X2")}&PID_0{XInputData.PID.ToString("X2")}%\"";
            var moSearch = new ManagementObjectSearcher(query);
            var moCollection = moSearch.Get();

            int idx = 0;
            foreach (ManagementObject mo in moCollection)
            {
                if (idx == 0)
                    ProductName = (string)mo.Properties["Description"].Value;

                string DeviceID = (string)mo.Properties["DeviceID"].Value;
                if (DeviceID != null && !ControllerIDs.Contains(DeviceID))
                    ControllerIDs.Add(DeviceID);

                idx++;
            }
        }

        private void UpdateTimer_Ticked(object sender, EventArgs e)
        {
            // update timestamp
            CurrentMicroseconds = stopwatch.ElapsedMilliseconds * 1000L;
            TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            DeltaSeconds = (TotalMilliseconds - UpdateTimePreviousMilliseconds) / 1000L;
            UpdateTimePreviousMilliseconds = TotalMilliseconds;

            // get current gamepad state
            State state = physicalController.GetState();
            Gamepad = state.Gamepad;

            lock (updateLock)
            {
                // update reading(s)
                AngularVelocity = Gyrometer.GetCurrentReading();
                AngularVelocityC = Gyrometer.GetCurrentReading(true);
                AngularRawC = Gyrometer.GetCurrentReadingRaw(true);

                Acceleration = Accelerometer.GetCurrentReading();
                AccelerationRaw = Accelerometer.GetCurrentReadingRaw(false);
                Angle = Inclinometer.GetCurrentReading();

                // update sensorFusion (todo: call only when needed ?)
                sensorFusion.UpdateReport(TotalMilliseconds, DeltaSeconds, AngularVelocity, Acceleration);

                // async update client(s)
                Task.Run(() =>
                {
                    switch (ControllerService.CurrentTag)
                    {
                        case "ProfileSettingsMode0":
                            pipeServer?.SendMessage(new PipeSensor(AngularVelocityC, SensorType.Girometer));
                            break;

                        case "ProfileSettingsMode1":
                            pipeServer?.SendMessage(new PipeSensor(Angle, SensorType.Inclinometer));
                            break;
                    }

                    switch (ControllerService.CurrentOverlayStatus)
                    {
                        case 0: // Visible
                            AngularVelocityRad.X = -InputUtils.deg2rad(AngularRawC.X);
                            AngularVelocityRad.Y = -InputUtils.deg2rad(AngularRawC.Y);
                            AngularVelocityRad.Z = -InputUtils.deg2rad(AngularRawC.Z);
                            madgwickAHRS.UpdateReport(AngularVelocityRad.X, AngularVelocityRad.Y, AngularVelocityRad.Z, -AccelerationRaw.X, AccelerationRaw.Y, AccelerationRaw.Z, DeltaSeconds);

                            pipeServer?.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            break;
                        case 1: // Hidden
                        case 2: // Collapsed
                            madgwickAHRS = new MadgwickAHRS(0.01f, 0.1f);
                            pipeServer?.SendMessage(new PipeSensor(madgwickAHRS.GetEuler(), madgwickAHRS.GetQuaternion(), SensorType.Quaternion));
                            ControllerService.CurrentOverlayStatus = 3; // leave the loop
                            break;
                    }
                });

                Task.Run(() =>
                {
                    logger.LogDebug("Plot AccelerationRawX {0} {1}", TotalMilliseconds, AccelerationRaw.X);
                    logger.LogDebug("Plot AccelerationRawY {0} {1}", TotalMilliseconds, AccelerationRaw.Y);
                    logger.LogDebug("Plot AccelerationRawZ {0} {1}", TotalMilliseconds, AccelerationRaw.Z);

                    logger.LogDebug("Plot GyroRawCX {0} {1}", TotalMilliseconds, AngularRawC.X);
                    logger.LogDebug("Plot GyroRawCY {0} {1}", TotalMilliseconds, AngularRawC.Y);
                    logger.LogDebug("Plot GyroRawCZ {0} {1}", TotalMilliseconds, AngularRawC.Z);

                    logger.LogDebug("Plot PoseX {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().X);
                    logger.LogDebug("Plot PoseY {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Y);
                    logger.LogDebug("Plot PoseZ {0} {1}", TotalMilliseconds, madgwickAHRS.GetEuler().Z);
                });

                if (prevGamepad.ToString() != Gamepad.ToString())
                    pipeServer?.SendMessage(new PipeGamepad(Gamepad));
                prevGamepad = Gamepad;

                // update virtual controller
                virtualTarget?.UpdateReport(Gamepad);

                Updated?.Invoke(this);
            }
        }

        public void SetProfile(Profile profile)
        {
            // skip if current profile
            if (profile == this.profile)
                return;

            // restore default profile
            if (profile == null)
                profile = defaultProfile;

            this.profile = profile;

            // update default profile
            if (profile.IsDefault)
                defaultProfile = profile;
            else
                logger.LogInformation("Profile {0} applied.", profile.name);
        }

        public void SetPollRate(int HIDrate)
        {
            updateInterval = HIDrate;
            UpdateTimer.Interval = HIDrate;
        }

        public void SetVibrationStrength(double strength)
        {
            vibrationStrength = strength;
            this.virtualTarget?.SetVibrationStrength(vibrationStrength);
        }

        public void SetViGEmTarget(ViGEmTarget target)
        {
            this.virtualTarget = target;

            SetPollRate(updateInterval);
            SetVibrationStrength(vibrationStrength);

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, ProductName, UserIndex);
        }
    }
}