using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ControllerCommon.Utils
{
    public struct SensorSpec
    {
        public float minIn;
        public float maxIn;
        public float minOut;
        public float maxOut;
    }

    public enum MotionInput
    {
        PlayerSpace = 0,
        JoystickCamera = 1,
        AutoRollYawSwap = 2,
        JoystickSteering = 3,
    }

    public enum MotionMode
    {
        Off = 0,
        On = 1
    }

    public enum MotionOutput
    {
        LeftStick = 0,
        RightStick = 1,
        /* [Description("Mouse")]
        Mouse = 2 */
    }

    public enum OverlayModelMode
    {
        OEM = 0,
        Virtual = 1,
        XboxOne = 2,
        ZDOPlus = 3,
        EightBitDoLite2 = 4,
        MachenikeHG510 = 5,
        Toy = 6,
        N64 = 7,
        DualSense = 8,
    }

    public static class InputUtils
    {
        public static float rangeMap(float value, SensorSpec spec)
        {
            float inRange = spec.maxIn - spec.minIn;
            float outRange = spec.maxOut - spec.minOut;

            return spec.minOut + outRange * ((value - spec.minIn) / inRange);
        }

        public static float Deg2Rad(float degrees)
        {
            return (float)((Math.PI / 180) * degrees);
        }

        public static float Rad2Deg(float rad)
        {
            return (float)(rad * (180 / Math.PI));
        }

        public static float MapRange(float value, float oldMin, float oldMax, float newMin, float newMax)
        {
            if (oldMin == oldMax)
            {
                // Prevent division by zero
                return newMin;
            }

            return newMin + (newMax - newMin) * (value - oldMin) / (oldMax - oldMin);
        }

        public static byte NormalizeXboxInput(float input)
        {
            input = Math.Clamp(input, short.MinValue, short.MaxValue);
            float output = input / ushort.MaxValue * byte.MaxValue + (byte.MaxValue / 2.0f);
            return (byte)Math.Round(output);
        }

        public static float Steering(float deviceAngle, float deviceAngleMax, float toThePowerOf, float deadzoneAngle)
        {
            // Range angle y value (0 to user defined angle) into -1.0 to 1.0 position value taking into account deadzone angle
            float result = AngleToJoystickPos(deviceAngle, deviceAngleMax, deadzoneAngle);

            // Apply user-defined to the power of to joystick position
            result = DirectionRespectingPowerOf(result, toThePowerOf);

            // Scale joystick x position -1 to 1 to joystick range
            return -result * short.MaxValue;
        }

        // Determine -1 to 1 joystick position given user defined max input angle and dead zone
        // Angles in degrees
        public static float AngleToJoystickPos(float angle, float deviceAngleMax, float deadzoneAngle)
        {
            // Deadzone remapped angle, note this angle is no longer correct with device angle
            float result = ((Math.Abs(angle) - deadzoneAngle) / (deviceAngleMax - deadzoneAngle)) * deviceAngleMax;

            // Clamp deadzone remapped angle, prevents negative values when
            // actual device angle is below dead zone angle
            // Divide by max angle, angle to joystick position with user max
            result = Math.Clamp(result, 0, deviceAngleMax) / deviceAngleMax;

            // Apply the direction based on the original angle
            return (angle < 0f) ? -result : result;
        }

        // Apply power of to -1 to 1 joystick position while respecting direction
        public static float DirectionRespectingPowerOf(float joystickPos, float power)
        {
            float result = (float)Math.Pow(Math.Abs(joystickPos), power);

            // Apply direction based on the the original joystick position
            return (joystickPos < 0.0) ? -result : result;
        }

        // Compensation for in game deadzone
        // Inputs: raw ThumbValue and deadzone 0-100%
        // Should not be used under normal circumstances, in game should be set to 0% if possible. Results in loss of resolution.
        // Use cases foreseen:
        // - Game has deadzone, but no way to configure or change it
        // - User does not want to change general emulator deadzone setting but want's it removed for specific game and use UMC Steering
        public static Vector2 ApplyAntiDeadzone(Vector2 ThumbValue, float DeadzonePercentage)
        {
            // Return if thumbstick or anti deadzone is not used
            if (DeadzonePercentage.Equals(0.0f) || ThumbValue == Vector2.Zero)
                return ThumbValue;

            // Convert short value input to -1 to 1
            Vector2 StickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

            // Convert 0-100% to 0 to 1
            float Deadzone = DeadzonePercentage / 100;

            // Map vector to new range by determining the multiplier
            float Multiplier = ((1 - Deadzone) * StickInput.Length() + Deadzone) / StickInput.Length();

            // Convert -1 to 1 back to short value and return
            return StickInput * Multiplier * short.MaxValue;
        }

        public static float ApplyAntiDeadzone(float ThumbValue, float DeadzonePercentage)
        {
            float StickInput = ThumbValue / short.MaxValue;

            if (DeadzonePercentage.Equals(0.0f) || StickInput <= DeadzonePercentage)
                return ThumbValue;

            float Deadzone = DeadzonePercentage / 100 * Math.Sign(ThumbValue);
            return (StickInput + Deadzone) * short.MaxValue;
        }

        public static Vector2 ImproveCircularity(Vector2 ThumbValue)
        {
            // Convert short value input to -1 to 1
            Vector2 StickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

            // Return if length is not longer then 1
            if (StickInput.Length() <= 1.0f)
                return ThumbValue;

            // Cap vector length to 1 by determining the multiplier
            float Multiplier = 1 / StickInput.Length();

            // Convert -1 to 1 back to short value and return
            return StickInput * Multiplier * short.MaxValue;
        }

        // Triggers, inner and outer deadzone
        public static float InnerOuterDeadzone(float TriggerInput, int InnerDeadzonePercentage, int OuterDeadzonePercentage, int MaxValue)
        {
            // Return if thumbstick or deadzone is not used
            if ((InnerDeadzonePercentage.Equals(0) && OuterDeadzonePercentage.Equals(0)) || TriggerInput.Equals(float.NaN) || TriggerInput.Equals(0.0f))
                return TriggerInput;

            // Convert deadzone percentage to 0 - 1 range
            float InnerDeadZone = (float)InnerDeadzonePercentage / 100.0f;
            float OuterDeadZone = (float)OuterDeadzonePercentage / 100.0f;

            // Convert 0 - MaxValue range value input to -1 to 1
            float Trigger = Math.Abs(TriggerInput / MaxValue);

            // Trigger is either:
            // - Within inner deadzone, return 0
            // - Within outer deadzone, return max
            // - In between deadzone values, map accordingly
            if (Trigger <= InnerDeadZone)
            {
                return 0.0f;
            }
            else if (Trigger >= 1 - OuterDeadZone)
            {
                return MaxValue * Math.Sign(TriggerInput);
            }
            else
            {
                // Map to new range
                // Convert back to 0 - MaxValue range
                // Cut off float remains
                return (int)(MapRange(Trigger, InnerDeadZone, (1 - OuterDeadZone), 0, 1) * MaxValue * Math.Sign(TriggerInput));
            }
        }

        // Inner and outer scaled radial deadzone
        public static Vector2 ThumbScaledRadialInnerOuterDeadzone(Vector2 ThumbValue, int InnerDeadzonePercentage, int OuterDeadzonePercentage)
        {
            // Return if thumbstick or deadzone is not used
            if ((InnerDeadzonePercentage.Equals(0) && OuterDeadzonePercentage.Equals(0)) || ThumbValue == Vector2.Zero)
                return ThumbValue;

            // Convert short value input to -1 to 1
            Vector2 StickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

            // Convert deadzone percentage to 0 - 1 range
            float InnerDeadZone = (float)InnerDeadzonePercentage / 100.0f;
            float OuterDeadZone = (float)OuterDeadzonePercentage / 100.0f;

            // Joystick is either:
            // - Within inner deadzone, return 0
            // - Within outer deadzone, return max
            // - In between deadzone values, map accordingly
            if (StickInput.Length() <= InnerDeadZone)
            {
                return Vector2.Zero;
            }
            else if (StickInput.Length() >= 1 - OuterDeadZone)
            {
                // Cap vector length to 1 by determining the multiplier
                float Multiplier = 1 / StickInput.Length();

                // Convert -1 to 1 back to short value and return
                return StickInput * Multiplier * short.MaxValue;
            }
            else
            {
                // Normalize values, used for direction signs
                Vector2 StickValueNormalized = StickInput / StickInput.Length();

                // Map to new range
                Vector2 StickInputMapped = StickValueNormalized * MapRange(StickInput.Length(), InnerDeadZone, (1 - OuterDeadZone), 0, 1);

                // Return and convert from 0 1 range back to short
                return StickInputMapped * short.MaxValue;
            }
        }

        // Custom sensitivity
        // Interpolation function (linear), takes list of nodes coordinates and gamepad joystick position returns game input
        private static int SensivityIdx = 2;
        public static float ApplyCustomSensitivity(float AngularValue, float MaxValue, SortedDictionary<double, double> Nodes)
        {
            // Use absolute joystick position, range -1 to 1, re-apply direction later
            float JoystickPosAbs = (float)Math.Abs(AngularValue / MaxValue);
            float JoystickPosAdjusted = 0.0f;

            // Check what we will be sending
            if (JoystickPosAbs <= 0)
            {
                // Send 0 output to game
                JoystickPosAdjusted = 0.0f;
            }
            else if (JoystickPosAbs >= 1)
            {
                // Send 1 output to game
                JoystickPosAdjusted = 1.0f;
            }
            // Calculate custom sensitivty
            else
            {
                var closests = Nodes.Select(n => new { n, distance = Math.Abs(n.Key - JoystickPosAbs) }).OrderBy(p => p.distance).Take(SensivityIdx);
                foreach (var item in closests)
                    JoystickPosAdjusted += (float)(item.n.Value / (1.0f + item.distance));

                JoystickPosAdjusted /= SensivityIdx;
                JoystickPosAdjusted *= 2.0f; // a 1.0f vector means a 100% increase
            }

            // Apply direction
            return JoystickPosAdjusted;
        }

        public static Vector2 AutoRollYawSwap(Vector3 Gravity, Vector3 AngularVelocityDeg)
        {
            // Auto roll yaw swap function allows for clampshell, laptop and controller type devices to
            // automatically change the roll and yaw axis depending on how the device is being held. No need for changing settings.

            // Depending on how a device is being held, one of the gravity vector values will be near 1 and the others near 0
            // multiplying this with the respective desired rotational angle speed vector (roll or yaw) will result in a motion input
            // for the horizontal plane. 

            // Normalize gravity to:
            // - Prevent multiplying with values > 1 ie additional user shaking
            // - When rolling device and maintaining the roll angle, accelY and accelZare less than horizon angle.
            Vector3 GravityNormalized = Vector3.Normalize(new Vector3(Gravity.X, Gravity.Y, Gravity.Z));

            // Handle NaN, check for empty inputs, prevent NaN computes          
            Vector3 EmptyVector = new(0f, 0f, 0f);

            if (Gravity.Equals(EmptyVector))
                return new Vector2(EmptyVector.X, EmptyVector.Y);

            // -acc[1] * gyro[1] + -acc[2] * gyro[2]
            return new Vector2(-GravityNormalized.Z * -AngularVelocityDeg.Z + -GravityNormalized.Y * -AngularVelocityDeg.Y,
                               AngularVelocityDeg.X);
        }
    }
}
