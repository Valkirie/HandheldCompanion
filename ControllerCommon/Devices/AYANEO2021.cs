using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon.Devices
{
    public class OXPAMDMini : Device
    {
        public OXPAMDMini(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName, "BMI160", new DeviceController(0x045E, 0x028E))
        {
        }
    }
}
