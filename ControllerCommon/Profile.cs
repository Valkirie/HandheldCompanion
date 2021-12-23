using Nefarius.ViGEm.Client.Targets.DualShock4;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControllerCommon
{
    public enum ProfileErrorCode
    {
        None = 0,
        MissingExecutable = 1,
        MissingPath = 2
    }

    public enum InputStyle
    {
        None = 0,
        RightStick = 1,
        LeftStick = 2,
        Mouse = 3
    }

    public enum HapticIntensity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Extreme = 3
    }

    public class ProfileButton : DualShock4Button
    {
        public ProfileButton(int id, string name, ushort value) : base(id, name, value)
        {
        }

        public static ProfileButton AlwaysOn = new ProfileButton(12, "Always On", 8);
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
        public float umc_sensivity { get; set; } = 500.0f;

        public HapticIntensity umc_intensity { get; set; } = HapticIntensity.Low;
        public int umc_trigger { get; set; } = 0;

        [JsonIgnore] public ProfileErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }

        public Profile()
        {
        }

        public Profile(string name, string path)
        {
            this.name = name;
            this.path = path;
            this.fullpath = path;
        }

        public float GetIntensity()
        {
            switch (umc_intensity)
            {
                default:
                case HapticIntensity.Low:
                    return 1.0f;
                case HapticIntensity.Medium:
                    return 0.8f;
                case HapticIntensity.High:
                    return 0.6f;
                case HapticIntensity.Extreme:
                    return 0.4f;
            }
        }

        public override string ToString()
        {
            return name;
        }

        public static Dictionary<int, DualShock4Button> ListTriggers()
        {
            return new Dictionary<int, DualShock4Button>() {
                { ProfileButton.AlwaysOn.Value, ProfileButton.AlwaysOn },

                { DualShock4Button.ThumbRight.Value, DualShock4Button.ThumbRight },
                { DualShock4Button.ThumbLeft.Value, DualShock4Button.ThumbLeft },

                { DualShock4Button.Options.Value, DualShock4Button.Options },
                { DualShock4Button.Share.Value, DualShock4Button.Share },

                { DualShock4Button.TriggerRight.Value, DualShock4Button.TriggerRight },
                { DualShock4Button.TriggerLeft.Value, DualShock4Button.TriggerLeft },

                { DualShock4Button.ShoulderRight.Value, DualShock4Button.ShoulderRight },
                { DualShock4Button.ShoulderLeft.Value, DualShock4Button.ShoulderLeft },

                { DualShock4Button.Triangle.Value, DualShock4Button.Triangle },
                { DualShock4Button.Circle.Value, DualShock4Button.Circle },
                { DualShock4Button.Cross.Value, DualShock4Button.Cross },
                { DualShock4Button.Square.Value, DualShock4Button.Square },
            };
        }
    }
}
