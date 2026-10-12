using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System;
using System.Diagnostics;

namespace HandheldCompanion;

public static class DS4Touch
{
    private static readonly object overlayLock = new();
    private static readonly ControllerState overlayInputs = new();

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
    public static TrackPadTouch OutputLeftPadTouch = new(TOUCH0_ID, true);
    public static TrackPadTouch OutputRightPadTouch = new(TOUCH1_ID, true);
    public static byte TouchPacketCounter;

    private static OverlayTouchState leftOverlayTouch;
    private static OverlayTouchState rightOverlayTouch;
    private static bool inputClickButton;

    static DS4Touch()
    {
        Debug.Assert(CoordinateToAxis(0, TOUCHPAD_WIDTH) == short.MinValue);
        Debug.Assert(CoordinateToAxis(TOUCHPAD_WIDTH - 1, TOUCHPAD_WIDTH) == short.MaxValue);
    }

    public static bool OutputClickButton
    {
        get
        {
            lock (overlayLock)
                return inputClickButton || leftOverlayTouch.IsClicked || rightOverlayTouch.IsClicked;
        }
    }

    public static void ClearOutputTouches()
    {
        OutputLeftPadTouch.IsActive = false;
        OutputRightPadTouch.IsActive = false;
    }

    public static void SetOutputTouch(byte finger, int x, int y)
    {
        TrackPadTouch touch = finger == 1 ? OutputLeftPadTouch : OutputRightPadTouch;
        touch.X = (short)Math.Clamp(x, short.MinValue, short.MaxValue);
        touch.Y = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
        touch.IsActive = true;
    }

    private static bool prevLeftPadTouch, prevRightPadTouch;
    private static bool prevLeftPadClick, prevRightPadClick;

    public static void OnMouseUp(double X, double Y, CursorButton Button, int flags = 20)
    {
        UpdateOverlayTouch(X, Y, Button, false);
        SetOverlayClick(Button, false);
    }

    public static void OnMouseDown(double X, double Y, CursorButton Button, int flags = 20)
    {
        UpdateOverlayTouch(X, Y, Button, true);

        if (flags > 26) // double tap
            SetOverlayClick(Button, true);

        TouchPacketCounter++;
    }

    public static void OnMouseMove(double X, double Y, CursorButton Button, int flags = 20)
    {
        UpdateOverlayTouch(X, Y, Button, null);
    }

    public static void SetOverlayClick(CursorButton button, bool isClicked)
    {
        lock (overlayLock)
        {
            ref OverlayTouchState overlayTouch = ref GetOverlayTouch(button);
            overlayTouch.IsClicked = isClicked;
        }
    }

