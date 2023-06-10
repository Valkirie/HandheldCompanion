using MemoryPack;
using System;

namespace ControllerCommon.Controllers;

[Serializable]
[MemoryPackable]
public partial class ControllerMovements
{
    public float GyroAccelX, GyroAccelY, GyroAccelZ;
    public float GyroRoll, GyroPitch, GyroYaw;

    public long TickCount;

    public ControllerMovements()
    {
    }

    [MemoryPackConstructor]
    public ControllerMovements(float gyroAccelX, float gyroAccelY, float gyroAccelZ, float gyroRoll, float gyroPitch, float gyroYaw)
    {
        this.GyroAccelX = gyroAccelX;
        this.GyroAccelY = gyroAccelY;
        this.GyroAccelZ = gyroAccelZ;

        this.GyroRoll = gyroRoll;
        this.GyroPitch = gyroPitch;
        this.GyroYaw = gyroYaw;
    }
}