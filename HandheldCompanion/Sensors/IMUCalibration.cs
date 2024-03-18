using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Sensors
{
    public class IMUCalibration
    {
        public float xOffset;
        public float yOffset;
        public float zOffset;
        public int weight;

        public IMUCalibration()
        { }

        public IMUCalibration(float xOffset, float yOffset, float zOffset, int weight)
        {
            this.xOffset = xOffset;
            this.yOffset = yOffset;
            this.zOffset = zOffset;
            this.weight = weight;
        }
    }
}
