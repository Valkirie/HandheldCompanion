using System;
using System.Windows.Forms;

namespace HandheldCompanion.Inputs
{
    [Serializable]
    public class InputsKey
    {
        public int KeyValue { get; set; }
        public int ScanCode { get; set; }
        public int Timestamp { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsKeyUp { get; set; }
        public bool IsExtendedKey { get; set; }

        public override string ToString()
        {
            return ((Keys)KeyValue).ToString();
        }
    }
}
