using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

    public enum GamepadButtonFlags : uint
    {
        [Description("DPad Up")]
        DPadUp = 1,
        [Description("DPad Down")]
        DPadDown = 2,
        [Description("DPad Left")]
        DPadLeft = 4,
        [Description("DPad Right")]
        DPadRight = 8,
        [Description("Start")]
        Start = 16,
        [Description("Back")]
        Back = 32,
        [Description("Left Thumb")]
        LeftThumb = 64,
        [Description("Right Thumb")]
        RightThumb = 128,
        [Description("Left Shoulder")]
        LeftShoulder = 256,
        [Description("Right Shoulder")]
        RightShoulder = 512,
        [Description("Left Trigger")]
        LeftTrigger = 1024,     // specific
        [Description("Right Trigger")]
        RightTrigger = 2048,    // specific
        [Description("A")]
        A = 4096,
        [Description("B")]
        B = 8192,
        [Description("X")]
        X = 16384,
        [Description("Y")]
        Y = 32768,
        [Description("Always On")]
        AlwaysOn = 65536        // specific
    }

    public static class InputUtils
    {

        public static float Clamp(float value, float min, float max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        public static float rangeMap(float value, SensorSpec spec)
        {
            float inRange = spec.maxIn - spec.minIn;
            float outRange = spec.maxOut - spec.minOut;

            return spec.minOut + outRange * ((value - spec.minIn) / inRange);
        }

        public static float deg2rad(float degrees)
        {
            return (float)((Math.PI / 180) * degrees);
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
        public static Vector2 ApplyAntiDeadzone(Vector2 ThumbValue, float DeadzoneIn)
        {
            float deadzone = DeadzoneIn / 100;

            Vector2 stickInput = new Vector2(ThumbValue.X, ThumbValue.Y) / short.MaxValue;

            float magnitude = stickInput.Length();

            if (magnitude < deadzone)
            {
                float dist = Math.Abs(magnitude - deadzone);
                stickInput = new Vector2(stickInput.X, stickInput.Y) * (1 + dist);
            }

            return stickInput * short.MaxValue;
        }

        // Custom sensitivity
        // Interpolation function (linear), takes list of nodes coordinates and gamepad joystick position returns game input
        private static int SensivityIdx = 2;
        public static float ApplyCustomSensitivity(float AngularValue, float MaxValue, List<ProfileVector> Nodes)
        {
            int NodeAmount = Profile.array_size;

            // Use absolute joystick position, range -1 to 1, re-apply direction later
            float JoystickPosAbs = (float)Math.Abs(AngularValue / MaxValue);
            float JoystickPosAdjusted = 0.0f;

            // Check what we will be sending
            if (JoystickPosAbs <= Nodes[0].x)
            {
                // Send 0 output to game
                JoystickPosAdjusted = 0.0f;
            }
            else if (JoystickPosAbs >= Nodes[NodeAmount - 1].x)
            {
                // Send 1 output to game
                JoystickPosAdjusted = 1.0f;
            }
            // Calculate custom sensitivty
            else
            {
                var closests = Nodes.Select(n => new { n, distance = Math.Abs(n.x - JoystickPosAbs) }).OrderBy(p => p.distance).Take(SensivityIdx);
                foreach (var item in closests)
                    JoystickPosAdjusted += (float)(item.n.y / (1.0f + item.distance));

                JoystickPosAdjusted /= SensivityIdx;
                JoystickPosAdjusted *= 2.0f; // a 1.0f vector means a 100% increase
            }

            // Apply direction
            return JoystickPosAdjusted;
        }

        public class FlickStick
        {
            // Flickstick
            private float FlickProgress = 0.0f;
            private float FlickSize = 0.0f;
            private double UpdateTimePreviousMilliSeconds;
            private Vector2 Stick;
            private Vector2 LastStick;
            private CommonUtils.OneEuroFilterPair JoystickFilter = new CommonUtils.OneEuroFilterPair();
            private Vector2 StickFiltered;
            private Vector2 LastStickFiltered;

            private float[] InputBuffer = new float[16];
            private int CurrentInputIndex;

            private float FlickTime = 0.1f;
            private float FlickTimePartial = 0.01f;

            // Flick stick, flick to initial angle, allow for stick rotation in horizontal plane after
            public float Handle(Vector2 Stick, float FlickDuration, float StickSensitivity, double TotalMilliseconds)
            {
                // Provide -1 to 1 joystickposition range for function.
                // @Benjamin not sure about converting here again from float to short.
                Stick = new Vector2(MapShortToMinusOnePlusOneRange((short)Stick.X),
                                    MapShortToMinusOnePlusOneRange((short)Stick.Y));

                // Variables
                float Result = 0.0f;
                float LastLength = LastStick.Length();
                float Length = Stick.Length();

                // Settings
                float FlickThreshold = 0.9f;
                float TurnSmoothThreshold = 0.1f;

                double DeltaTimeSeconds = (TotalMilliseconds - UpdateTimePreviousMilliSeconds) / 1000L;
                var rate = 1.0 / (double)(TotalMilliseconds - UpdateTimePreviousMilliSeconds);
                UpdateTimePreviousMilliSeconds = TotalMilliseconds;

                StickFiltered = new Vector2((float)JoystickFilter.axis1Filter.Filter(Stick.X, rate),
                                            (float)JoystickFilter.axis2Filter.Filter(Stick.Y, rate));

                //logger.LogInformation("Plot Vigemtarget_FlickStickInputXFiltered {0} {1}", xinputController.totalmilliseconds, StickFiltered.X);

                // Compare last frame to this, determine if flick occured
                if (Length >= FlickThreshold)
                {
                    if (LastLength < FlickThreshold)
                    {
                        // Start flick
                        FlickProgress = 0.0f; // Reset flick timer
                        FlickSize = (float)Math.Atan2(-Stick.X, Stick.Y); // Stick angle from up/forward

                        //logger.LogInformation("Plot Vigemtarget_FlickSize {0} {1}", xinputController.totalmilliseconds, FlickSize);

                        // Determine flick pulse duration
                        // Partial flick time based on flick size
                        // Flick duration is 180 degrees, flick partial duration is time needed for partial turn
                        // Note, games that use acceleration and deceleration won't be 100% accurate
                        FlickTimePartial = FlickDuration * Math.Abs(FlickSize) / (float)Math.PI;
                    }
                    else
                    {
                        // Possible improvement: already combine stick rotation and flick
                        // for last flick output to allow for smoother transition.
                        
                        // Stick turn along horizontal plane
                        float StickAngle = (float)Math.Atan2(-StickFiltered.X, StickFiltered.Y); // Stick angle from up/forward
                        float LastStickAngle = (float)Math.Atan2(-LastStickFiltered.X, LastStickFiltered.Y);

                        float AngleChange = (float)WrapMinMax(StickAngle - LastStickAngle, -Math.PI, Math.PI);

                        Result += GetTieredSmoothedStickRotation(AngleChange, TurnSmoothThreshold / 2.0f, TurnSmoothThreshold)
                                  * StickSensitivity * 2;

                        //logger.LogInformation("Plot Vigemtarget_FlickStickAngleChange {0} {1}", xinputController.totalmilliseconds, AngleChange);
                    }

                }
                else
                {
                    // Turn cleanup
                    if (LastLength >= FlickThreshold)
                    {
                        // Transitioned from flick/turn to no flick, clean up
                        ZeroTurnSmoothing();
                    }
                }

                // Continue flick
                if (FlickProgress < FlickTimePartial)
                {
                    // Determine flick strength
                    // Strength remains 1 up to last cycle,
                    // then it's proportional to remaining time,
                    // assuming same duration as last cycle
                    if (FlickTimePartial - FlickProgress > DeltaTimeSeconds)
                    {
                        Result = 1.0f;
                    }
                    else
                    {
                        // Determine proportional strength
                        // Assumption is made here that
                        // next cycle will take as long as last cycle
                        // Could use HID rate as alternative
                        Result = (float)(1.0f * (FlickTimePartial - FlickProgress) / DeltaTimeSeconds);
                    }

                    // Apply flick direction
                    Result *= Math.Sign(FlickSize);

                    // Increment progress
                    // Possible improvement, determine flickprogress at the start to compensate for timing inaccuracy
                    FlickProgress += (float)DeltaTimeSeconds;
                }

                /*
                logger.LogInformation("Plot Vigemtarget_FlickStickDeltaTime {0} {1}", xinputController.totalmilliseconds, DeltaTime);
                logger.LogInformation("Plot Vigemtarget_FlickStickProgress {0} {1}", xinputController.totalmilliseconds, FlickProgress);
                logger.LogInformation("Plot Vigemtarget_FlickTime {0} {1}", xinputController.totalmilliseconds, FlickTime);
                logger.LogInformation("Plot Vigemtarget_FlickTimePartial {0} {1}", xinputController.totalmilliseconds, FlickTimePartial);
                logger.LogInformation("Plot Vigemtarget_FlickStickResult {0} {1}", xinputController.totalmilliseconds, Result);
                */

                LastStick = Stick;
                LastStickFiltered = StickFiltered;

                return Result * short.MaxValue;
            }

            private static float MapShortToMinusOnePlusOneRange(short Input)
            {
                return ((float)Input - (float)short.MinValue) * ((float)1 - (float)-1) / ((float)short.MaxValue - (float)short.MinValue) + -1;
            }

            // Zero i.e. reset input buffer
            private void ZeroTurnSmoothing()
            {
                for (int i = 0; i < InputBuffer.Length; i++)
                {
                    InputBuffer[i] = 0.0f;
                }
            }

            private float GetSmoothedStickRotation(float input)
            {
                CurrentInputIndex = (CurrentInputIndex + 1) % InputBuffer.Length;
                // logger.LogInformation("Plot Vigemtarget_CurrentInputIndex {0} {1}", xinputController.totalmilliseconds, CurrentInputIndex);

                InputBuffer[CurrentInputIndex] = input;

                // logger.LogInformation("InputBuffer GetSmoothed {0}", InputBuffer);

                float Average = 0.0f;
                foreach (float Sample in InputBuffer)
                {
                    Average += Sample;
                }
                Average /= InputBuffer.Length;

                return Average;
            }

            private float GetTieredSmoothedStickRotation(float Input,
               float Threshold1, float Threshold2)
            {

                float InputMagnitude = Math.Abs(Input);

                float DirectWeight = (InputMagnitude - Threshold1) /
                   (Threshold2 - Threshold1);
                DirectWeight = Math.Clamp(DirectWeight, 0.0f, 1.0f);

                return (Input * DirectWeight) + GetSmoothedStickRotation(Input * (1.0f - DirectWeight));
            }

            // Wrap around max
            private static double WrapMax(double X, double Max)
            {
                return (Max + X % Max) % Max;
            }

            // Wrap around min max
            private static double WrapMinMax(double X, double Min, double Max)
            {
                return Min + WrapMax(X - Min, Max - Min);
            }
        }
    }
}
