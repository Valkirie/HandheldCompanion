using System;
using System.Numerics;
using static ControllerCommon.Utils.CommonUtils;

namespace ControllerCommon
{
    public class FlickStick
    {
        // Flickstick
        // Based on: http://gyrowiki.jibbsmart.com/blog:good-gyro-controls-part-2:the-flick-stick

        private float FlickProgress = 0.0f;
        private float FlickSize = 0.0f;
        private double UpdateTimePreviousMilliSeconds;
        private Vector2 LastStick;
        private OneEuroFilterPair JoystickFilter = new OneEuroFilterPair();
        private Vector2 StickFiltered;
        private Vector2 LastStickFiltered;

        private float[] InputBuffer = new float[16];
        private int CurrentInputIndex;

        private float FlickTimePartial = 0.01f;

        public FlickStick()
        {
        }

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

            // Compare last frame to this, determine if flick occured
            if (Length >= FlickThreshold)
            {
                if (LastLength < FlickThreshold)
                {
                    // Start flick
                    FlickProgress = 0.0f; // Reset flick timer
                    FlickSize = (float)Math.Atan2(-Stick.X, Stick.Y); // Stick angle from up/forward

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
            InputBuffer[CurrentInputIndex] = input;
            float Average = 0.0f;

            foreach (float Sample in InputBuffer)
            {
                Average += Sample;
            }

            Average /= InputBuffer.Length;

            return Average;
        }

        private float GetTieredSmoothedStickRotation(float Input, float Threshold1, float Threshold2)
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
