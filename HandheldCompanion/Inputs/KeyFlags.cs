using GregsStack.InputSimulatorStandard.Native;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace HandheldCompanion.Inputs
{
    [Serializable]
    public enum KeyFlags
    {
        Escape = VirtualKeyCode.ESCAPE,
        Enter = VirtualKeyCode.RETURN,
        Space = VirtualKeyCode.SPACE,
        Backspace = VirtualKeyCode.BACK,
        Tab = VirtualKeyCode.TAB,
        [Description("Caps Lock")]
        CapsLock = VirtualKeyCode.CAPITAL,
        [Description("Arrow Left")]
        ArrowLeft = VirtualKeyCode.LEFT,
        [Description("Arrow Up")]
        ArrowUp = VirtualKeyCode.UP,
        [Description("Arrow Right")]
        ArrowRight = VirtualKeyCode.RIGHT,
        [Description("Arrow Down")]
        ArrowDown = VirtualKeyCode.DOWN,
        Insert = VirtualKeyCode.INSERT,
        Delete = VirtualKeyCode.DELETE,
        Home = VirtualKeyCode.HOME,
        End = VirtualKeyCode.END,
        [Description("Page Up")]
        PageUp = VirtualKeyCode.PRIOR,
        [Description("Page Down")]
        PageDown = VirtualKeyCode.NEXT,
        Shift = VirtualKeyCode.SHIFT,
        [Description("Left Shift")]
        LeftShift = VirtualKeyCode.LSHIFT,
        [Description("Right Shift")]
        RightShift = VirtualKeyCode.RSHIFT,
        Control = VirtualKeyCode.CONTROL,
        [Description("Left Control")]
        LeftControl = VirtualKeyCode.LCONTROL,
        [Description("Right Control")]
        RightControl = VirtualKeyCode.RCONTROL,
        Alt = VirtualKeyCode.MENU,
        [Description("Left Alt")]
        LeftAlt = VirtualKeyCode.LMENU,
        [Description("Right Alt")]
        RightAlt = VirtualKeyCode.RMENU,
        [Description("Left Win")]
        LeftWin = VirtualKeyCode.LWIN,
        [Description("Right Win")]
        RightWin = VirtualKeyCode.RWIN,
        F1 = VirtualKeyCode.F1,
        F2 = VirtualKeyCode.F2,
        F3 = VirtualKeyCode.F3,
        F4 = VirtualKeyCode.F4,
        F5 = VirtualKeyCode.F5,
        F6 = VirtualKeyCode.F6,
        F7 = VirtualKeyCode.F7,
        F8 = VirtualKeyCode.F8,
        F9 = VirtualKeyCode.F9,
        F10 = VirtualKeyCode.F10,
        F11 = VirtualKeyCode.F11,
        F12 = VirtualKeyCode.F12,
        Grave = VirtualKeyCode.OEM_3,           // `
        [Description("1")]
        VK_1 = VirtualKeyCode.VK_1,
        [Description("2")]
        VK_2 = VirtualKeyCode.VK_2,
        [Description("3")]
        VK_3 = VirtualKeyCode.VK_3,
        [Description("4")]
        VK_4 = VirtualKeyCode.VK_4,
        [Description("5")]
        VK_5 = VirtualKeyCode.VK_5,
        [Description("6")]
        VK_6 = VirtualKeyCode.VK_6,
        [Description("7")]
        VK_7 = VirtualKeyCode.VK_7,
        [Description("8")]
        VK_8 = VirtualKeyCode.VK_8,
        [Description("9")]
        VK_9 = VirtualKeyCode.VK_9,
        [Description("0")]
        VK_0 = VirtualKeyCode.VK_0,
        Minus = VirtualKeyCode.OEM_MINUS,       // -
        Equal = VirtualKeyCode.OEM_PLUS,        // =
        [Description("Left Bracket")]
        LeftBracket = VirtualKeyCode.OEM_4,     // [
        [Description("Right Bracket")]
        RightBracket = VirtualKeyCode.OEM_6,    // ]
        Semicolon = VirtualKeyCode.OEM_1,       // ;
        Apostrophe = VirtualKeyCode.OEM_7,      // '
        Comma = VirtualKeyCode.OEM_COMMA,       // ,
        Period = VirtualKeyCode.OEM_PERIOD,     // .
        Slash = VirtualKeyCode.OEM_2,           // /
        Backslash = VirtualKeyCode.OEM_5,       // \
        [Description("Alt Backslash")]
        AltBackslash = VirtualKeyCode.OEM_102,  // another \
        A = VirtualKeyCode.VK_A,
        B = VirtualKeyCode.VK_B,
        C = VirtualKeyCode.VK_C,
        D = VirtualKeyCode.VK_D,
        E = VirtualKeyCode.VK_E,
        F = VirtualKeyCode.VK_F,
        G = VirtualKeyCode.VK_G,
        H = VirtualKeyCode.VK_H,
        I = VirtualKeyCode.VK_I,
        J = VirtualKeyCode.VK_J,
        K = VirtualKeyCode.VK_K,
        L = VirtualKeyCode.VK_L,
        M = VirtualKeyCode.VK_M,
        N = VirtualKeyCode.VK_N,
        O = VirtualKeyCode.VK_O,
        P = VirtualKeyCode.VK_P,
        Q = VirtualKeyCode.VK_Q,
        R = VirtualKeyCode.VK_R,
        S = VirtualKeyCode.VK_S,
        T = VirtualKeyCode.VK_T,
        U = VirtualKeyCode.VK_U,
        V = VirtualKeyCode.VK_V,
        W = VirtualKeyCode.VK_W,
        X = VirtualKeyCode.VK_X,
        Y = VirtualKeyCode.VK_Y,
        Z = VirtualKeyCode.VK_Z,
        [Description("Print Screen")]
        PrintScreen = VirtualKeyCode.SNAPSHOT,
        [Description("Scroll Lock")]
        ScrollLock = VirtualKeyCode.SCROLL,
        Pause = VirtualKeyCode.PAUSE,
        [Description("Num Lock")]
        NumLock = VirtualKeyCode.NUMLOCK,
        [Description("Num Enter")]
        NunEnter = VirtualKeyCode.NUMPAD_RETURN,
        [Description("Num /")]
        NumDivide = VirtualKeyCode.DIVIDE,
        [Description("Num *")]
        NumMultiply = VirtualKeyCode.MULTIPLY,
        [Description("Num -")]
        NumSubtract = VirtualKeyCode.SUBTRACT,
        [Description("Num +")]
        NumAdd = VirtualKeyCode.ADD,
        [Description("Num .")]
        NumDecimal = VirtualKeyCode.DECIMAL,
        [Description("Num 0")]
        Num0 = VirtualKeyCode.NUMPAD0,
        [Description("Num 1")]
        Num1 = VirtualKeyCode.NUMPAD1,
        [Description("Num 2")]
        Num2 = VirtualKeyCode.NUMPAD2,
        [Description("Num 3")]
        Num3 = VirtualKeyCode.NUMPAD3,
        [Description("Num 4")]
        Num4 = VirtualKeyCode.NUMPAD4,
        [Description("Num 5")]
        Num5 = VirtualKeyCode.NUMPAD5,
        [Description("Num 6")]
        Num6 = VirtualKeyCode.NUMPAD6,
        [Description("Num 7")]
        Num7 = VirtualKeyCode.NUMPAD7,
        [Description("Num 8")]
        Num8 = VirtualKeyCode.NUMPAD8,
        [Description("Num 9")]
        Num9 = VirtualKeyCode.NUMPAD9,
        [Description("Web Backward")]
        WebBack = VirtualKeyCode.BROWSER_BACK,
        [Description("Web Forward")]
        WebForward = VirtualKeyCode.BROWSER_FORWARD,
        [Description("Web Refresh")]
        WebRefresh = VirtualKeyCode.BROWSER_REFRESH,
        [Description("Web Stop")]
        WebStop = VirtualKeyCode.BROWSER_STOP,
        [Description("Web Search")]
        WebSearch = VirtualKeyCode.BROWSER_SEARCH,
        [Description("Web Favorites")]
        WebFavorites = VirtualKeyCode.BROWSER_FAVORITES,
        [Description("Web Home")]
        WebHome = VirtualKeyCode.BROWSER_HOME,
        [Description("Volume Mute")]
        VolumeMute = VirtualKeyCode.VOLUME_MUTE,
        [Description("Volume Down")]
        VolumeDown = VirtualKeyCode.VOLUME_DOWN,
        [Description("Volume Up")]
        VolumeUp = VirtualKeyCode.VOLUME_UP,
        [Description("Media Next")]
        MediaNext = VirtualKeyCode.MEDIA_NEXT_TRACK,
        [Description("Media Prev")]
        MediaPrev = VirtualKeyCode.MEDIA_PREV_TRACK,
        [Description("Media Stop")]
        MediaStop = VirtualKeyCode.MEDIA_STOP,
        [Description("Media Play/Pause")]
        MediaPlayPause = VirtualKeyCode.MEDIA_PLAY_PAUSE,
    }

    public static class KeyFlagsOrder
    {
        static KeyFlagsOrder()
        {
            // make sure we haven't forgot anything
            var count1 = ((KeyFlags[])Enum.GetValues(typeof(KeyFlags))).Length;
            var count2 = arr.Length;
            Debug.Assert(count1 == count2);
        }

        public static KeyFlags[] arr = {
            KeyFlags.Escape,
            KeyFlags.Enter,
            KeyFlags.Space,
            KeyFlags.Backspace,
            KeyFlags.Tab,
            KeyFlags.CapsLock,
            KeyFlags.ArrowLeft,
            KeyFlags.ArrowUp,
            KeyFlags.ArrowRight,
            KeyFlags.ArrowDown,
            KeyFlags.Insert,
            KeyFlags.Delete,
            KeyFlags.Home,
            KeyFlags.End,
            KeyFlags.PageUp,
            KeyFlags.PageDown,
            KeyFlags.Shift,
            KeyFlags.LeftShift,
            KeyFlags.RightShift,
            KeyFlags.Control,
            KeyFlags.LeftControl,
            KeyFlags.RightControl,
            KeyFlags.Alt,
            KeyFlags.LeftAlt,
            KeyFlags.RightAlt,
            KeyFlags.LeftWin,
            KeyFlags.RightWin,
            KeyFlags.F1,
            KeyFlags.F2,
            KeyFlags.F3,
            KeyFlags.F4,
            KeyFlags.F5,
            KeyFlags.F6,
            KeyFlags.F7,
            KeyFlags.F8,
            KeyFlags.F9,
            KeyFlags.F10,
            KeyFlags.F11,
            KeyFlags.F12,
            KeyFlags.Grave,
            KeyFlags.VK_1,
            KeyFlags.VK_2,
            KeyFlags.VK_3,
            KeyFlags.VK_4,
            KeyFlags.VK_5,
            KeyFlags.VK_6,
            KeyFlags.VK_7,
            KeyFlags.VK_8,
            KeyFlags.VK_9,
            KeyFlags.VK_0,
            KeyFlags.Minus,
            KeyFlags.Equal,
            KeyFlags.LeftBracket,
            KeyFlags.RightBracket,
            KeyFlags.Semicolon,
            KeyFlags.Apostrophe,
            KeyFlags.Comma,
            KeyFlags.Period,
            KeyFlags.Slash,
            KeyFlags.Backslash,
            KeyFlags.AltBackslash,
            KeyFlags.A,
            KeyFlags.B,
            KeyFlags.C,
            KeyFlags.D,
            KeyFlags.E,
            KeyFlags.F,
            KeyFlags.G,
            KeyFlags.H,
            KeyFlags.I,
            KeyFlags.J,
            KeyFlags.K,
            KeyFlags.L,
            KeyFlags.M,
            KeyFlags.N,
            KeyFlags.O,
            KeyFlags.P,
            KeyFlags.Q,
            KeyFlags.R,
            KeyFlags.S,
            KeyFlags.T,
            KeyFlags.U,
            KeyFlags.V,
            KeyFlags.W,
            KeyFlags.X,
            KeyFlags.Y,
            KeyFlags.Z,
            KeyFlags.PrintScreen,
            KeyFlags.ScrollLock,
            KeyFlags.Pause,
            KeyFlags.NumLock,
            KeyFlags.NunEnter,
            KeyFlags.NumDivide,
            KeyFlags.NumMultiply,
            KeyFlags.NumSubtract,
            KeyFlags.NumAdd,
            KeyFlags.NumDecimal,
            KeyFlags.Num0,
            KeyFlags.Num1,
            KeyFlags.Num2,
            KeyFlags.Num3,
            KeyFlags.Num4,
            KeyFlags.Num5,
            KeyFlags.Num6,
            KeyFlags.Num7,
            KeyFlags.Num8,
            KeyFlags.Num9,
            KeyFlags.WebBack,
            KeyFlags.WebForward,
            KeyFlags.WebRefresh,
            KeyFlags.WebStop,
            KeyFlags.WebSearch,
            KeyFlags.WebFavorites,
            KeyFlags.WebHome,
            KeyFlags.VolumeMute,
            KeyFlags.VolumeDown,
            KeyFlags.VolumeUp,
            KeyFlags.MediaNext,
            KeyFlags.MediaPrev,
            KeyFlags.MediaStop,
            KeyFlags.MediaPlayPause,
        };
    }
}