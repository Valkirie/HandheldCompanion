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

        public float umc_sensivity { get; set; } = 2.0f;
        public float umc_intensity { get; set; } = 1.0f;

        public int umc_trigger { get; set; } = 0;

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
