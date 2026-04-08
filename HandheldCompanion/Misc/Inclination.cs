using System;
using System.Numerics;

namespace HandheldCompanion.Misc
{
    public class Inclination
    {
        public Vector2 Angles = new();

        public void UpdateReport(Vector3 acceleration)
        {
            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing
            float ax = acceleration.X, ay = acceleration.Y, az = acceleration.Z;
            Angles.X = (float)(-Math.Atan(ay / Math.Sqrt(ax * ax + az * az)) * (180.0 / Math.PI));
            Angles.Y = (float)(-Math.Atan(ax / Math.Sqrt(ay * ay + az * az)) * (180.0 / Math.PI));
        }
    }
}