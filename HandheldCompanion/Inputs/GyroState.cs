using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HandheldCompanion.Inputs
{
    public sealed class GyroState : ICloneable
    {
        public enum SensorState
        {
            Default,
            GamepadMotion,
            DSU
        }

        public static readonly SensorState[] SensorStates =
            (SensorState[])Enum.GetValues(typeof(SensorState));

        private readonly Vector3[] _accelerometer = new Vector3[SensorStates.Length];
        private readonly Vector3[] _gyroscope = new Vector3[SensorStates.Length];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(SensorState s) => (int)s;

        public void SetAccelerometer(float ax, float ay, float az)
        {
            var v = new Vector3(ax, ay, az);
            for (int i = 0; i < _accelerometer.Length; i++) _accelerometer[i] = v;
        }

        public void SetGyroscope(float gx, float gy, float gz)
        {
            var v = new Vector3(gx, gy, gz);
            for (int i = 0; i < _gyroscope.Length; i++) _gyroscope[i] = v;
        }

        public void SetAccelerometer(SensorState s, float ax, float ay, float az) => _accelerometer[Idx(s)] = new Vector3(ax, ay, az);
        public void SetGyroscope(SensorState s, float gx, float gy, float gz) => _gyroscope[Idx(s)] = new Vector3(gx, gy, gz);

        public void SetGyroscope(SensorState s, Vector3 v) => _gyroscope[(int)s] = v;
        public void SetAccelerometer(SensorState s, Vector3 v) => _accelerometer[(int)s] = v;

        public Vector3 GetAccelerometer(SensorState s) => _accelerometer[Idx(s)];
        public Vector3 GetGyroscope(SensorState s) => _gyroscope[Idx(s)];

        public ref Vector3 GetGyroscopeRef(SensorState s) => ref _gyroscope[(int)s];
        public ref Vector3 GetAccelerometerRef(SensorState s) => ref _accelerometer[(int)s];


        public void Zero()
        {
            Array.Clear(_accelerometer, 0, _accelerometer.Length);
            Array.Clear(_gyroscope, 0, _gyroscope.Length);
        }

        public void CopyFrom(GyroState src)
        {
            Array.Copy(src._accelerometer, _accelerometer, _accelerometer.Length);
            Array.Copy(src._gyroscope, _gyroscope, _gyroscope.Length);
        }

        public object Clone()
        {
            var g = new GyroState();
            g.CopyFrom(this);
            return g;
        }
    }
}