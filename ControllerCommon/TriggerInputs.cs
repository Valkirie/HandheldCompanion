using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsInput.Events;
using WindowsInput.Events.Sources;

namespace ControllerCommon
{
    [Flags]
    public enum TriggerInputsType
    {
        Gamepad,
        Keyboard
    }

    public class TriggerInputs
    {
        public TriggerInputsType type;
        public GamepadButtonFlags buttons;
        public ChordClick chord = new();
        public string raw;

        public TriggerInputs(TriggerInputsType type, string value)
        {
            this.type = type;
            this.raw = value;

            switch(type)
            {
                default:
                case TriggerInputsType.Gamepad:
                    {
                        this.buttons = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), value, true);
                    }
                    break;

                case TriggerInputsType.Keyboard:
                    {
                        string[] keys = value.Split(',').ToArray();
                        List<KeyCode> keys2 = new();

                        foreach (string key in keys)
                        {
                            try
                            {
                                KeyCode code = (KeyCode)Enum.Parse(typeof(KeyCode), key, true);
                                keys2.Add(code);
                            } catch (Exception) { }
                        }
                        this.chord = new ChordClick(keys2);
                    }
                    break;
            }
        }

        public string GetValue()
        {
            switch(type)
            {
                default:
                case TriggerInputsType.Gamepad:
                    return this.buttons.ToString();

                case TriggerInputsType.Keyboard:
                    return string.Join(",", this.chord.Keys);
            }
        }
    }
}
