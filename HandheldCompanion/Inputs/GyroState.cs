using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Inputs
{
    public class GyroState : ICloneable, IDisposable
    {
        public Dictionary<SensorState, Vector3> Accelerometer = [];
        public Dictionary<SensorState, Vector3> Gyroscope = [];
        private bool _disposed = false; // Prevent multiple disposals

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

        ~GyroState()
        {
            Dispose(false);
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

                    // write it back
                    Accelerometer[state] = vector;
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

                    // write it back
                    Gyroscope[state] = vector;
                }
            }
        }

        private static ref Vector3 GetRef(Dictionary<SensorState, Vector3> dict, SensorState state)
            => ref CollectionsMarshal.GetValueRefOrNullRef(dict, state);

        /// <summary>Set all accelerometer & gyroscope entries to Vector3.Zero (no reallocs).</summary>
        public void Zero()
        {
            foreach (var s in SensorStates)
            {
                GetRef(Accelerometer, s) = Vector3.Zero;
                GetRef(Gyroscope, s) = Vector3.Zero;
            }
        }

        /// <summary>Copy values from another GyroState (no new dictionaries).</summary>
        public void CopyFrom(GyroState src)
        {
            foreach (var s in SensorStates)
            {
                GetRef(Accelerometer, s) = src.Accelerometer[s];
                GetRef(Gyroscope, s) = src.Gyroscope[s];
            }
        }

        /// <summary>Copy values from provided dictionaries (keeps our dictionaries).</summary>
        public void CopyFrom(Dictionary<SensorState, Vector3> accelerometer, Dictionary<SensorState, Vector3> gyroscope)
        {
            foreach (var s in SensorStates)
            {
                GetRef(Accelerometer, s) = accelerometer[s];
                GetRef(Gyroscope, s) = gyroscope[s];
            }
        }

        public object Clone()
        {
            return new GyroState(Accelerometer, Gyroscope);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Free managed resources
                Accelerometer?.Clear();
                Accelerometer = null;

                Gyroscope?.Clear();
                Gyroscope = null;
            }

            _disposed = true;
        }
    }
}