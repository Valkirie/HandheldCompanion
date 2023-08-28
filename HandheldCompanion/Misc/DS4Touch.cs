using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System;

namespace HandheldCompanion;

public static class DS4Touch
{
    [Serializable]
    public enum CursorAction
    {
        CursorUp = 0,
        CursorDown = 1,
        CursorMove = 2
    }

    [Serializable]
    public enum CursorButton
    {
        None = 0,
        TouchLeft = 1,
        TouchRight = 2
    }

    public const int TOUCHPAD_WIDTH = 1920;
    public const int TOUCHPAD_HEIGHT = 943;
    public const int TOUCH_DISABLE = 128;

    private const int TOUCH0_ID = 1;
    private const int TOUCH1_ID = 2;

    public static TrackPadTouch LeftPadTouch = new(TOUCH0_ID, true);
    public static TrackPadTouch RightPadTouch = new(TOUCH1_ID, true);
    public static byte TouchPacketCounter;

    private static short TouchX, TouchY;
    public static bool OutputClickButton;

    private static bool prevLeftPadTouch, prevRightPadTouch;
    private static bool prevLeftPadClick, prevRightPadClick;

    public static void OnMouseUp(double X, double Y, CursorButton Button, int flags = 20)
    {
        TouchX = (short)(X * TOUCHPAD_WIDTH);
        TouchY = (short)(Y * TOUCHPAD_HEIGHT);

        switch (Button)
        {
            case CursorButton.TouchLeft:
                LeftPadTouch.X = TouchX;
                LeftPadTouch.Y = TouchY;
                LeftPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                break;
            case CursorButton.TouchRight:
                RightPadTouch.X = TouchX;
                RightPadTouch.Y = TouchY;
                RightPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                break;
        }

        OutputClickButton = false;
    }

    public static void OnMouseDown(double X, double Y, CursorButton Button, int flags = 20)
    {
        TouchX = (short)(X * TOUCHPAD_WIDTH);
        TouchY = (short)(Y * TOUCHPAD_HEIGHT);

        switch (Button)
        {
            case CursorButton.TouchLeft:
                LeftPadTouch.X = TouchX;
                LeftPadTouch.Y = TouchY;
                LeftPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                break;
            case CursorButton.TouchRight:
                RightPadTouch.X = TouchX;
                RightPadTouch.Y = TouchY;
                RightPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                break;
        }

        if (flags > 26) // double tap
            OutputClickButton = true;

        TouchPacketCounter++;
    }

    public static void OnMouseMove(double X, double Y, CursorButton Button, int flags = 20)
    {
        TouchX = (short)(X * TOUCHPAD_WIDTH);
        TouchY = (short)(Y * TOUCHPAD_HEIGHT);

        switch (Button)
        {
            case CursorButton.TouchLeft:
                LeftPadTouch.X = TouchX;
                LeftPadTouch.Y = TouchY;
                break;
            case CursorButton.TouchRight:
                RightPadTouch.X = TouchX;
                RightPadTouch.Y = TouchY;
                break;
        }
    }

    public static void UpdateInputs(ControllerState Inputs)
    {
        // Left Pad
        if (prevLeftPadTouch != Inputs.ButtonState[ButtonFlags.LeftPadTouch])
        {
            if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
            {
                TouchPacketCounter++;
                LeftPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                LeftPadTouch.IsActive = true;
            }
            else
            {
                LeftPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                LeftPadTouch.IsActive = false;
            }

            prevLeftPadTouch = Inputs.ButtonState[ButtonFlags.LeftPadTouch];
        }

        if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
        {
            LeftPadTouch.X = (short)((Inputs.AxisState[AxisFlags.LeftPadX] + short.MaxValue) * TOUCHPAD_WIDTH / ushort.MaxValue);
            LeftPadTouch.Y = (short)((-Inputs.AxisState[AxisFlags.LeftPadY] + short.MaxValue) * TOUCHPAD_HEIGHT / ushort.MaxValue);
        }

        // Right Pad
        if (prevRightPadTouch != Inputs.ButtonState[ButtonFlags.RightPadTouch])
        {
            if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
            {
                TouchPacketCounter++;
                RightPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                RightPadTouch.IsActive = true;
            }
            else
            {
                RightPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                RightPadTouch.IsActive = false;
            }

            prevRightPadTouch = Inputs.ButtonState[ButtonFlags.RightPadTouch];
        }

        if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
        {
            RightPadTouch.X =
                (short)((Inputs.AxisState[AxisFlags.RightPadX] + short.MaxValue) * TOUCHPAD_WIDTH / ushort.MaxValue);
            RightPadTouch.Y = (short)((-Inputs.AxisState[AxisFlags.RightPadY] + short.MaxValue) * TOUCHPAD_HEIGHT / ushort.MaxValue);
        }

        if (prevLeftPadClick != Inputs.ButtonState[ButtonFlags.LeftPadClick] ||
            prevRightPadClick != Inputs.ButtonState[ButtonFlags.RightPadClick])
        {
            if (Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick])
                OutputClickButton = true;
            else
                OutputClickButton = false;

            prevLeftPadClick = Inputs.ButtonState[ButtonFlags.LeftPadClick];
            prevRightPadClick = Inputs.ButtonState[ButtonFlags.RightPadClick];
        }
    }

    public class TrackPadTouch
    {
        public byte Id;
        public bool IsActive;
        public int RawTrackingNum;
        public short X;
        public short Y;

        public TrackPadTouch(byte _Id, bool disabled = false)
        {
            Id = _Id;
            if (disabled)
                RawTrackingNum |= _Id + TOUCH_DISABLE;
        }
    }
}