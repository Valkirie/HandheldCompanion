using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static HandheldCompanion.Managers.SystemManager;

namespace HandheldCompanion.Managers.Desktop;

public class ScreenResolution
{
    public SortedList<int, int> Frequencies;
    public int Height;
    public int Width;
    public int BitsPerPel;

    public ScreenResolution(int dmPelsWidth, int dmPelsHeight, int bitsPerPel)
    {
        Width = dmPelsWidth;
        Height = dmPelsHeight;
        BitsPerPel = bitsPerPel;
        Frequencies = new(Comparer<int>.Create((x, y) => y.CompareTo(x)));
    }

    public override string ToString()
    {
        return $"{Width} x {Height}";
    }
}

public class ScreenFramelimit
{
    public int divider;
    public int limit;

    public ScreenFramelimit(int divider, int limit)
    {
        this.divider = divider;
        this.limit = limit;
    }

    public override string ToString()
    {
        if (limit == 0)
            return "Disabled";

        return $"{limit} FPS";
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
    public List<ScreenResolution> resolutions = new();

    public DesktopScreen(Screen primaryScreen)
    {
        PrimaryScreen = primaryScreen;
    }

    public bool HasResolution(ScreenResolution resolution)
    {
        return resolutions.Count(a => a.Width == resolution.Width && a.Height == resolution.Height) > 0;
    }

    public ScreenResolution GetResolution(int dmPelsWidth, int dmPelsHeight)
    {
        // improve me
        int width = dmPelsWidth > dmPelsHeight ? dmPelsWidth : dmPelsHeight;
        int height = dmPelsWidth > dmPelsHeight ? dmPelsHeight : dmPelsWidth;

        // unreliable !
        /*
        switch(SystemInformation.ScreenOrientation)
        {
            case ScreenOrientation.Angle0:
            case ScreenOrientation.Angle180:
                width = dmPelsWidth;
                height = dmPelsHeight;
                break;
            case ScreenOrientation.Angle90:
            case ScreenOrientation.Angle270:
                height = dmPelsWidth;
                width = dmPelsHeight;
                break;
        }
        */

        return resolutions.FirstOrDefault(a => a.Width == width && a.Height == height);
    }

    public int GetCurrentFrequency()
    {
        return devMode.dmDisplayFrequency;
    }

    // A function that takes a screen frequency int value and returns a list of integer values that are the quotient of the frequency and the closest divisor
    public List<ScreenFramelimit> GetFramelimits()
    {
        // A list to store the quotients
        List<ScreenFramelimit> Limits = new(); // (Comparer<int>.Create((x, y) => y.CompareTo(x)));

        // A variable to store the divider value, initialized to 10
        int divider = 1;
        int dmDisplayFrequency = ClosestMultipleOf10(devMode.dmDisplayFrequency);

        // A loop to find the closest divisors of the frequency
        while (divider < 10)
        {
            // If the frequency is divisible by the divider, add the quotient to the list
            if (dmDisplayFrequency % divider == 0)
            {
                int frequency = dmDisplayFrequency / divider;
                if (frequency >= 20)
                    Limits.Add(new(divider, frequency));
            }

            // Decrease the divider by 1
            divider++;
        }

        // Return the list of quotients
        return Limits;
    }

    // A function that takes an int as a parameter and returns the closest multiple of 10
    private int ClosestMultipleOf10(decimal num)
    {
        // Round the number to the nearest integer
        num = (int)Math.Round(num);

        // If the number is already a multiple of 10, return it
        if (num % 10 == 0)
            return (int)num;

        // Otherwise, find the closest lower and higher multiples of 10
        decimal lower = num - (num % 10);
        decimal higher = lower + 10;

        // Return the one that has the smaller absolute difference with the number
        if (Math.Abs(num - lower) <= Math.Abs(num - higher))
            return (int)lower;
        else
            return (int)higher;
    }

    public void SortResolutions()
    {
        resolutions = resolutions.OrderByDescending(a => a.Width).ThenByDescending(b => b.Height).ToList();
    }
}