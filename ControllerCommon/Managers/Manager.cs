using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Managers
{
    public abstract class Manager
    {
        public bool IsEnabled { get; set; }
        public bool IsInitialized { get; set; }

        public virtual void Start()
        {
            IsInitialized = true;
        }

        public virtual void Stop()
        {
        }
    }
}
