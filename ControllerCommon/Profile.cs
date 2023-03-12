using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ControllerCommon
{
    [Flags]
    public enum ProfileErrorCode
    {
        None = 0,
        MissingExecutable = 1,
        MissingPath = 2,
        MissingPermission = 3,
        Default = 4,
        Running = 5
    }

    [Flags]
    public enum ProfileUpdateSource
    {
        Background = 0,
        ProfilesPage = 1,
        QuickProfilesPage = 2,
        Creation = 4,
        Serialiazer = 5
    }

    [Serializable]
    public class ProfileVector
    {
        public double x { get; set; }
        public double y { get; set; }

        public ProfileVector(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public class Profile : ICloneable
    {
        // move me to HandheldCompanion ?
        public static Dictionary<MotionInput, string> InputDescription = new()
        {
            { MotionInput.JoystickCamera, Properties.Resources.JoystickCameraDesc },
            { MotionInput.JoystickSteering, Properties.Resources.JoystickSteeringDesc },
            { MotionInput.PlayerSpace, Properties.Resources.PlayerSpaceDesc },
            { MotionInput.AutoRollYawSwap, Properties.Resources.AutoRollYawSwapDesc }
        };

        [JsonIgnore]
        public const int SensivityArraySize = 49;             // x + 1 (hidden)

        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
        [JsonIgnore]
        public string ExecutablePath;
        public bool Enabled { get; set; }
        public bool Default { get; set; }

        public Layout Layout { get; set; } = new();

        [JsonIgnore]
        public bool Running { get; set; }

        public bool Whitelisted { get; set; }                       // if true, can see through the HidHide cloak
        public bool XInputPlus { get; set; }                        // if true, deploy xinput1_3.dll

        public float GyrometerMultiplier { get; set; } = 1.0f;      // gyroscope multiplicator (remove me)
        public float AccelerometerMultiplier { get; set; } = 1.0f;  // accelerometer multiplicator (remove me)

        public int SteeringAxis { get; set; } = 0;                  // 0 = Roll, 1 = Yaw

        public bool MotionEnabled { get; set; }
        public MotionInput MotionInput { get; set; } = MotionInput.JoystickCamera;
        public MotionOutput MotionOutput { get; set; } = MotionOutput.RightStick;
        public MotionMode MotionMode { get; set; } = MotionMode.Off;
        public float MotionAntiDeadzone { get; set; } = 0.0f;
        public bool MotionInvertHorizontal { get; set; }            // if true, invert horizontal axis
        public bool MotionInvertVertical { get; set; }              // if false, invert vertical axis
        public float MotionSensivityX { get; set; } = 1.0f;
        public float MotionSensivityY { get; set; } = 1.0f;
        public List<ProfileVector> MotionSensivityArray { get; set; } = new();

        public ButtonState MotionTrigger { get; set; } = new();

        // steering
        public float SteeringMaxAngle { get; set; } = 30.0f;
        public float SteeringPower { get; set; } = 1.0f;
        public float SteeringDeadzone { get; set; } = 0.0f;

        // Aiming down sights
        public float AimingSightsMultiplier { get; set; } = 1.0f;
        public ButtonState AimingSightsTrigger { get; set; } = new();

        // flickstick
        public bool FlickstickEnabled { get; set; }
        public float FlickstickDuration { get; set; } = 0.1f;
        public float FlickstickSensivity { get; set; } = 3.0f;

        // power
        public bool TDPOverrideEnabled { get; set; }
        public double[] TDPOverrideValues { get; set; } = new double[3];

        [JsonIgnore]
        public ProfileErrorCode ErrorCode;

        public Profile()
        {
            // initialize aiming array
            if (MotionSensivityArray.Count == 0)
            {
                for (int i = 0; i < SensivityArraySize; i++)
                {
                    double value = (double)i / (double)(SensivityArraySize - 1);
                    ProfileVector vector = new ProfileVector(value, 0.5f);
                    MotionSensivityArray.Add(vector);
                }
            }
        }

        public Profile(string path) : this()
        {
            Dictionary<string, string> AppProperties = ProcessUtils.GetAppProperties(path);

            string ProductName = AppProperties.ContainsKey("FileDescription") ? AppProperties["FileDescription"] : AppProperties["ItemFolderNameDisplay"];
            // string Version = AppProperties.ContainsKey("FileVersion") ? AppProperties["FileVersion"] : "1.0.0.0";
            // string Company = AppProperties.ContainsKey("Company") ? AppProperties["Company"] : AppProperties.ContainsKey("Copyright") ? AppProperties["Copyright"] : "Unknown";

            this.Executable = AppProperties["FileName"];
            this.Name = ProductName;
            this.Path = this.ExecutablePath = path;

            // enable the below variables when profile is created
            this.Enabled = true;
            this.MotionEnabled = true;
            this.Layout = new("Profile");
        }

        public float GetSensitivityX()
        {
            return MotionSensivityX * 1000.0f;
        }

        public float GetSensitivityY()
        {
            return MotionSensivityY * 1000.0f;
        }

        public string GetFileName()
        {
            string name = Name;
            switch (Default)
            {
                case false:
                    name = System.IO.Path.GetFileNameWithoutExtension(Executable);
                    break;
            }

            return $"{name}.json";
        }

        public override string ToString()
        {
            return Name;
        }

        public object Clone()
        {
            Profile profile = (Profile)MemberwiseClone();
            profile.Layout = this.Layout.Clone() as Layout;
            profile.MotionTrigger = this.MotionTrigger.Clone() as ButtonState;
            profile.AimingSightsTrigger = this.AimingSightsTrigger.Clone() as ButtonState;

            return profile;
        }
    }
}
