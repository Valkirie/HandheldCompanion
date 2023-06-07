using System;

namespace neptune_hidapi.net
{
    public class NeptuneControllerInputEventArgs : EventArgs
    {
        public NeptuneControllerInputEventArgs(NeptuneControllerInputState state)
        {
            State = state;
        }

        public NeptuneControllerInputState State { get; private set; }
    }
}