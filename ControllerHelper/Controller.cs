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
        public bool gyrometer;
        public bool accelerometer;

        public Controller(string name, Guid guid, int index, bool gyrometer, bool accelerometer)
        {
            this.name = name;
            this.guid = guid;
            this.index = index;
            this.gyrometer = gyrometer;
            this.accelerometer = accelerometer;
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
