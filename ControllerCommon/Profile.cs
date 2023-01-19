using ControllerCommon.Actions;
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
    public class Profile
    {
        // move me to HandheldCompanion ?
        public static Dictionary<MotionInput, string> InputDescription = new()
        {
            { MotionInput.JoystickCamera, Properties.Resources.JoystickCameraDesc },
            { MotionInput.JoystickSteering, Properties.Resources.JoystickSteeringDesc },
            { MotionInput.PlayerSpace, Properties.Resources.PlayerSpaceDesc },
            { MotionInput.AutoRollYawSwap, Properties.Resources.AutoRollYawSwapDesc }
        };

        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Default { get; set; }

        public Dictionary<ButtonFlags, IActions> ButtonMapping { get; set; } = new();
        public Dictionary<AxisFlags, IActions> AxisMapping { get; set; } = new();

        [JsonIgnore]
        public bool Running { get; set; }

        public bool whitelisted { get; set; }                   // if true, can see through the HidHide cloak
        public bool use_wrapper { get; set; }                   // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;            // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;        // accelerometer multiplicator (remove me)

        public int steering { get; set; } = 0;                  // 0 = Roll, 1 = Yaw
        public bool thumb_improve_circularity_left { get; set; } = true;
        public bool thumb_improve_circularity_right { get; set; } = true;
        public int thumb_deadzone_inner_left { get; set; } = 0;
        public int thumb_deadzone_outer_left { get; set; } = 0;
        public int thumb_deadzone_inner_right { get; set; } = 0;
        public int thumb_deadzone_outer_right { get; set; } = 0;

        public float thumb_anti_deadzone_left { get; set; } = 0.0f;        // todo: typeme
        public float thumb_anti_deadzone_right { get; set; } = 0.0f;        // todo: typeme

        public int trigger_deadzone_inner_left { get; set; } = 0;
        public int trigger_deadzone_outer_left { get; set; } = 0;
        public int trigger_deadzone_inner_right { get; set; } = 0;
        public int trigger_deadzone_outer_right { get; set; } = 0;

        public bool inverthorizontal { get; set; }              // if true, invert horizontal axis
        public bool invertvertical { get; set; }                // if false, invert vertical axis

        public bool MotionEnabled { get; set; }
        public MotionInput MotionInput { get; set; } = MotionInput.JoystickCamera;
        public MotionOutput MotionOutput { get; set; } = MotionOutput.RightStick;
        public MotionMode MotionMode { get; set; } = MotionMode.Off;
        public float MotionAntiDeadzone { get; set; } = 0.0f;
        public ButtonState MotionTrigger { get; set; } = new();

        // aiming
        public float aiming_sensitivity_x { get; set; } = 1.0f;
        public float aiming_sensitivity_y { get; set; } = 1.0f;

        public List<ProfileVector> aiming_array { get; set; } = new();

        // steering
        public float steering_max_angle { get; set; } = 30.0f;
        public float steering_power { get; set; } = 1.0f;
        public float steering_deadzone { get; set; } = 0.0f;

        // Aiming down sights
        public float aiming_down_sights_multiplier { get; set; } = 1.0f;
        public ButtonState aiming_down_sights_activation { get; set; } = new();

        // flickstick
        public bool flickstick_enabled { get; set; }
        public float flick_duration { get; set; } = 0.1f;
        public float stick_sensivity { get; set; } = 3.0f;

        // power
        public bool TDP_override { get; set; }
        public double[] TDP_value { get; set; } = new double[3];

        // hidden settings
        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string ExecutablePath { get; set; }
        [JsonIgnore] public static int array_size = 49;             // x + 1 (hidden)

        public Profile()
        {
            // initialize aiming array
            if (aiming_array.Count == 0)
            {
                for (int i = 0; i < array_size; i++)
                {
                    double value = (double)i / (double)(array_size - 1);
                    ProfileVector vector = new ProfileVector(value, 0.5f);
                    aiming_array.Add(vector);
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
        }

        public float GetSensitivityX()
        {
            return aiming_sensitivity_x * 1000.0f;
        }

        public float GetSensitivityY()
        {
            return aiming_sensitivity_y * 1000.0f;
        }

        public string GetFileName()
        {
            string name = Name;
            switch(Default)
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
    }
}
