using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace neptune_hidapi.net
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
