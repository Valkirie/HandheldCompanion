using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace HandheldCompanion.DSU
{
    public class ClientRequestTimes
    {
        DateTime allPads;
        DateTime[] padIds;
        Dictionary<PhysicalAddress, DateTime> padMacs;

        public DateTime AllPadsTime { get { return allPads; } }
        public DateTime[] PadIdsTime { get { return padIds; } }
        public Dictionary<PhysicalAddress, DateTime> PadMacsTime { get { return padMacs; } }

        public ClientRequestTimes()
        {
            allPads = DateTime.MinValue;
            padIds = new DateTime[4];

            for (int i = 0; i < padIds.Length; i++)
                padIds[i] = DateTime.MinValue;

            padMacs = [];
        }

        public void RequestPadInfo(byte regFlags, byte idToReg, PhysicalAddress macToReg)
        {
            if (regFlags == 0)
                allPads = DateTime.UtcNow;
            else
            {
                if ((regFlags & 0x01) != 0) //id valid
                {
                    if (idToReg < padIds.Length)
                        padIds[idToReg] = DateTime.UtcNow;
                }
                if ((regFlags & 0x02) != 0) //mac valid
                {
                    padMacs[macToReg] = DateTime.UtcNow;
                }
            }
        }
    }
}
