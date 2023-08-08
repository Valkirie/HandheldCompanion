using System;

namespace steam_hidapi.net
{
    public class NeptuneControllerInputEventArgs : EventArgs
    {
        public NeptuneControllerInputState State { get; private set; }

        public NeptuneControllerInputEventArgs(NeptuneControllerInputState state)
        {
            State = state;
        }
    }
}
