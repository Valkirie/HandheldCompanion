using System;
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

    public enum InputStyle
    {
        None = 0,
        RightStick = 1,
        LeftStick = 2,
        Mouse = 3
    }

    [Serializable]
    public class Profile
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; } = false;              // if true, can see through the HidHide cloak
        public bool use_wrapper { get; set; } = false;              // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;                // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;            // accelerometer multiplicator (remove me)

        public int steering { get; set; } = 0;                      // 0 = Roll, 1 = Yaw          

        public bool inverthorizontal { get; set; } = false;         // if true, invert horizontal axis
        public bool invertvertical { get; set; } = false;           // if false, invert vertical axis

        public bool umc_enabled { get; set; } = false;
        public InputStyle umc_input { get; set; } = InputStyle.None;

        public float umc_sensivity { get; set; } = 2.0f;
        public float umc_intensity { get; set; } = 2.0f;

        public uint umc_trigger { get; set; } = 0;

        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }
        [JsonIgnore] public bool IsDefault { get; set; } = false;

        public Profile()
        {
        }

        public Profile(string name, string path)
        {
            this.name = name;
            this.path = path;
            this.fullpath = path;
        }

        public float GetSensiviy()
        {
            return umc_sensivity * 500.0f;
        }

        public float GetIntensity()
        {
            return 1.0f - (umc_intensity / 20.0f) + 0.1f;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
