using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Simulators;
using LiveCharts.Wpf;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Windows.Documents;
using static HandheldCompanion.Simulators.MouseSimulator;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum MouseActionsType
    {
        [Description("Left Button")]
        LeftButton = 0,
        [Description("Right Button")]
        RightButton = 1,
        [Description("Middle Button")]
        MiddleButton = 2,

        [Description("Move Cursor")]
        Move = 3,
        [Description("Scroll Wheel")]
        Scroll = 4,

        [Description("Scroll Up")]
        ScrollUp = 5,
        [Description("Scroll Down")]
        ScrollDown = 6,
    }

    [Serializable]
    public class MouseActions : IActions
    {
        public MouseActionsType MouseType { get; set; }

        private bool IsCursorDown { get; set; }
        private bool IsCursorUp { get; set; }

        // settings
        public bool EnhancePrecision { get; set; } = false;
        public float Sensivity { get; set; } = 10.0f;
        public int scrollAmountInClicks { get; set; } = 1;
        public bool AxisInverted { get; set; } = false;

        public MouseActions()
        {
            this.ActionType = ActionType.Mouse;
            this.IsCursorDown = false;
            this.IsCursorUp = true;

            this.Value = false;
            this.prevValue = false;
        }

        public MouseActions(MouseActionsType type) : this()
        {
            this.MouseType = type;
        }

        public override void Execute(ButtonFlags button, bool value)
        {
            if (Toggle)
            {
                if ((bool)prevValue != value && value)
                    IsToggled = !IsToggled;
            }
            else
                IsToggled = false;

            if (Turbo)
            {
                if (value || IsToggled)
                {
                    if (TurboIdx % TurboDelay == 0)
                        IsTurboed = !IsTurboed;

                    TurboIdx += UPDATE_INTERVAL;
                }
                else
                {
                    IsTurboed = false;
                    TurboIdx = 0;
                }
            }
            else
                IsTurboed = false;

            // update previous value
            prevValue = value;

            // update value
            if (Toggle && Turbo)
                this.Value = IsToggled && IsTurboed;
            else if (Toggle)
                this.Value = IsToggled;
            else if (Turbo)
                this.Value = IsTurboed;
            else
                this.Value = value;

            switch (this.Value)
            {
                case true:
                    {
                        if (IsCursorDown || !IsCursorUp)
                            return;

                        IsCursorDown = true;
                        IsCursorUp = false;
                        MouseSimulator.MouseDown(MouseType, scrollAmountInClicks);
                    }
                    break;
                case false:
                    {
                        if (IsCursorUp || !IsCursorDown)
                            return;

                        IsCursorUp = true;
                        IsCursorDown = false;
                        MouseSimulator.MouseUp(MouseType);
                    }
                    break;
            }
        }

        public override void Execute(AxisFlags axis, short value)
        {
        }

        private Vector2 entryMousePos = new();
        private bool IsPressed = false;

        private Vector2 Vector = new();
        private Vector2 prevVector = new();
        private Vector2 entryVector = new();

        public void Execute(AxisLayout layout)
        {
            if (layout.vector.Length() < ControllerState.AxisDeadzones[layout.flags])
                layout.vector *= 0.0f;

            layout.vector.Y *= -1;

            switch(layout.flags)
            {
                // MoveBy
                // ScrollBy
                default:
                case AxisLayoutFlags.LeftThumb:
                case AxisLayoutFlags.RightThumb:
                    {
                        if (layout.vector == Vector2.Zero)
                            return;

                        // apply sensivity
                        Vector = (layout.vector / short.MaxValue) * Sensivity * ((float)ControllerState.AxisDeadzones[layout.flags] / short.MaxValue);
                        Vector *= (AxisInverted ? -1.0f : 1.0f);

                        if (MouseType == MouseActionsType.Move)
                        {
                            MouseSimulator.MoveBy((int)Vector.X, (int)Vector.Y);
                        }
                        else
                        {
                            MouseSimulator.HorizontalScroll((int)(Sensivity * Vector.X));
                            MouseSimulator.VerticalScroll((int)(Sensivity * Vector.Y));
                        }
                    }
                    break;

                // MoveTo
                // ScrollTo
                case AxisLayoutFlags.LeftPad:
                case AxisLayoutFlags.RightPad:
                    {
                        if (layout.vector == Vector2.Zero)
                        {
                            IsPressed = false;
                            prevVector = Vector2.Zero;
                            return;
                        }
                        else
                        {
                            // update entry point
                            if (!IsPressed)
                            {
                                prevVector = layout.vector;
                                entryVector = layout.vector;

                                entryMousePos = new Vector2(MouseSimulator.GetMousePosition().X, MouseSimulator.GetMousePosition().Y);

                                IsPressed = true;

                                return;
                            }

                            // compute
                            if (MouseType == MouseActionsType.Move)
                            {
                                Vector2 pointVector = (layout.vector - entryVector) / short.MaxValue * Sensivity * 10.0f;
                                Vector = entryMousePos + pointVector;

                                MouseSimulator.MoveTo((int)Vector.X, (int)Vector.Y);
                            }
                            else
                            {
                                Vector2 travelVector = (prevVector - layout.vector) / (100.0f - Sensivity);
                                int scrollY = (int)Math.Round(travelVector.Y);

                                Debug.WriteLine($"t:{travelVector.Length()},x:{travelVector.X},y:{scrollY}");

                                // MouseSimulator.HorizontalScroll((int)(travelVector.X));
                                MouseSimulator.VerticalScroll(scrollY);
                            }

                            // update previous position
                            prevVector = layout.vector;
                        }
                        break;
                    }
            }
        }
    }
}
