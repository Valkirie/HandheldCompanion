using System;
using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Inputs
{
    public class GyroState : ICloneable, IDisposable
    {
        public Dictionary<SensorState, Vector3> Accelerometer = [];
        public Dictionary<SensorState, Vector3> Gyroscope = [];

        public static readonly SensorState[] SensorStates = (SensorState[])Enum.GetValues(typeof(SensorState));
        public enum SensorState
        {
            Default,
            GamepadMotion,
            DSU
        }

        public GyroState()
        {
            foreach (SensorState state in SensorStates)
            {
                Accelerometer[state] = new();
                Gyroscope[state] = new();
            }
        }

        public GyroState(Dictionary<SensorState, Vector3> accelerometer, Dictionary<SensorState, Vector3> gyroscope)
        {
            foreach (SensorState state in SensorStates)
            {
                Accelerometer[state] = accelerometer[state];
                Gyroscope[state] = gyroscope[state];
            }
        }

        public void SetAccelerometer(float aX, float aY, float aZ)
        {
            foreach (SensorState state in SensorStates)
            {
                if (Accelerometer.TryGetValue(state, out Vector3 vector))
                {
                    vector.X = aX;
                    vector.Y = aY;
                    vector.Z = aZ;
                }
            }
        }

        public void SetGyroscope(float gX, float gY, float gZ)
        {
            foreach (SensorState state in SensorStates)
            {
                if (Gyroscope.TryGetValue(state, out Vector3 vector))
                {
                    vector.X = gX;
                    vector.Y = gY;
                    vector.Z = gZ;
                }
            }
        }

        public object Clone()
        {
            return new GyroState(Accelerometer, Gyroscope);
        }

        public void Dispose()
        {
            Accelerometer.Clear();
            Accelerometer = null;
            Gyroscope.Clear();
            Gyroscope = null;

            GC.SuppressFinalize(this);
        }
    }
}