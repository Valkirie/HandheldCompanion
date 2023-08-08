using System;
using System.Runtime.InteropServices;

namespace steam_hidapi.net.Hid
{
    internal enum SCPid : ushort
    {
        WIRED     = 0x1102,
        WIRELESS  = 0x1142,
        STEAMDECK = 0x1205,
    }

    internal enum SCEventType : byte
    {
        INPUT_DATA      = 0x01,
        CONNECT         = 0x03,
        BATTERY         = 0x04,
        DECK_INPUT_DATA = 0x09,
    }

    internal enum SCPacketType : byte
    {
        // linux kernel
        CLEAR_MAPPINGS       = 0x81,
        GET_MAPPINGS         = 0x82,
        GET_ATTRIB           = 0x83,
        GET_ATTRIB_LABEL     = 0x84,
        DEFAULT_MAPPINGS     = 0x85,
        FACTORY_RESET        = 0x86,
        WRITE_REGISTER       = 0x87,
        CLEAR_REGISTER       = 0x88,
        READ_REGISTER        = 0x89,
        GET_REGISTER_LABEL   = 0x8a,
        GET_REGISTER_MAX     = 0x8b,
        GET_REGISTER_DEFAULT = 0x8c,
        SET_MODE             = 0x8d,
        DEFAULT_MOUSE        = 0x8e,
        SET_HAPTIC           = 0x8f,
        GET_SERIAL           = 0xae,
        REQUEST_COMM_STATUS  = 0xb4,
        HAPTIC_RUMBLE        = 0xeb,

        // other sources
        RESET                = 0x95,
        OFF                  = 0x9f,
        CALIBRATE_TRACKPAD   = 0xa7,
        AUDIO                = 0xb6,
        CALIBRATE_JOYSTICK   = 0xbf,
        SET_AUDIO_INDICES    = 0xc1,
        SET_HAPTIC2          = 0xea,
    }

    internal enum SCRegister : byte
    {
        LPAD_MODE           = 0x07,  // cursor keys, haptic on SD
        RPAD_MODE           = 0x08,  // mouse
        RPAD_MARGIN         = 0x18,  // dead margin, eliminating small movements, noise, on by default
        LED_INTENSITY       = 0x2d,  // 0 - 100
        UNKNOWN1            = 0x2e,  // seen in scc config packet, set to 0x00
        GYRO_MODE           = 0x30,  // Gordon
        UNKNOWN2            = 0x31,  // seen in scc config packet, set to 0x02
        IDLE_TIMEOUT        = 0x32,  // in seconds
        LPAD_CLICK_PRESSURE = 0x34,  // Neptune
        RPAD_CLICK_PRESSURE = 0x35,  // Neptune
    }

    internal enum SCRegisterValue : byte
    {
        OFF = 0x00,
        ON = 0x01,
    }

    internal enum SCLizardMode : byte
    {
        ON  = 0x00,
        OFF = 0x07,
    }

    public enum SCHapticMotor : byte
    {
        Right = 0x00,
        Left = 0x01,
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SCHapticPacket
    {
        public byte packet_type; // = 0x8f;
        public byte len;         // = 0x07;
        public byte position;    // = 0|1;
        public UInt16 amplitude;
        public UInt16 period;
        public UInt16 count;
    }

    // GORDON CONTROLLER SPECIFIC

    internal enum GCGyroMode : byte
    {
        NONE    = 0x00,
        TILT_X  = 0x01,
        TILT_Y  = 0x02,
        Q       = 0x04,
        ACCEL   = 0x08,
        GYRO    = 0x10,
    }

    internal enum GCButton0
    {
        BTN_R2              = 0b00000001,
        BTN_L2              = 0b00000010,
        BTN_R1              = 0b00000100,
        BTN_L1              = 0b00001000,
        BTN_Y               = 0b00010000,
        BTN_B               = 0b00100000,
        BTN_X               = 0b01000000,
        BTN_A               = 0b10000000,
    }

    internal enum GCButton1
    {
        BTN_DPAD_UP         = 0b00000001,
        BTN_DPAD_RIGHT      = 0b00000010,
        BTN_DPAD_LEFT       = 0b00000100,
        BTN_DPAD_DOWN       = 0b00001000,
        BTN_MENU            = 0b00010000,
        BTN_STEAM           = 0b00100000,
        BTN_OPTIONS         = 0b01000000,
        BTN_L4              = 0b10000000,
    }

