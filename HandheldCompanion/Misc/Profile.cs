using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using static HandheldCompanion.Utils.XInputPlusUtils;

namespace HandheldCompanion;

[Flags]
public enum ProfileErrorCode
{
    None = 0,
    MissingExecutable = 1,
    MissingPath = 2,
    MissingPermission = 4,
    Default = 8,
    Running = 16
}

[Flags]
public enum UpdateSource
{
    Background = 0,
    ProfilesPage = 1,
    QuickProfilesPage = 2,
    QuickProfilesCreation = 3,
    Creation = 4,
    Serializer = 5,
    ProfilesPageUpdateOnly = 6
}

public enum SteeringAxis
{
    Roll = 0,
    Yaw = 1,
    Auto = 2, // unused
}

[Serializable]
public partial class Profile : ICloneable, IComparable
{
    [JsonIgnore] public const int SensivityArraySize = 49; // x + 1 (hidden)

    public ProfileErrorCode ErrorCode = ProfileErrorCode.None;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public bool IsSubProfile { get; set; } = false;
    public bool IsFavoriteSubProfile { get; set; } = false;

    public Guid Guid { get; set; } = Guid.NewGuid();
    public string Executable { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public bool Default { get; set; }
    public Version Version { get; set; } = new();

    public string LayoutTitle { get; set; } = string.Empty;
    public bool LayoutEnabled { get; set; } = false;
    public Layout Layout { get; set; } = new();

    public bool Whitelisted { get; set; } // if true, can see through the HidHide cloak

    public XInputPlusMethod XInputPlus { get; set; } // if true, deploy xinput1_3.dll

    public float GyrometerMultiplier { get; set; } = 1.0f; // gyroscope multiplicator (remove me)
    public float AccelerometerMultiplier { get; set; } = 1.0f; // accelerometer multiplicator (remove me)

    public SteeringAxis SteeringAxis { get; set; } = SteeringAxis.Roll;

    public bool MotionInvertHorizontal { get; set; } // if true, invert horizontal axis
    public bool MotionInvertVertical { get; set; } // if false, invert vertical axis
    public float MotionSensivityX { get; set; } = 1.0f;
    public float MotionSensivityY { get; set; } = 1.0f;
    public SortedDictionary<double, double> MotionSensivityArray { get; set; } = new();

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

    // power & graphics
    public Guid PowerProfile { get; set; } = new();
    public int FramerateValue { get; set; } = 0; // default RTSS value
    public bool GPUScaling { get; set; } = false;
    public int ScalingMode { get; set; } = 0; // default AMD value
    public bool RSREnabled { get; set; } = false;
    public int RSRSharpness { get; set; } = 20; // default AMD value
    public bool IntegerScalingEnabled { get; set; } = false;
    public int IntegerScalingDivider { get; set; } = 1;
    public byte IntegerScalingType { get; set; } = 0;
    public bool RISEnabled { get; set; } = false;
    public int RISSharpness { get; set; } = 80; // default AMD value

    // AppCompatFlags
    public bool FullScreenOptimization { get; set; } = true;
    public bool HighDPIAware { get; set; } = true;

    // emulated controller type, default is default
    public HIDmode HID { get; set; } = HIDmode.NotSelected;

    public Profile()
    {
        // initialize aiming array
        if (MotionSensivityArray.Count == 0)
            for (var i = 0; i < SensivityArraySize; i++)
            {
                var value = i / (double)(SensivityArraySize - 1);
                MotionSensivityArray[value] = 0.5f;
            }
    }

    public Profile(string path) : this()
    {
        if (!string.IsNullOrEmpty(path))
        {
            Dictionary<string, string> AppProperties = ProcessUtils.GetAppProperties(path);

            string ProductName = AppProperties.TryGetValue("FileDescription", out var property) ? property : AppProperties["ItemFolderNameDisplay"];
            // string Version = AppProperties.ContainsKey("FileVersion") ? AppProperties["FileVersion"] : "1.0.0.0";
            // string Company = AppProperties.ContainsKey("Company") ? AppProperties["Company"] : AppProperties.ContainsKey("Copyright") ? AppProperties["Copyright"] : "Unknown";

            Executable = System.IO.Path.GetFileName(path);
            Name = string.IsNullOrEmpty(ProductName) ? Executable : ProductName;
            Path = path;
        }

        // enable the below variables when profile is created
        Enabled = true;
    }

    public object Clone()
    {
        var jsonString = JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        return JsonConvert.DeserializeObject<Profile>(jsonString,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
    }

    public int CompareTo(object obj)
    {
        var profile = (Profile)obj;
        return profile.Name.CompareTo(Name);
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
        var name = Name;

        if (!Default)
            name = System.IO.Path.GetFileNameWithoutExtension(Executable);

        // sub profile files will be of form "executable - #guid"
        if (IsSubProfile)
            name = $"{name} - {Guid}";

        return $"{name}.json";
    }

    public override string ToString()
    {
        // if sub profile, return the following (mainprofile.name - subprofile.name)
        if (IsSubProfile)
        {
            string mainProfileName = ProfileManager.GetProfileForSubProfile(this).Name;
            return $"{mainProfileName} - {Name}";
        }
        else
            return Name;
    }
}