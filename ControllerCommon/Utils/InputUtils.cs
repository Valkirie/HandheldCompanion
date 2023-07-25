using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ControllerCommon.Utils;

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
    JoystickSteering = 3
}

public enum MotionMode
{
    Off = 0,
    On = 1
}

public enum MotionOutput
{
    LeftStick = 0,

    RightStick = 1
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
    DualSense = 8
}

public static class InputUtils
{
    // Custom sensitivity
    // Interpolation function (linear), takes list of nodes coordinates and gamepad joystick position returns game input
    private static readonly int SensivityIdx = 2;

    public static float rangeMap(float value, SensorSpec spec)
    {
        var inRange = spec.maxIn - spec.minIn;
        var outRange = spec.maxOut - spec.minOut;

        return spec.minOut + outRange * ((value - spec.minIn) / inRange);
    }

    public static float Deg2Rad(float degrees)
    {
        return (float)(Math.PI / 180 * degrees);
    }

    public static float Rad2Deg(float rad)
    {
        return (float)(rad * (180 / Math.PI));
    }

    public static float MapRange(float value, float oldMin, float oldMax, float newMin, float newMax)
    {
        if (oldMin == oldMax)
            // Prevent division by zero
            return newMin;

        return newMin + (newMax - newMin) * (value - oldMin) / (oldMax - oldMin);
    }

    public static byte NormalizeXboxInput(float input)
    {
        input = Math.Clamp(input, short.MinValue, short.MaxValue);
        var output = input / ushort.MaxValue * byte.MaxValue + byte.MaxValue / 2.0f;
        return (byte)Math.Round(output);
    }

    public static float Steering(float deviceAngle, float deviceAngleMax, float toThePowerOf, float deadzoneAngle)
    {
        // Range angle y value (0 to user defined angle) into -1.0 to 1.0 position value taking into account deadzone angle
        var result = AngleToJoystickPos(deviceAngle, deviceAngleMax, deadzoneAngle);

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
        var result = (Math.Abs(angle) - deadzoneAngle) / (deviceAngleMax - deadzoneAngle) * deviceAngleMax;

        // Clamp deadzone remapped angle, prevents negative values when
        // actual device angle is below dead zone angle
        // Divide by max angle, angle to joystick position with user max
        result = Math.Clamp(result, 0, deviceAngleMax) / deviceAngleMax;

        // Apply the direction based on the original angle
        return angle < 0f ? -result : result;
    }

    // Apply power of to -1 to 1 joystick position while respecting direction
    public static float DirectionRespectingPowerOf(float joystickPos, float power)
    {
        var result = (float)Math.Pow(Math.Abs(joystickPos), power);

        // Apply direction based on the the original joystick position
        return joystickPos < 0.0 ? -result : result;
    }

    // Compensation for in-game deadzone
    // Inputs: raw ThumbValue and deadzone 0-100%
    // Should not be used under normal circumstances, in-game deadzone should be set to 0% if possible. Results in loss of resolution.
    // Use cases foreseen:
    // - Game has a deadzone but no way to configure or change it
    // - User does not want to change general emulator deadzone setting but wants it removed for a specific game and use UMC Steering
    public static Vector2 ApplyAntiDeadzone(Vector2 thumbValue, float deadzonePercentage)
    {
        // Return thumbValue if deadzone percentage is 0 or thumbValue is already zero
        if (deadzonePercentage.Equals(0.0f) || thumbValue == Vector2.Zero)
            return thumbValue;

        // Convert short value input to -1 to 1 range
        var stickInput = thumbValue / short.MaxValue;

        // Convert 0-100% deadzone to 0-1 range
        var deadzone = deadzonePercentage / 100f;

        // Map vector to new range by determining the multiplier 
        var multiplier = ((1f - deadzone) * stickInput.Length() + deadzone) / stickInput.Length();

        // Convert -1 to 1 back to short value and return
        return stickInput * multiplier * short.MaxValue;
    }

    public static float ApplyAntiDeadzone(float ThumbValue, int DeadzonePercentage, int MaxValue)
    {
        if (DeadzonePercentage == 0 || ThumbValue == 0.0f)
            return ThumbValue;

        // Convert MaxValue value input to 0 to 1
        float StickInput = ThumbValue / MaxValue;

        // Convert 0-100% to 0 to 1
        float Deadzone = DeadzonePercentage / 100.0f;

        // Calculate the new value
        StickInput = ((1 - Deadzone) * StickInput + Deadzone);

        // Convert 0 to 1 back to MaxValue value and return
        return StickInput * MaxValue;
    }

