using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Simulators;
using System;
using System.ComponentModel;
using System.Numerics;

namespace HandheldCompanion.Actions
{
    [Serializable]
    public enum SpecialActionsType
    {
        [Description("Flick Stick")]
        FlickStick = 0,
    }

    [Serializable]
    public class SpecialActions : IActions
    {
        public SpecialActionsType SpecialType;

        // runtime variables
        private FlickStick flickStick = new();
        private float remainder = 0;

        // settings
        public float FlickSensitivity = 5.0f;
        public float SweepSensitivity = 5.0f;
        public float FlickThreshold = 0.75f;
        public int FlickSpeed = 100;
        public int FlickFrontAngleDeadzone = 15;

        public SpecialActions()
        {
            this.ActionType = ActionType.Special;
        }

        public SpecialActions(SpecialActionsType type) : this()
        {
            this.SpecialType = type;
        }

        public void Execute(AxisLayout layout)
        {
            if (layout.vector == Vector2.Zero)
                return;

            float delta = flickStick.Handle(layout.vector, FlickSensitivity, SweepSensitivity,
                                            FlickThreshold, FlickSpeed, FlickFrontAngleDeadzone);

            delta += remainder;
            int intDelta = (int)Math.Truncate(delta);
            remainder = delta - intDelta;

            MouseSimulator.MoveBy(intDelta, 0);
        }
    }
}
