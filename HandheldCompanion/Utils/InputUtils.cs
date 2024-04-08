using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Utils;

public enum MotionInput
{
    /// <summary>
    /// Local space: A gyro control method commonly used in games on devices like the Nintendo Switch and PS4.
    /// In local space, the game disregards the controller’s real-world orientation and focuses solely on its angular velocity around its local axes.
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
    /// World Space: Gyro controls calculate the direction of gravity to determine the player’s “up” orientation.
    /// The yaw axis remains aligned with this “up” direction, regardless of the controller’s physical orientation.
    /// By using the accelerometer, local space inputs are converted to world space.
    /// Players can consistently turn the camera left and right by rotating the controller relative to themselves.
    /// While more intuitive, world space controls are challenging to implement and less suitable for handheld devices.
    /// </summary>
    WorldSpace = 2,
    JoystickSteering = 3
}

public enum MotionOuput
{
    Disabled = 0,
    LeftStick = 1,
    RightStick = 2,
    MoveCursor = 3,
    ScrollWheel = 4
}

public enum MotionMode
{
    Off = 0,
    On = 1,
    Toggle = 2
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
    public static float Clamp(float value, float min, float max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    public static float rangeMap(float value, float minIn, float maxIn, float minOut, float maxOut)
    {
        float inRange = maxIn - minIn;
        float outRange = maxOut - minOut;

        return minOut + outRange * ((value - minIn) / inRange);
    }

    public static float deg2rad(float degrees)
    {
        return (float)((Math.PI / 180) * degrees);
    }

    public static float rad2deg(float rad)
    {
        return rad * (180 / (float)Math.PI);
    }

    public static float MapRange(float Value, float OldMin, float OldMax, float NewMin, float NewMax)
    {
        return (NewMin + (NewMax - NewMin) * (Value - OldMin) / (OldMax - OldMin));
    }

    public static byte NormalizeXboxInput(float input)
    {
        input = Math.Clamp(input, short.MinValue, short.MaxValue);
        float output = input / ushort.MaxValue * byte.MaxValue + (byte.MaxValue / 2.0f);
        return (byte)Math.Round(output);
    }

    public static float Steering(float DeviceAngle,
                                 float DeviceAngleMax,
                                 float ToThePowerOf,
                                 float DeadzoneAngle)
    {
        // Range angle y value (0 to user defined angle) into -1.0 to 1.0 position value taking into account deadzone angle
        float Result = AngleToJoystickPos(DeviceAngle, DeviceAngleMax, DeadzoneAngle);

        // Apply user defined to the power of to joystick pos
        Result = DirectionRespectingPowerOf(Result, ToThePowerOf);

        // Scale joystick x pos -1 to 1 to joystick x range, send 0 for y.
        return (float)-(Result * short.MaxValue);
    }

    // Determine -1 to 1 joystick position given user defined max input angle and dead zone
    // Angles in degrees
    public static float AngleToJoystickPos(float Angle, float DeviceAngleMax, float DeadzoneAngle)
    {
        // Deadzone remapped angle, note this angle is no longer correct with device angle
        float Result = ((Math.Abs(Angle) - DeadzoneAngle) / (DeviceAngleMax - DeadzoneAngle)) * DeviceAngleMax;

        // Clamp deadzone remapped angle, prevents negative values when
        // actual device angle is below dead zone angle
        // Divide by max angle, angle to joystick position with user max
        Result = Math.Clamp(Result, 0, DeviceAngleMax) / DeviceAngleMax;

        // Apply direction again
        return (Angle < 0.0) ? -Result : Result;
    }

    // Apply power of to -1 to 1 joystick position while respecting direction
    public static float DirectionRespectingPowerOf(float JoystickPos, float Power)
    {
        float Result = (float)Math.Pow(Math.Abs(JoystickPos), Power);

        // Apply direction again
        return (JoystickPos < 0.0) ? -Result : Result;
    }

    // Compensation for in game deadzone
    // Inputs: raw ThumbValue and deadzone 0-100%
    // Should not be used under normal circumstances, in game should be set to 0% if possible. Results in loss of resolution.
    // Use cases foreseen:
    // - Game has deadzone, but no way to configure or change it
    // - User does not want to change general emulator deadzone setting but want's it removed for specific game and use UMC Steering
    public static Vector2 ApplyAntiDeadzone(Vector2 ThumbValue, int DeadzonePercentage)
    {
        // Return if thumbstick or anti deadzone is not used
        if (DeadzonePercentage == 0 || ThumbValue == Vector2.Zero)
            return ThumbValue;

        // Convert short value input to -1 to 1
        Vector2 StickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

        // Convert 0-100% to 0 to 1
        float Deadzone = DeadzonePercentage / 100.0f;

        // Map vector to new range by determining the multiplier
        float Multiplier = ((1 - Deadzone) * StickInput.Length() + Deadzone) / StickInput.Length();

        // Convert -1 to 1 back to short value and return
        return StickInput * Multiplier * short.MaxValue;
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

    public static void TouchToDirections(int x, int y, int radius, int radialShift, out bool[] buttons)
    {
        buttons = new bool[4];

        float length = new Vector2(x, y).Length();
        if (length < radius)
            return;

        int angle = (int)(Math.Atan2(x, y) * (180 / Math.PI) + 22.5) + radialShift;
        if (angle < 0) angle += 360;
        if (angle == 360) angle = 0;

        switch (angle / 45)
        {
            case 0:
                buttons[0] = true;
                break;
            case 1:
                buttons[0] = true;
                buttons[1] = true;
                break;
            case 2:
                buttons[1] = true;
                break;
            case 3:
                buttons[1] = true;
                buttons[2] = true;
                break;
            case 4:
                buttons[2] = true;
                break;
            case 5:
                buttons[2] = true;
                buttons[3] = true;
                break;
            case 6:
                buttons[3] = true;
                break;
            case 7:
                buttons[3] = true;
                buttons[0] = true;
                break;
        }
    }

    public static Vector3 ToEulerAngles(System.Windows.Media.Media3D.Quaternion q)
    {
        // Store the Euler angles in radians
        var pitchYawRoll = new Vector3();

        double sqw = q.W * q.W;
        double sqx = q.X * q.X;
        double sqy = q.Y * q.Y;
        double sqz = q.Z * q.Z;

        // If quaternion is normalised the unit is one, otherwise it is the correction factor
        var unit = sqx + sqy + sqz + sqw;
        double test = q.X * q.Y + q.Z * q.W;

        if (test > 0.4999f * unit) // 0.4999f OR 0.5f - EPSILON
        {
            // Singularity at north pole
            pitchYawRoll.Y = 2f * (float)Math.Atan2(q.X, q.W); // Yaw
            pitchYawRoll.X = (float)Math.PI * 0.5f; // Pitch
            pitchYawRoll.Z = 0f; // Roll
            return pitchYawRoll;
        }

        if (test < -0.4999f * unit) // -0.4999f OR -0.5f + EPSILON
        {
            // Singularity at south pole
            pitchYawRoll.Y = -2f * (float)Math.Atan2(q.X, q.W); // Yaw
            pitchYawRoll.X = -(float)Math.PI * 0.5f; // Pitch
            pitchYawRoll.Z = 0f; // Roll
            return pitchYawRoll;
        }

        pitchYawRoll.Y = (float)Math.Atan2(2f * q.Y * q.W - 2f * q.X * q.Z, sqx - sqy - sqz + sqw); // Yaw
        pitchYawRoll.X = (float)Math.Asin(2f * test / unit); // Pitch
        pitchYawRoll.Z = (float)Math.Atan2(2f * q.X * q.W - 2f * q.Y * q.Z, -sqx + sqy - sqz + sqw); // Roll

        return pitchYawRoll;
    }
}