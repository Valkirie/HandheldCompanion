using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
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
    QuickProfilesCreation = 4,
    Creation = 8,
    Serializer = 16,
    ProfilesPageUpdateOnly = 32,
    LibraryUpdate = 64,
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
    [JsonIgnore] public ProfileErrorCode ErrorCode = ProfileErrorCode.None;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public bool IsSubProfile { get; set; }
    public bool IsFavoriteSubProfile { get; set; }

    public Guid Guid { get; set; } = Guid.NewGuid();
    public DateTime DateCreated { get; set; } = DateTime.MinValue;
    public DateTime DateModified { get; set; } = DateTime.MinValue;
    public DateTime LastUsed { get; set; } = DateTime.MinValue;

    // Library
    public LibraryEntry LibraryEntry { get; set; }

    public bool ShowInLibrary { get; set; } = true;

    public string Executable { get; set; } = string.Empty;

    public bool Enabled { get; set; }
    public bool IsPinned { get; set; } = true;
    public bool SuspendOnSleep { get; set; }

    public bool Default { get; set; }
    public Version Version { get; set; } = new();

    public string LayoutTitle { get; set; } = string.Empty;
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
    public SortedDictionary<double, double> MotionSensivityArray { get; set; } = [];

    // steering
    public float SteeringMaxAngle { get; set; } = 30.0f;
    public float SteeringPower { get; set; } = 1.0f;
    public float SteeringDeadzone { get; set; } = 0.0f;

    // Aiming down sights
    public float AimingSightsMultiplier { get; set; } = 1.0f;
    public ButtonState AimingSightsTrigger { get; set; } = new();

    // power & graphics
    public Dictionary<int, Guid> PowerProfiles { get; set; } = new()
    {
        { 0 /*PowerLineStatus.Offline*/, new() },
        { 1 /*PowerLineStatus.Online*/, new() },
    };

    public int FramerateValue { get; set; } = 0; // default RTSS value
    public bool GPUScaling { get; set; }
    public int ScalingMode { get; set; } = 0; // default AMD value
    public bool RSREnabled { get; set; }
    public int RSRSharpness { get; set; } = 20; // default AMD value
    public bool IntegerScalingEnabled { get; set; }
    public int IntegerScalingDivider { get; set; } = 1;
    public byte IntegerScalingType { get; set; } = 0;
    public bool RISEnabled { get; set; }
    public int RISSharpness { get; set; } = 80; // default AMD value
    public bool AFMFEnabled { get; set; }

    // AppCompatFlags
    public bool FullScreenOptimization { get; set; } = true;
    public bool HighDPIAware { get; set; } = true;

    // emulated controller type, default is default
    public HIDmode HID { get; set; } = HIDmode.NotSelected;

    public Profile()
    {
        // initialize aiming array
        if (MotionSensivityArray.Count == 0)
        {
            for (var i = 0; i < SensivityArraySize; i++)
            {
                var value = i / (double)(SensivityArraySize - 1);
                MotionSensivityArray[value] = 0.5f;
            }
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

        // initialize layout
        Layout.FillInherit();
        LayoutTitle = LayoutTemplate.DefaultLayout.Name;

        // enable the below variables when profile is created
        Enabled = true;
    }

    public object Clone()
    {
        return CloningHelper.DeepClone(this);
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

    public static string RemoveSpecialCharacters(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Define a set of allowed characters (letters, digits, '.', '_', and space)
        var allowedCharacters = new HashSet<char>("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._ ");
        var sanitizedString = new StringBuilder(input.Length);

        // Iterate over each character in the input string
        foreach (char character in input)
            if (allowedCharacters.Contains(character))
                sanitizedString.Append(character);

        // Return the sanitized string
        return sanitizedString.ToString();
    }

    public string GetOwnerName()
    {
        if (IsSubProfile)
            return ManagerFactory.profileManager.GetProfileForSubProfile(this).Name;
        else
            return string.Empty;
    }

    public override string ToString()
    {
        // if sub profile, return the following (mainprofile.name - subprofile.name)
        if (IsSubProfile)
        {
            string mainProfileName = ManagerFactory.profileManager.GetProfileForSubProfile(this).Name;
            return $"{mainProfileName} - {Name}";
        }
        else
            return Name;
    }
}