using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace HandheldCompanion.Misc
{
    public class ColorTracker
    {
        private ConcurrentQueue<Color> colorHistory = new();

        private const int BlackBorderThreshold = 3;
        private int historySize = 4; // Number of colors to track

        public void AddColor(Color currentColor)
        {
            // Add the current color to the history
            colorHistory.Enqueue(currentColor);

            // Ensure the history does not exceed the specified size
            while (colorHistory.Count > historySize)
                colorHistory.TryDequeue(out Color result);
        }

        public void Reset()
        {
            // Reset the color history when ambilight
            // is no longer used so we start fresh next time
            colorHistory = new();
        }

        public Color CalculateAverageColor()
        {
            if (colorHistory.Count == 0)
            {
                // No colors in history, return black
                return Color.FromRgb(0, 0, 0);
            }

            // Check if the current color is black
            if (IsBlack(colorHistory.Last()))
            {
                return Color.FromRgb(0, 0, 0);
            }

            // Find the number of consecutive non-black colors in the history
            int consecutiveNonBlackCount = 0;
            foreach (Color color in colorHistory.Reverse())
            {
                if (!IsBlack(color))
                {
                    break;
                }
                consecutiveNonBlackCount++;
            }

            // Don't use black for the average color history, start from first color entry
            if (consecutiveNonBlackCount > 0)
            {
                // Use consecutive non-black colors to determine the average
                List<Color> nonBlackColors = colorHistory.Reverse().Take(consecutiveNonBlackCount).ToList();
                byte averageR = (byte)nonBlackColors.Average(c => c.R);
                byte averageG = (byte)nonBlackColors.Average(c => c.G);
                byte averageB = (byte)nonBlackColors.Average(c => c.B);
                return Color.FromRgb(averageR, averageG, averageB);
            }
            else
            {
                // If none of the previous colors were black, calculate the average of all colors
                byte averageR = (byte)colorHistory.Average(c => c.R);
                byte averageG = (byte)colorHistory.Average(c => c.G);
                byte averageB = (byte)colorHistory.Average(c => c.B);
                return Color.FromRgb(averageR, averageG, averageB);
            }
        }

        public static bool IsBlack(Color pixel)
        {
            // Determine if pixel color is black given a certain threshold
            return pixel.R <= BlackBorderThreshold
                && pixel.G <= BlackBorderThreshold
                && pixel.B <= BlackBorderThreshold;
        }
    }
}