    public static ControllerState ApplyOverlayInputs(ControllerState inputs)
    {
        lock (overlayLock)
        {
            if (!leftOverlayTouch.IsActive && !rightOverlayTouch.IsActive &&
                !leftOverlayTouch.IsClicked && !rightOverlayTouch.IsClicked)
                return inputs;

            ButtonState.Overwrite(inputs.ButtonState, overlayInputs.ButtonState);
            overlayInputs.AxisState.AddRange(inputs.AxisState);
            overlayInputs.GyroState.CopyFrom(inputs.GyroState);

            ApplyOverlayInput(leftOverlayTouch, CursorButton.TouchLeft, overlayInputs);
            ApplyOverlayInput(rightOverlayTouch, CursorButton.TouchRight, overlayInputs);
            return overlayInputs;
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
            lock (overlayLock)
                inputClickButton = Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick];

            prevLeftPadClick = Inputs.ButtonState[ButtonFlags.LeftPadClick];
            prevRightPadClick = Inputs.ButtonState[ButtonFlags.RightPadClick];
        }
    }

    public static short CoordinateToAxis(int value, int extent)
    {
        int clamped = Math.Clamp(value, 0, extent - 1);
        long numerator = (long)clamped * ushort.MaxValue + (extent - 1) / 2;
        return (short)Math.Clamp(numerator / (extent - 1) + short.MinValue, short.MinValue, short.MaxValue);
    }

    public static int AxisToCoordinate(short value, int extent)
    {
        long numerator = ((long)value - short.MinValue) * (extent - 1) + ushort.MaxValue / 2;
        return (int)Math.Clamp(numerator / ushort.MaxValue, 0, extent - 1);
    }

    private static void UpdateOverlayTouch(double x, double y, CursorButton button, bool? isActive)
    {
        double localX = button == CursorButton.TouchLeft ? x * 2.0d : (x - 0.5d) * 2.0d;
        int axisX = (int)Math.Round(Math.Clamp(localX, 0.0d, 1.0d) * (TOUCHPAD_WIDTH - 1));
        int axisY = (int)Math.Round((1.0d - Math.Clamp(y, 0.0d, 1.0d)) * (TOUCHPAD_HEIGHT - 1));
        short touchX = (short)Math.Clamp((int)(x * TOUCHPAD_WIDTH), 0, TOUCHPAD_WIDTH - 1);
        short touchY = (short)Math.Clamp((int)(y * TOUCHPAD_HEIGHT), 0, TOUCHPAD_HEIGHT - 1);

        lock (overlayLock)
        {
            ref OverlayTouchState overlayTouch = ref GetOverlayTouch(button);
            overlayTouch.AxisX = CoordinateToAxis(axisX, TOUCHPAD_WIDTH);
            overlayTouch.AxisY = CoordinateToAxis(axisY, TOUCHPAD_HEIGHT);
            overlayTouch.TouchX = touchX;
            overlayTouch.TouchY = touchY;

            if (isActive.HasValue)
                overlayTouch.IsActive = isActive.Value;

            TrackPadTouch touch = button == CursorButton.TouchLeft ? LeftPadTouch : RightPadTouch;
            touch.X = touchX;
            touch.Y = touchY;
            touch.IsActive = overlayTouch.IsActive;
            if (overlayTouch.IsActive)
                touch.RawTrackingNum &= ~TOUCH_DISABLE;
            else
                touch.RawTrackingNum |= TOUCH_DISABLE;
        }
    }

    private static void ApplyOverlayInput(OverlayTouchState overlayTouch, CursorButton button, ControllerState inputs)
    {
        ButtonFlags touchButton = button == CursorButton.TouchLeft ? ButtonFlags.LeftPadTouch : ButtonFlags.RightPadTouch;
        ButtonFlags clickButton = button == CursorButton.TouchLeft ? ButtonFlags.LeftPadClick : ButtonFlags.RightPadClick;
        AxisFlags axisX = button == CursorButton.TouchLeft ? AxisFlags.LeftPadX : AxisFlags.RightPadX;
        AxisFlags axisY = button == CursorButton.TouchLeft ? AxisFlags.LeftPadY : AxisFlags.RightPadY;

        inputs.ButtonState[clickButton] |= overlayTouch.IsClicked;
        if (!overlayTouch.IsActive)
            return;

        inputs.ButtonState[touchButton] = true;
        inputs.AxisState[axisX] = overlayTouch.AxisX;
        inputs.AxisState[axisY] = overlayTouch.AxisY;

        TrackPadTouch touch = button == CursorButton.TouchLeft ? LeftPadTouch : RightPadTouch;
        touch.X = overlayTouch.TouchX;
        touch.Y = overlayTouch.TouchY;
        touch.IsActive = true;
        touch.RawTrackingNum &= ~TOUCH_DISABLE;
    }

    private static ref OverlayTouchState GetOverlayTouch(CursorButton button)
    {
        if (button == CursorButton.TouchRight)
            return ref rightOverlayTouch;

        return ref leftOverlayTouch;
    }

    private struct OverlayTouchState
    {
        public bool IsActive;
        public bool IsClicked;
        public short AxisX;
        public short AxisY;
        public short TouchX;
        public short TouchY;
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
