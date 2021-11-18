using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerHelper
{
    class Controller
    {
        public string name;
        public Guid guid;
        public int index;

        public Controller(string name, Guid guid, int index)
        {
            this.name = name;
            this.guid = guid;
            this.index = index;
        }

        public override string ToString()
        {
            return this.name;
        }
    }

    class HIDmode
    {
        public string mode;
        public string name;

        public HIDmode(string mode, string name)
        {
            this.mode = mode;
            this.name = name;
        }

        public override string ToString()
        {
            return this.name;
        }
    }
}
