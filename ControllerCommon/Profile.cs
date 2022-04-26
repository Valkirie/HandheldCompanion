using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ControllerCommon
{
    // todo : use localization and an array
    public enum ProfileErrorCode
    {
        None = 0,
        [Description("Oops. It seems this profile does not have an executable. How is this even possible?")]
        MissingExecutable = 1,
        [Description("Oops. It seems this profile does not have a path to the application. Some options requiring an executable might be disabled.")]
        MissingPath = 2,
        [Description("Oops. It seems you do not have the necessary permission level to modify the content of this application. Make sure you have started this program in administrator mode.")]
        MissingPermission = 3,
        [Description("This is your default controller profile. This profile will be applied for all your applications that do not have a specific profile. Some options requiring an executable might be disabled.")]
        IsDefault = 4,
        [Description("Oops. It seems this profile excutable is running. Some options requiring an executable might be disabled.")]
        IsRunning = 5
    }

    // todo : use localization and an array
    public enum Input
    {
        [Description("Player space")]
        PlayerSpace = 0,
        [Description("Joystick camera")]
        JoystickCamera = 1,
        [Description("Joystick steering")]
        JoystickSteering = 2
    }

    // todo : use localization and an array
    public enum Output
    {
        [Description("Left Joystick")]
        LeftStick = 0,
        [Description("Right joystick")]
        RightStick = 1,
        /* [Description("Mouse")]
        Mouse = 2 */
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

        public static Dictionary<Input, string> InputDescription = new()
        {
            { Input.JoystickCamera, Properties.Resources.JoystickCamera },
            { Input.JoystickSteering, Properties.Resources.JoystickSteering },
            { Input.PlayerSpace, Properties.Resources.PlayerSpace }
        };

        public string name { get; set; }
        public string path { get; set; }
        public string executable { get; set; }
        public bool isEnabled { get; set; } = true;

        public bool whitelisted { get; set; } = false;              // if true, can see through the HidHide cloak
        public bool use_wrapper { get; set; } = false;              // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;                // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;            // accelerometer multiplicator (remove me)

        public int steering { get; set; } = 0;                      // 0 = Roll, 1 = Yaw
        public float antideadzone { get; set; } = 0.0f;             // todo: typeme

        public bool inverthorizontal { get; set; } = false;         // if true, invert horizontal axis
        public bool invertvertical { get; set; } = false;           // if false, invert vertical axis

        public bool umc_enabled { get; set; } = false;

        public Input umc_input { get; set; } = Input.JoystickCamera;
        public Output umc_output { get; set; } = Output.RightStick;

        // aiming
        public float aiming_sensivity { get; set; } = 2.0f;

        public List<ProfileVector> aiming_array { get; set; } = new();

        // steering
        public float steering_max_angle { get; set; } = 30.0f;
        public float steering_power { get; set; } = 1.0f;
        public float steering_deadzone { get; set; } = 0.0f;

        // flickstick
        public bool flickstick_enabled { get; set; } = false;
        public float flick_duration { get; set; } = 0.1f;
        public float stick_sensivity { get; set; } = 3.0f;

        public GamepadButtonFlagsExt umc_trigger { get; set; } = 0;

        // hidden settings
        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }
        [JsonIgnore] public string json { get; set; }
        [JsonIgnore] public bool isDefault { get; set; } = false;
        [JsonIgnore] public bool isApplied { get; set; } = false;
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
        }

        public float GetSensiviy()
        {
            return aiming_sensivity * 500.0f;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