    internal enum GCButton2
    {
        BTN_R4              = 0b00000001,
        BTN_LPAD_PRESS      = 0b00000010,
        BTN_RPAD_PRESS      = 0b00000100,
        BTN_LPAD_TOUCH      = 0b00001000,
        BTN_RPAD_TOUCH      = 0b00010000,
        BTN_LSTICK_PRESS    = 0b01000000,
        BTN_LPAD_AND_JOY    = 0b10000000,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GCInput
    {
        public byte ptype;          //0x00
        public byte _a1;            //0x01
        public byte _a2;            //0x02
        public byte _a3;            //0x03
        public UInt32 seq;          //0x04
        public byte buttons0;       //0x08
        public byte buttons1;       //0x09
        public byte buttons2;       //0x0A
        public byte ltrig;          //0x0B
        public byte rtrig;          //0x0C
        public byte nop0;           //0x0D
        public byte nop1;           //0x0E
        public byte nop2;           //0x0F
        public Int16 lpad_x;        //0x10
        public Int16 lpad_y;        //0x12
        public Int16 rpad_x;        //0x13
        public Int16 rpad_y;        //0x16
        public Int16 w_ltrig;       //0x18
        public Int16 w_rtrig;       //0x1A
        public Int16 accel_x;       //0x1C
        public Int16 accel_y;       //0x1E
        public Int16 accel_z;       //0x20
        public Int16 gpitch;        //0x22
        public Int16 gyaw;          //0x24
        public Int16 groll;         //0x26
        public Int16 q1;            //0x28
        public Int16 q2;            //0x2A
        public Int16 q3;            //0x2C
        public Int16 q4;            //0x2E
        public Int16 nop3;          //0x30
        public Int16 w_ltrig_u;     //0x32
        public Int16 w_rtrig_u;     //0x34
        public Int16 w_joyx_u;      //0x36
        public Int16 w_joyy_u;      //0x38
        public Int16 w_lpadx_u;     //0x3A
        public Int16 w_lpady_u;     //0x3C
        public UInt16 battery;      //0x3E
    }

    // NEPTUNE CONTROLLER SPECIFIC

    internal enum NCButton0
    {
        BTN_R2              = 0b00000001,
        BTN_L2              = 0b00000010,
        BTN_R1              = 0b00000100,
        BTN_L1              = 0b00001000,
        BTN_Y               = 0b00010000,
        BTN_B               = 0b00100000,
        BTN_X               = 0b01000000,
        BTN_A               = 0b10000000,
    }

    internal enum NCButton1
    {
        BTN_DPAD_UP         = 0b00000001,
        BTN_DPAD_RIGHT      = 0b00000010,
        BTN_DPAD_LEFT       = 0b00000100,
        BTN_DPAD_DOWN       = 0b00001000,
        BTN_MENU            = 0b00010000,
        BTN_STEAM           = 0b00100000,
        BTN_OPTIONS         = 0b01000000,
        BTN_L5              = 0b10000000,
    }

    internal enum NCButton2
    {
        BTN_R5              = 0b00000001,
        BTN_LPAD_PRESS      = 0b00000010,
        BTN_RPAD_PRESS      = 0b00000100,
        BTN_LPAD_TOUCH      = 0b00001000,
        BTN_RPAD_TOUCH      = 0b00010000,
        BTN_LSTICK_PRESS    = 0b01000000,
    }

    internal enum NCButton3
    {
        BTN_RSTICK_PRESS    = 0b00000100,
    }

    internal enum NCButton5
    {
        BTN_L4              = 0b00000010,
        BTN_R4              = 0b00000100,
        BTN_LSTICK_TOUCH    = 0b01000000,
        BTN_RSTICK_TOUCH    = 0b10000000,
    }

    internal enum NCButton6
    {
        BTN_QUICK_ACCESS    = 0b00000100,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NCInput
    {
        public byte ptype;          //0x00
        public byte _a1;            //0x01
        public byte _a2;            //0x02
        public byte _a3;            //0x03
        public UInt32 seq;          //0x04
        public byte buttons0;       //0x08
        public byte buttons1;       //0x09
        public byte buttons2;       //0x0A
        public byte buttons3;       //0x0B
        public byte nop0;           //0x0C
        public byte buttons5;       //0x0D
        public byte buttons6;       //0x0E
        public byte nop1;           //0x0F
        public Int16 lpad_x;        //0x10
        public Int16 lpad_y;        //0x12
        public Int16 rpad_x;        //0x13
        public Int16 rpad_y;        //0x16
        public Int16 accel_x;       //0x18
        public Int16 accel_y;       //0x1A
        public Int16 accel_z;       //0x1C
        public Int16 gpitch;        //0x1E
        public Int16 gyaw;          //0x20
        public Int16 groll;         //0x22
        public Int16 q1;            //0x24
        public Int16 q2;            //0x26
        public Int16 q3;            //0x28
        public Int16 q4;            //0x2A
        public Int16 ltrig;         //0x2C
        public Int16 rtrig;         //0x2E
        public Int16 lthumb_x;      //0x30
        public Int16 lthumb_y;      //0x32
        public Int16 rthumb_x;      //0x34
        public Int16 rthumb_y;      //0x36
        public Int16 lpad_pressure; //0x38
        public Int16 rpad_pressure;	//0x3A
    }

    public enum NCHapticStyle : byte
    {
        Disabled = 0,
        Weak = 1,
        Strong = 2
    };

    // TODO: this should be Pack = 1, whole packet needs verification with USBpcap
    [StructLayout(LayoutKind.Sequential)]
    internal struct NCHapticPacket2
    {
        public byte packet_type;       // = 0xea;
        public byte len;               // = 0xd;
        public SCHapticMotor position; // = HapticPad.Left;
        public NCHapticStyle style;    // = HapticStyle.Strong; //
        public byte unsure2;           // = 0x0;
        public sbyte intensity;        // = 0x00; // -7..5 => -2dB..10dB
        public byte unsure3;           // = 0x4;
        public int tsA;                // = 0; // timestamp?
        public int tsB;                // = 0;
    }
}
