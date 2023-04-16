using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers.Desktop
{
    public struct ScreenResolution
    {
        public int width;
        public int height;

        public List<ScreenFrequency> frequencies;

        public ScreenResolution(int dmPelsWidth, int dmPelsHeight) : this()
        {
            this.width = dmPelsWidth;
            this.height = dmPelsHeight;
            this.frequencies = new();
        }

        public override string ToString()
        {
            return $"{width} x {height}";
        }

        public void AddFrequencies(List<int> _frequencies)
        {
            foreach (int frequency in _frequencies)
                frequencies.Add(new ScreenFrequency(frequency));
        }

        public void SortFrequencies()
        {
            frequencies = frequencies.OrderByDescending(a => a.frequency).ToList();
        }
    }

    public struct ScreenFrequency
    {
        public int frequency;

        public ScreenFrequency(int frequency)
        {
            this.frequency = frequency;
        }

        public override string ToString()
        {
            return $"{frequency} Hz";
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
            this.rotationUnnormalized = Rotations.DEFAULT;
            this.rotationNativeBase = Rotations.DEFAULT;
            this.rotation = Rotations.DEFAULT;
        }

        public ScreenRotation(Rotations unnormalized, Rotations native)
        {
            this.rotationUnnormalized = unnormalized;

            if (native == Rotations.UNSET)
                this.rotationNativeBase = (Rotations)((4 - (int)unnormalized) % 4);
            else
                this.rotationNativeBase = native;

            this.rotation = (Rotations)(((int)unnormalized + (int)this.rotationNativeBase) % 4);
        }

        public static implicit operator Rotations(ScreenRotation r) => r.rotation;
        public static implicit operator System.Windows.Forms.ScreenOrientation(ScreenRotation r) => (System.Windows.Forms.ScreenOrientation)r.rotation;

        public override string ToString()
        {
            switch (this.rotation)
            {
                case Rotations.DEFAULT:
                case Rotations.D90:
                case Rotations.D180:
                case Rotations.D270:
                    return $"{((int)this.rotation * 90).ToString()}°";
                default:
                    return "undefined";
            }
        }
    }

    public class DesktopScreen
    {
        public List<ScreenResolution> resolutions;
        private string deviceName;

        public DesktopScreen(string deviceName)
        {
            this.deviceName = deviceName;
            this.resolutions = new();
        }

        public bool HasResolution(ScreenResolution resolution)
        {
            return resolutions.Where(a => a.width == resolution.width && a.height == resolution.height).Count() > 0;
        }

        public ScreenResolution GetResolution(int dmPelsWidth, int dmPelsHeight)
        {
            return resolutions.Where(a => a.width == dmPelsWidth && a.height == dmPelsHeight).FirstOrDefault();
        }

        public void SortResolutions()
        {
            resolutions = resolutions.OrderByDescending(a => a.width).ThenByDescending(b => b.height).ToList();
        }
    }
}