    public static Vector2 ImproveCircularity(Vector2 thumbValue)
    {
        // Convert short value input to -1 to 1 range
        var stickInput = thumbValue / short.MaxValue;

        // Return thumbValue if length is not longer than 1
        if (stickInput.LengthSquared() <= 1.0f)
            return thumbValue;

        // Cap vector length to 1 by normalizing it
        var normalizedInput = Vector2.Normalize(stickInput);

        // Convert -1 to 1 back to short value and return
        return normalizedInput * short.MaxValue;
    }

    // Triggers, inner and outer deadzone
    public static float InnerOuterDeadzone(float triggerInput, int innerDeadzonePercentage, int outerDeadzonePercentage,
        int maxValue)
    {
        // Return triggerInput if both inner and outer deadzones are 0 or if triggerInput is NaN or 0
        if ((innerDeadzonePercentage == 0 && outerDeadzonePercentage == 0) || float.IsNaN(triggerInput) ||
            triggerInput == 0.0f)
            return triggerInput;

        // Convert deadzone percentages to the 0-1 range
        var innerDeadzone = innerDeadzonePercentage / 100.0f;
        var outerDeadzone = outerDeadzonePercentage / 100.0f;

        // Convert 0-MaxValue range input to -1 to 1
        var trigger = Math.Abs(triggerInput / maxValue);

        // Trigger is either:
        // - Within inner deadzone, return 0
        // - Within outer deadzone, return max
        // - In between deadzone values, map accordingly
        if (trigger <= innerDeadzone)
            return 0.0f;
        if (trigger >= 1.0f - outerDeadzone)
            return triggerInput > 0 ? maxValue : -maxValue;
        // Map trigger to the new range and convert back to 0-MaxValue range
        return MapRange(trigger, innerDeadzone, 1.0f - outerDeadzone, 0, 1) * maxValue * Math.Sign(triggerInput);
    }

    // Inner and outer scaled radial deadzone
    public static Vector2 ThumbScaledRadialInnerOuterDeadzone(Vector2 ThumbValue, int InnerDeadzonePercentage,
        int OuterDeadzonePercentage)
    {
        // Return if thumbstick or deadzone is not used
        if ((InnerDeadzonePercentage.Equals(0) && OuterDeadzonePercentage.Equals(0)) || ThumbValue == Vector2.Zero)
            return ThumbValue;

        // Convert short value input to -1 to 1
        var StickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

        // Convert deadzone percentage to 0 - 1 range
        var InnerDeadZone = InnerDeadzonePercentage / 100.0f;
        var OuterDeadZone = OuterDeadzonePercentage / 100.0f;

        // Joystick is either:
        // - Within inner deadzone, return 0
        // - Within outer deadzone, return max
        // - In between deadzone values, map accordingly
        if (StickInput.Length() <= InnerDeadZone) return Vector2.Zero;

        if (StickInput.Length() >= 1 - OuterDeadZone)
        {
            // Cap vector length to 1 by determining the multiplier
            var Multiplier = 1 / StickInput.Length();

            // Convert -1 to 1 back to short value and return
            return StickInput * Multiplier * short.MaxValue;
        }

        // Normalize values, used for direction signs
        var StickValueNormalized = StickInput / StickInput.Length();

        // Map to new range
        var StickInputMapped =
            StickValueNormalized * MapRange(StickInput.Length(), InnerDeadZone, 1 - OuterDeadZone, 0, 1);

        // Return and convert from 0 1 range back to short
        return StickInputMapped * short.MaxValue;
    }

    public static float ApplyCustomSensitivity(float AngularValue, float MaxValue,
        SortedDictionary<double, double> Nodes)
    {
        // Use absolute joystick position, range -1 to 1, re-apply direction later
        var JoystickPosAbs = Math.Abs(AngularValue / MaxValue);
        var JoystickPosAdjusted = 0.0f;

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
            var closests = Nodes.Select(n => new { n, distance = Math.Abs(n.Key - JoystickPosAbs) })
                .OrderBy(p => p.distance).Take(SensivityIdx);
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
        var GravityNormalized = Vector3.Normalize(new Vector3(Gravity.X, Gravity.Y, Gravity.Z));

        // Handle NaN, check for empty inputs, prevent NaN computes          
        Vector3 EmptyVector = new(0f, 0f, 0f);

        if (Gravity.Equals(EmptyVector))
            return new Vector2(EmptyVector.X, EmptyVector.Y);

        // -acc[1] * gyro[1] + -acc[2] * gyro[2]
        return new Vector2(-GravityNormalized.Z * -AngularVelocityDeg.Z + -GravityNormalized.Y * -AngularVelocityDeg.Y,
            AngularVelocityDeg.X);
    }
}