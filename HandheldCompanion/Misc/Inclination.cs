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
            var angle_x_psi = -1 * Math.Atan(acceleration.Y / Math.Sqrt(Math.Pow(acceleration.X, 2) + Math.Pow(acceleration.Z, 2))) * 180 /
                              Math.PI;
            var angle_y_theta = -1 * Math.Atan(acceleration.X / Math.Sqrt(Math.Pow(acceleration.Y, 2) + Math.Pow(acceleration.Z, 2))) *
                180 / Math.PI;

            Angles.X = (float)angle_x_psi;
            Angles.Y = (float)angle_y_theta;
        }
    }
}