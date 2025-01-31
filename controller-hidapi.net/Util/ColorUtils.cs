using System;
using System.Drawing;

namespace controller_hidapi.net.Util
{
    internal static class ColorUtils
    {
        public static void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            // Convert RGB values to a scale of 0 to 1
            float rScaled = color.R / 255f;
            float gScaled = color.G / 255f;
            float bScaled = color.B / 255f;

            // Find the maximum and minimum values of R, G and B
            float max = Math.Max(rScaled, Math.Max(gScaled, bScaled));
            float min = Math.Min(rScaled, Math.Min(gScaled, bScaled));
            float delta = max - min;

            // Calculate V (value/brightness) - scaled to 100%
            value = max * 100;

            // Calculate S (saturation) - scaled to 100%
            saturation = (max == 0) ? 0 : (delta / max) * 100;

            // Calculate H (hue)
            hue = color.GetHue();
            hue = hue / 360.0f * 255f;
        }
    }
}
