using HandheldCompanion.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Misc
{
    public enum EnduranceGamingControl
    {
        Off = 0,    // Endurance Gaming disable
        On = 1,     // Endurance Gaming enable
        Auto = 2,   // Endurance Gaming auto
    }

    public enum EnduranceGamingMode
    {
        Performance = 0,        // Endurance Gaming better performance mode
        Balanced = 1,           // Endurance Gaming balanced mode
        MaximumBattery = 2,     // Endurance Gaming maximum battery mode
    }

    [Serializable]
    public class IntelEnduranceGamingProfile
    {
        public EnduranceGamingControl control = EnduranceGamingControl.Off;
        public EnduranceGamingMode mode = EnduranceGamingMode.Performance;

        // A public constructor
        public IntelEnduranceGamingProfile()
        {
   
        }

        // A public constructor that takes an Endurance Gaming parameters as argument
        public IntelEnduranceGamingProfile(EnduranceGamingControl control, EnduranceGamingMode mode) : this()
        {
            this.control = control;
            this.mode = mode;
        }

        // A public method that sets Intel Gaming Endurance Mode and its preset
        public void setEnduranceGamingControlMode(EnduranceGamingControl control, EnduranceGamingMode mode)
        {
            this.control = control;
            this.mode = mode;
        }

        // A public method that returns the fan speed based on average temperature by linear interpolation
        public EnduranceGamingControl GetEnduranceGamingControl()
        {
            return this.control;
        }

        // A public method that returns the fan speed based on average temperature by linear interpolation
        public EnduranceGamingMode GetEnduranceGamingMode()
        {
            return this.mode;
        }
    }
}
