using System;
using System.Numerics;

namespace HandheldCompanion.Inputs
{
    public class GyroState : ICloneable
    {
        public Vector3 Accelerometer = new();
        public Vector3 Gyroscope = new();

        public GyroState()
        {
        }

        public GyroState(Vector3 accelerometer, Vector3 gyroscope)
        {
            Accelerometer = accelerometer;
            Gyroscope = gyroscope;
        }

        public object Clone()
        {
            return new GyroState(Accelerometer, Gyroscope);
        }
    }
}