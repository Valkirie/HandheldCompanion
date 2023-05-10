using ControllerCommon.Inputs;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static HandheldCompanion.Managers.SystemManager;

namespace HandheldCompanion.Managers.Desktop
{
    public class ScreenResolution
    {
        public int width;
        public int height;

        public SortedDictionary<int, ScreenFrequency> frequencies;

        public ScreenResolution(int dmPelsWidth, int dmPelsHeight)
        {
            this.width = dmPelsWidth;
            this.height = dmPelsHeight;
            this.frequencies = new SortedDictionary<int, ScreenFrequency>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
        }

        public override string ToString()
        {
            return $"{width} x {height}";
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
        private SortedDictionary<Frequency, double> frequencies = new();

        public ScreenFrequency(int frequency)
        {
            this.frequencies[Frequency.Quarter] = Math.Round(frequency / 4.0d, 1);
            this.frequencies[Frequency.Third] = Math.Round(frequency / 3.0d, 1);
            this.frequencies[Frequency.Half] = Math.Round(frequency / 2.0d, 1);
            this.frequencies[Frequency.Full] = frequency;
        }

        public double GetFrequency(Frequency frequency)
        {
            return this.frequencies[frequency];
        }

        public override string ToString()
        {
            return $"{this.frequencies[Frequency.Full]} Hz";
        }

        public override bool Equals(object obj)
        {
            ScreenFrequency frequency = obj as ScreenFrequency;
            if (frequency != null)
            {
                foreach (Frequency freq in (Frequency[])Enum.GetValues(typeof(Frequency)))
                {
                    if (frequencies[freq] != frequency.frequencies[freq])
                        return false;
                }
            }

            return true;
        }
    }

    public class DesktopScreen
    {
        public List<ScreenResolution> resolutions;
        public Screen PrimaryScreen;
        public Display devMode;

        public DesktopScreen(Screen primaryScreen)
        {
            this.PrimaryScreen = primaryScreen;
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

        public ScreenFrequency GetFrequency()
        {
            return new ScreenFrequency(devMode.dmDisplayFrequency);
        }

        public void SortResolutions()
        {
            resolutions = resolutions.OrderByDescending(a => a.width).ThenByDescending(b => b.height).ToList();
        }
    }
}
