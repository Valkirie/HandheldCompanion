using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControllerCommon
{
    public enum ProfileErrorCode
    {
        None = 0,
        MissingExecutable = 1,
        MissingPath = 2,
        MissingPermission = 3,
        IsDefault = 4,
        IsRunning = 5
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
        public static Dictionary<Input, string> InputDescription = new()
        {
            { Input.JoystickCamera, Properties.Resources.JoystickCameraDesc },
            { Input.JoystickSteering, Properties.Resources.JoystickSteeringDesc },
            { Input.PlayerSpace, Properties.Resources.PlayerSpaceDesc },
            { Input.AutoRollYawSwap, Properties.Resources.AutoRollYawSwapDesc }
        };

        public string name { get; set; }
        public string path { get; set; }
        public string executable { get; set; }
        public bool isEnabled { get; set; }

        public bool whitelisted { get; set; }                   // if true, can see through the HidHide cloak
        public bool use_wrapper { get; set; }                   // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;            // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;        // accelerometer multiplicator (remove me)

        public int steering { get; set; } = 0;                  // 0 = Roll, 1 = Yaw
        public float antideadzone { get; set; } = 0.0f;         // todo: typeme

        public bool inverthorizontal { get; set; }              // if true, invert horizontal axis
        public bool invertvertical { get; set; }                // if false, invert vertical axis

        public bool umc_enabled { get; set; }

        public Input umc_input { get; set; } = Input.JoystickCamera;
        public Output umc_output { get; set; } = Output.RightStick;

        public UMC_Motion_Default umc_motion_defaultoffon { get; set; } = UMC_Motion_Default.Off;

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
        public ControllerButtonFlags aiming_down_sights_activation { get; set; }

        // flickstick
        public bool flickstick_enabled { get; set; }
        public float flick_duration { get; set; } = 0.1f;
        public float stick_sensivity { get; set; } = 3.0f;

        // power
        public bool TDP_override { get; set; }
        public double[] TDP_value { get; set; } = new double[3];

        public ControllerButtonFlags umc_trigger { get; set; }

        // hidden settings
        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }
        [JsonIgnore] public string json { get; set; }
        [JsonIgnore] public bool isDefault { get; set; }
        [JsonIgnore] public bool isRunning { get; set; }
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

            this.executable = AppProperties["FileName"];
            this.name = ProductName;
            this.path = this.fullpath = path;

            // enable the below variables when profile is created
            this.isEnabled = true;
            this.umc_enabled = true;
        }

        public float GetSensitivityX()
        {
            return aiming_sensitivity_x * 1000.0f;
        }

        public float GetSensitivityY()
        {
            return aiming_sensitivity_y * 1000.0f;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
