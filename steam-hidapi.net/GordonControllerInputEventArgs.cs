using System;

namespace steam_hidapi.net
{
    public class GordonControllerInputEventArgs : EventArgs
    {
        public GordonControllerInputState State { get; private set; }

        public GordonControllerInputEventArgs(GordonControllerInputState state)
        {
            State = state;
        }
    }
}
