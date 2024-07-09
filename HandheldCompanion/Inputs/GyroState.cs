using System;
using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Inputs
{
    public class GyroState : ICloneable
    {
        public Dictionary<SensorState, Vector3> Accelerometer = [];
        public Dictionary<SensorState, Vector3> Gyroscope = [];

        public const byte SENSOR_MAX = 4;
        public enum SensorState
        {
            Raw,
            Default,
            GMH,
            DSU
        }

        public GyroState()
        {
            foreach (SensorState state in Enum.GetValues(typeof(SensorState)))
            {
                Accelerometer[state] = new();
                Gyroscope[state] = new();
            }
        }

        public GyroState(Dictionary<SensorState, Vector3> accelerometer, Dictionary<SensorState, Vector3> gyroscope)
        {
            foreach (SensorState state in Enum.GetValues(typeof(SensorState)))
            {
                Accelerometer[state] = accelerometer[state];
                Gyroscope[state] = gyroscope[state];
            }
        }

        public void SetAccelerometer(float aX, float aY, float aZ)
        {
            foreach (SensorState state in Enum.GetValues(typeof(SensorState)))
                Accelerometer[state] = new(aX, aY, aZ);
        }

        public void SetGyroscope(float gX, float gY, float gZ)
        {
            foreach (SensorState state in Enum.GetValues(typeof(SensorState)))
                Gyroscope[state] = new(gX, gY, gZ);
        }

        public object Clone()
        {
            return new GyroState(Accelerometer, Gyroscope);
        }
    }
}