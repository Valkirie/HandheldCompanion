using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static HandheldCompanion.Managers.SystemManager;

namespace HandheldCompanion.Managers.Desktop;

public class ScreenResolution
{
    public SortedDictionary<int, ScreenFrequency> Frequencies;
    public int Height;
    public int Width;
    public int BitsPerPel;

    public ScreenResolution(int dmPelsWidth, int dmPelsHeight, int bitsPerPel)
    {
        Width = dmPelsWidth;
        Height = dmPelsHeight;
        BitsPerPel = bitsPerPel;
        Frequencies = new SortedDictionary<int, ScreenFrequency>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
    }

    public override string ToString()
    {
        return $"{Width} x {Height}";
    }
}

public enum Frequency
{
    Quarter = 0,
    Third = 1,
    Half = 2,
    Full = 3
}

public class ScreenFrequency
{
    private readonly SortedDictionary<Frequency, double> Frequencies = new();

    public ScreenFrequency(int frequency)
    {
        Frequencies[Frequency.Quarter] = Math.Round(frequency / 4.0d, 1);
        Frequencies[Frequency.Third] = Math.Round(frequency / 3.0d, 1);
        Frequencies[Frequency.Half] = Math.Round(frequency / 2.0d, 1);
        Frequencies[Frequency.Full] = frequency;
    }

    public double GetValue(Frequency frequency)
    {
        return Frequencies[frequency];
    }

    public override string ToString()
    {
        return $"{Frequencies[Frequency.Full]} Hz";
    }

    public override bool Equals(object obj)
    {
        if (obj is ScreenFrequency frequency)
            foreach (var freq in (Frequency[])Enum.GetValues(typeof(Frequency)))
                if (Frequencies[freq] != frequency.Frequencies[freq])
                    return false;

        return true;
    }
}

public struct ScreenRotation
{
    public enum Rotations
    {
        UNSET = -1,
        DEFAULT = 0,
        D90 = 1,
        D180 = 2,
        D270 = 3
    }

    public Rotations rotation;
    public Rotations rotationNativeBase;
    public Rotations rotationUnnormalized;

    public ScreenRotation()
    {
        rotationUnnormalized = Rotations.DEFAULT;
        rotationNativeBase = Rotations.DEFAULT;
        rotation = Rotations.DEFAULT;
    }

    public ScreenRotation(Rotations unnormalized, Rotations native)
    {
        rotationUnnormalized = unnormalized;

        if (native == Rotations.UNSET)
            rotationNativeBase = (Rotations)((4 - (int)unnormalized) % 4);
        else
            rotationNativeBase = native;

        rotation = (Rotations)(((int)unnormalized + (int)rotationNativeBase) % 4);
    }

    public static implicit operator Rotations(ScreenRotation r)
    {
        return r.rotation;
    }

    public static implicit operator ScreenOrientation(ScreenRotation r)
    {
        return (ScreenOrientation)r.rotation;
    }

    public override string ToString()
    {
        switch (rotation)
        {
            case Rotations.DEFAULT:
            case Rotations.D90:
            case Rotations.D180:
            case Rotations.D270:
                return $"{((int)rotation * 90).ToString()}°";
            default:
                return "undefined";
        }
    }
}

public class DesktopScreen
{
    public Display devMode;
    public Screen PrimaryScreen;
    public List<ScreenResolution> resolutions;

    public DesktopScreen(Screen primaryScreen)
    {
        PrimaryScreen = primaryScreen;
        resolutions = new List<ScreenResolution>();
    }

    public bool HasResolution(ScreenResolution resolution)
    {
        return resolutions.Count(a => a.Width == resolution.Width && a.Height == resolution.Height) > 0;
    }

    public ScreenResolution GetResolution(int dmPelsWidth, int dmPelsHeight)
    {
        return resolutions.FirstOrDefault(a => a.Width == dmPelsWidth && a.Height == dmPelsHeight);
    }

    public ScreenFrequency GetFrequency()
    {
        return new ScreenFrequency(devMode.dmDisplayFrequency);
    }

    public void SortResolutions()
    {
        resolutions = resolutions.OrderByDescending(a => a.Width).ThenByDescending(b => b.Height).ToList();
    }
}