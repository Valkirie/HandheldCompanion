using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using System.Text.Json.Serialization;

namespace ControllerCommon
{
    public enum ProfileErrorCode
    {
        None = 0,
        MissingExecutable = 1,
        MissingPath = 2,
        MissingPermission
    }

    public enum Input
    {
        [Description("Joystick camera")]
        JoystickCamera = 0,
        [Description("Joystick steering")]
        JoystickSteering = 1
    }

    public enum Output
    {
        [Description("Right joystick")]
        RightStick = 0,
        [Description("Left Joystick")]
        LeftStick = 1,
        /* [Description("Mouse")]
        Mouse = 2 */
    }

    [Serializable]
    public class ProfileVector
    {
        public int x { get; set; }
        public double y { get; set; }

        public ProfileVector(int x, double y)
        {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public class Profile
    {
        public string name { get; set; }
        public string path { get; set; }
        public string executable { get; set; }

        public bool whitelisted { get; set; } = false;              // if true, can see through the HidHide cloak
        public bool use_wrapper { get; set; } = false;              // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;                // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;            // accelerometer multiplicator (remove me)

        public int steering { get; set; } = 0;                      // 0 = Roll, 1 = Yaw          

        public bool inverthorizontal { get; set; } = false;         // if true, invert horizontal axis
        public bool invertvertical { get; set; } = false;           // if false, invert vertical axis

        public bool umc_enabled { get; set; } = false;

        public Input umc_input { get; set; } = Input.JoystickCamera;
        public Output umc_output { get; set; } = Output.RightStick;

        // aiming
        public float aiming_sensivity { get; set; } = 2.0f;
        public float aiming_intensity { get; set; } = 2.0f;

        public List<ProfileVector> aiming_array { get; set; } = new();

        // steering
        public float steering_max_angle { get; set; } = 30.0f;
        public float steering_power { get; set; } = 1.0f;
        public float steering_deadzone { get; set; } = 0.0f;
        public float steering_deadzone_compensation { get; set; } = 0.0f;

        public GamepadButtonFlags umc_trigger { get; set; } = 0;

        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }
        [JsonIgnore] public bool IsDefault { get; set; } = false;
        [JsonIgnore] public static int array_size = 12;

        public Profile()
        {
            if (aiming_array.Count == 0)
            {
                for (int i = 1; i <= array_size; i++)
                {
                    ProfileVector vector = new ProfileVector(i, i / 10.0f);
                    aiming_array.Add(vector);
                }
            }
        }

        public Profile(string path)
        {
            Dictionary<string, string> AppProperties = Utils.GetAppProperties(path);

            string ProductName = AppProperties.ContainsKey("FileDescription") ? AppProperties["FileDescription"] : AppProperties["ItemFolderNameDisplay"];
            string Version = AppProperties.ContainsKey("FileVersion") ? AppProperties["FileVersion"] : "1.0.0.0";
            string Company = AppProperties.ContainsKey("Company") ? AppProperties["Company"] : AppProperties.ContainsKey("Copyright") ? AppProperties["Copyright"] : "Unknown";

            this.executable = AppProperties["FileName"];
            this.name = ProductName;
            this.path = this.fullpath = path;
        }

        public Profile(string name, string path)
        {
            this.executable = Path.GetFileName(path);
            this.name = name;
            this.path = this.fullpath = path;
        }

        public float GetSensiviy()
        {
            return aiming_sensivity * 500.0f;
        }

        public float GetIntensity()
        {
            return 1.0f - (aiming_intensity / 20.0f) + 0.1f;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
