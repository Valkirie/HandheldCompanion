using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        internal void AddFrequencies(List<int> _frequencies)
        {
            foreach (int frequency in _frequencies)
                frequencies.Add(new ScreenFrequency(frequency));
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

        internal ScreenResolution GetResolution(int dmPelsWidth, int dmPelsHeight)
        {
            return resolutions.Where(a => a.width == dmPelsWidth && a.height == dmPelsHeight).FirstOrDefault();
        }
    }
}
