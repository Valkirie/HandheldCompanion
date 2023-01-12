using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    [Serializable]
    public class ControllerMovements
    {
        public float GyroAccelX, GyroAccelY, GyroAccelZ;
        public float GyroRoll, GyroPitch, GyroYaw;

        public int Timestamp;

        public ControllerMovements()
        { }

        public ControllerMovements(ControllerMovements Inputs)
        {
            GyroAccelX = Inputs.GyroAccelX;
            GyroAccelY = Inputs.GyroAccelY;
            GyroAccelZ = Inputs.GyroAccelZ;

            GyroRoll = Inputs.GyroRoll;
            GyroPitch = Inputs.GyroPitch;
            GyroYaw = Inputs.GyroYaw;
        }
    }
}
