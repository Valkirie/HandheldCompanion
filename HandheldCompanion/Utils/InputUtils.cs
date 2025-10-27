using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HandheldCompanion.Utils
{
    public enum MotionInput
    {
        /// <summary>
        /// Local space: A gyro control method commonly used in games on devices like the Nintendo Switch and PS4.
        /// In local space, the game disregards the controller's real-world orientation and focuses solely on its angular velocity around its local axes.
        /// This approach is simple to implement, highly accurate, and works consistently regardless of player posture or environment.
        /// However, some players may find it less intuitive, especially when pitching the controller up or down.
        /// Despite this, local gyro controls are ideal for handheld devices like phones and tablets.
        /// </summary>
        LocalSpace = 0,

        /// <summary>
        /// Player space: A gyro control method that trusts the player's inputs in all 3 axes are intentional and meaningful.
        /// It allows players to play in local space, world space, or anything in between.
        /// It calculates a moment-to-moment yaw axis by combining the angular velocities in the local yaw and roll axes.
        /// The magnitude of the player's rotation is always respected and expressed in-game.
        /// It offers more freedom of movement than world space without any of its algorithmic error.
        /// However, it is not ideal for handheld/mobile due to its reliance on gravity.
        /// </summary>
        PlayerSpace = 1,

        /// <summary>
        /// World Space: Gyro controls calculate the direction of gravity to determine the player's "up" orientation.
        /// The yaw axis remains aligned with this "up" direction, regardless of the controller's physical orientation.
        /// By using the accelerometer, local space inputs are converted to world space.
        /// Players can consistently turn the camera left and right by rotating the controller relative to themselves.
        /// While more intuitive, world space controls are challenging to implement and less suitable for handheld devices.
        /// </summary>
        WorldSpace = 2,

        JoystickSteering = 3
    }

    public enum MotionOutput { Disabled, LeftStick, RightStick, MoveCursor, ScrollWheel }
    public enum MotionMode { Off, On, Toggle }

    [Flags]
    public enum DeflectionDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        Any = Left | Right | Up | Down
    }

    public enum OverlayModelMode
    {
        DualSense = 0,
        DualShock4 = 1,
        EightBitDoLite2 = 2,
        N64 = 3,
        SteamDeck = 4,
        Toy = 5,
        Xbox360 = 6,
        XboxOne = 7
    }

    public static class InputUtils
    {
        private const float SHORT_MAX_F = 32767f;
        private const float INV_SHORT_MAX = 1f / SHORT_MAX_F;
        private const float BYTE_MAX_F = 255f;
        private const float INV_USHORT_MAX = 1f / 65535f; // for mapping [-32768..32767] to [0..255]
        private const float DEG2RAD = (float)Math.PI / 180f;
        private const float RAD2DEG = 180f / (float)Math.PI;

        // ---------- Small inlined helpers ----------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max) => MathF.Min(max, MathF.Max(min, value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float rangeMap(float value, float minIn, float maxIn, float minOut, float maxOut)
        {
            float inRange = maxIn - minIn;
            float outRange = maxOut - minOut;
            return minOut + outRange * ((value - minIn) / inRange);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float deg2rad(float degrees) => degrees * DEG2RAD;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float rad2deg(float rad) => rad * RAD2DEG;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MapRange(float value, float oldMin, float oldMax, float newMin, float newMax)
        {
            if (oldMin == oldMax) throw new ArgumentException("oldMin and oldMax cannot be equal.");
            float proportion = (value - oldMin) / (oldMax - oldMin);
            return newMin + (newMax - newMin) * proportion;
        }

        // Maps [-32768..32767] -> [0..255]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte NormalizeXboxInput(float input)
        {
            input = MathF.Min(SHORT_MAX_F, MathF.Max(-32768f, input));
            float scaled = (input + 32768f) * INV_USHORT_MAX * BYTE_MAX_F;
            return (byte)MathF.Round(scaled);
        }

        // ---------- Steering helpers ----------

        public static float Steering(float DeviceAngle, float DeviceAngleMax, float ToThePowerOf, float DeadzoneAngle)
        {
            float result = AngleToJoystickPos(DeviceAngle, DeviceAngleMax, DeadzoneAngle);
            result = DirectionRespectingPowerOf(result, ToThePowerOf);
            return -(result * SHORT_MAX_F);
        }

        public static float AngleToJoystickPos(float Angle, float DeviceAngleMax, float DeadzoneAngle)
        {
            float result = ((MathF.Abs(Angle) - DeadzoneAngle) / (DeviceAngleMax - DeadzoneAngle)) * DeviceAngleMax;
            result = Clamp(result, 0f, DeviceAngleMax) / DeviceAngleMax; // no MathF.Clamp
            return (Angle < 0f) ? -result : result;
        }

        public static float DirectionRespectingPowerOf(float JoystickPos, float Power)
        {
            float result = (float)Math.Pow(Math.Abs(JoystickPos), Power);
            return (JoystickPos < 0f) ? -result : result;
        }

        // ---------- Deadzone & shaping ----------

        public static Vector2 ApplyAntiDeadzone(Vector2 ThumbValue, int DeadzonePercentage)
        {
            if (DeadzonePercentage == 0 || ThumbValue == Vector2.Zero)
                return ThumbValue;

            Vector2 stick = ThumbValue * INV_SHORT_MAX;     // [-1..1]
            float len = stick.Length();
            if (len <= 0f) return Vector2.Zero;

            float dz = DeadzonePercentage / 100f;
            float mul = ((1f - dz) * len + dz) / len;
            return stick * mul * SHORT_MAX_F;
        }

        public static float ApplyAntiDeadzone(float ThumbValue, int DeadzonePercentage, int MaxValue)
        {
            if (DeadzonePercentage == 0 || ThumbValue == 0f)
                return ThumbValue;

            float val01 = ThumbValue / MaxValue;     // [0..1]
            float dz = DeadzonePercentage / 100f;
            val01 = (1f - dz) * val01 + dz;
            return val01 * MaxValue;
        }

        public static Vector2 ImproveCircularity(Vector2 ThumbValue)
        {
            Vector2 stick = ThumbValue * INV_SHORT_MAX;
            float len = stick.Length();
            if (len <= 1f) return ThumbValue;
            float mul = 1f / len;
            return stick * mul * SHORT_MAX_F;
        }

        public static Vector2 ImproveSquare(Vector2 ThumbValue)
        {
            Vector2 stick = ThumbValue * INV_SHORT_MAX; // [-1..1]
            float len = stick.Length();
            if (len < 1e-5f) return Vector2.Zero;

            if (len > 1f)
            {
                stick /= len; // normalize
                len = 1f;
            }

            float c = stick.X;
            float s = stick.Y;
            float denom = MathF.Max(MathF.Abs(c), MathF.Abs(s));
            if (denom > 1e-5f)
            {
                float scale = len / denom;
                stick = new Vector2(c * scale, s * scale);
            }

            return stick * SHORT_MAX_F;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApplyAxisDeadzone(float value, float deadzone)
        {
            float absVal = MathF.Abs(value);
            if (absVal < deadzone) return 0f;
            float sign = MathF.Sign(value);
            float range = 1f - deadzone;
            float scaled = (absVal - deadzone) / range;
            return scaled * sign;
        }

        /// <summary>
        /// Cross/plus-shaped deadzone; deadzones are in [0..1] fractions.
        /// </summary>
        public static Vector2 CrossDeadzoneMapping(Vector2 thumbValue, float xDeadzone, float yDeadzone)
        {
            Vector2 stick = thumbValue * INV_SHORT_MAX; // [-1..1]
            float newX = ApplyAxisDeadzone(stick.X, xDeadzone);
            float newY = ApplyAxisDeadzone(stick.Y, yDeadzone);
            return new Vector2(newX, newY) * SHORT_MAX_F;
        }

        /// <summary>
        /// Convenience overload: deadzones as percentages [0..100].
        /// </summary>
        public static Vector2 CrossDeadzoneMapping(Vector2 thumbValue, int xDeadzonePercent, int yDeadzonePercent)
            => CrossDeadzoneMapping(thumbValue, xDeadzonePercent / 100f, yDeadzonePercent / 100f);

        // Triggers, inner and outer deadzone
        public static float InnerOuterDeadzone(float TriggerInput, int InnerDeadzonePercentage, int OuterDeadzonePercentage, int MaxValue)
        {
            if ((InnerDeadzonePercentage == 0 && OuterDeadzonePercentage == 0) || float.IsNaN(TriggerInput) || TriggerInput == 0f)
                return TriggerInput;

            float inner = InnerDeadzonePercentage / 100f;
            float outer = OuterDeadzonePercentage / 100f;

            float t = MathF.Abs(TriggerInput) / MaxValue; // [0..1]

            if (t <= inner) return 0f;
            if (t >= 1f - outer) return MaxValue * MathF.Sign(TriggerInput);

            float mapped = (t - inner) / (1f - inner - outer); // MapRange(t, inner, 1-outer, 0, 1)
            return mapped * MaxValue * MathF.Sign(TriggerInput);
        }

        // Inner and outer scaled radial deadzone
        public static Vector2 ThumbScaledRadialInnerOuterDeadzone(Vector2 ThumbValue, int InnerDeadzonePercentage, int OuterDeadzonePercentage)
        {
            if ((InnerDeadzonePercentage == 0 && OuterDeadzonePercentage == 0) || ThumbValue == Vector2.Zero)
                return ThumbValue;

            Vector2 stick = ThumbValue * INV_SHORT_MAX; // [-1..1]
            float len = stick.Length();

            float inner = InnerDeadzonePercentage / 100f;
            float outer = OuterDeadzonePercentage / 100f;

            if (len <= inner)
            {
                return Vector2.Zero;
            }
            else if (len >= 1f - outer)
            {
                float mul = 1f / len;
                return stick * mul * SHORT_MAX_F;
            }
            else
            {
                float invLen = 1f / len;
                Vector2 norm = stick * invLen;
                float mappedLen = (len - inner) / (1f - inner - outer); // MapRange(len, inner, 1-outer, 0, 1)
                return norm * mappedLen * SHORT_MAX_F;
            }
        }

        // ---------- Custom sensitivity ----------

        private static int SensivityIdx = 2; // average of 2 nearest points

        /// <summary>
        /// Linear-ish interpolation using the 2 closest nodes without LINQ/allocs.
        /// Nodes keys are in [0..1], values are scale factors.
        /// </summary>
        public static float ApplyCustomSensitivity(float AngularValue, float MaxValue, SortedDictionary<double, double> Nodes)
        {
            float posAbs = MathF.Abs(AngularValue / MaxValue); // [0..1]
            if (posAbs <= 0f) return 0f;
            if (posAbs >= 1f) return 1f;
            if (Nodes == null || Nodes.Count == 0) return posAbs;

            // Find the two closest keys to posAbs
            double best1Dist = double.MaxValue, best2Dist = double.MaxValue;
            double best1 = 0.0, best2 = 0.0;

            foreach (var kv in Nodes)
            {
                double d = Math.Abs(kv.Key - posAbs);
                if (d < best1Dist)
                {
                    best2Dist = best1Dist; best2 = best1;
                    best1Dist = d; best1 = kv.Value;
                }
                else if (d < best2Dist)
                {
                    best2Dist = d; best2 = kv.Value;
                }
            }

            int k = Math.Min(SensivityIdx, Math.Max(1, Nodes.Count));
            double w1 = 1.0 / (1.0 + best1Dist);
            double sum = best1 * w1;

            if (k > 1 && best2Dist < double.MaxValue)
            {
                double w2 = 1.0 / (1.0 + best2Dist);
                sum += best2 * w2;
            }

            float adjusted = (float)(sum / k);
            adjusted *= 2.0f; // keep original scaling intent (1.0 => +100%)
            return adjusted;
        }

        // ---------- Motion helpers ----------

        public static Vector2 AutoRollYawSwap(Vector3 Gravity, Vector3 AngularVelocityDeg)
        {
            float gx = Gravity.X, gy = Gravity.Y, gz = Gravity.Z;
            float len = MathF.Sqrt(gx * gx + gy * gy + gz * gz);
            if (len <= 0f) return Vector2.Zero;

            float invLen = 1f / len;
            float nx = gx * invLen, ny = gy * invLen, nz = gz * invLen;

            // -acc[1] * gyro[1] + -acc[2] * gyro[2],  gyroX passthrough
            return new Vector2(-nz * -AngularVelocityDeg.Z + -ny * -AngularVelocityDeg.Y, AngularVelocityDeg.X);
        }

        // ---------- Touch helpers ----------

        public static void TouchToDirections(int x, int y, int radius, int radialShift, out bool[] buttons)
        {
            buttons = new bool[4];
            TouchToDirections(x, y, radius, radialShift, buttons);
        }

        /// <summary>
        /// Allocation-free overload: fill a preallocated 4-length bool[].
        /// </summary>
        public static void TouchToDirections(int x, int y, int radius, int radialShift, bool[] buttons)
        {
            if (buttons == null || buttons.Length < 4)
                throw new ArgumentException("buttons must be a bool[4].");

            buttons[0] = buttons[1] = buttons[2] = buttons[3] = false;

            int r2 = radius * radius;
            int len2 = x * x + y * y;
            if (len2 < r2) return;

            // Note: original used Atan2(x, y) intentionally (coordinate system), keep it.
            int angle = (int)(MathF.Atan2(x, y) * RAD2DEG + 22.5f) + radialShift;
            if (angle < 0) angle += 360;
            if (angle == 360) angle = 0;

            switch (angle / 45)
            {
                case 0: buttons[0] = true; break;
                case 1: buttons[0] = buttons[1] = true; break;
                case 2: buttons[1] = true; break;
                case 3: buttons[1] = buttons[2] = true; break;
                case 4: buttons[2] = true; break;
                case 5: buttons[2] = buttons[3] = true; break;
                case 6: buttons[3] = true; break;
                case 7: buttons[3] = buttons[0] = true; break;
            }
        }

        // ---------- Quaternion/Euler ----------

        public static Vector3 ToEulerAngles(System.Windows.Media.Media3D.Quaternion q)
        {
            // Store the Euler angles in radians
            var pitchYawRoll = new Vector3();

            double sqw = q.W * q.W;
            double sqx = q.X * q.X;
            double sqy = q.Y * q.Y;
            double sqz = q.Z * q.Z;

            var unit = sqx + sqy + sqz + sqw;
            double test = q.X * q.Y + q.Z * q.W;

            if (test > 0.4999f * unit) // 0.4999f OR 0.5f - EPSILON
            {
                pitchYawRoll.Y = 2f * (float)Math.Atan2(q.X, q.W); // Yaw
                pitchYawRoll.X = (float)Math.PI * 0.5f;            // Pitch
                pitchYawRoll.Z = 0f;                               // Roll
                return pitchYawRoll;
            }

            if (test < -0.4999f * unit) // -0.4999f OR -0.5f + EPSILON
            {
                pitchYawRoll.Y = -2f * (float)Math.Atan2(q.X, q.W); // Yaw
                pitchYawRoll.X = -(float)Math.PI * 0.5f;            // Pitch
                pitchYawRoll.Z = 0f;                                // Roll
                return pitchYawRoll;
            }

            pitchYawRoll.Y = (float)Math.Atan2(2f * q.Y * q.W - 2f * q.X * q.Z, sqx - sqy - sqz + sqw); // Yaw
            pitchYawRoll.X = (float)Math.Asin(2f * test / unit);                                        // Pitch
            pitchYawRoll.Z = (float)Math.Atan2(2f * q.X * q.W - 2f * q.Y * q.Z, -sqx + sqy - sqz + sqw);// Roll

            return pitchYawRoll;
        }

        // ---------- Direction helpers ----------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DeflectionDirection GetDeflectionDirection(Vector2 vector, float threshold)
        {
            if (vector.LengthSquared() < threshold * threshold)
            {
                return 0;
            }

            double angle = Math.Atan2(vector.Y, vector.X) * (180.0 / Math.PI);
            double shiftedAngle = angle + 22.5;
            if (shiftedAngle < 0)
            {
                shiftedAngle += 360;
            }

            int sector = (int)(shiftedAngle / 45.0);

            var directions = new[]
            {
                DeflectionDirection.Right,
                DeflectionDirection.Up | DeflectionDirection.Right,
                DeflectionDirection.Up,
                DeflectionDirection.Up | DeflectionDirection.Left,
                DeflectionDirection.Left,
                DeflectionDirection.Down | DeflectionDirection.Left,
                DeflectionDirection.Down,
                DeflectionDirection.Down | DeflectionDirection.Right
            };

            return directions[sector];
        }
    }
}