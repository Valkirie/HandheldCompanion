using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerHelper
{
    public class Controller
    {
        public string ProductName;
        public Guid ProductGuid;
        public Guid InstanceGuid;
        public int ProductIndex;

        public Controller(string ProductName, Guid InstanceGuid, Guid ProductGuid, int ProductIndex)
        {
            this.ProductName = ProductName;
            this.InstanceGuid = InstanceGuid;
            this.ProductGuid = ProductGuid;
            this.ProductIndex = ProductIndex;
        }

        public override string ToString()
        {
            return this.ProductName;
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
