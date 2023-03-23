using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using System;
using System.Linq;
using System.Runtime.Intrinsics.Arm;

namespace ControllerCommon.Devices
{
    public class SteamDeck : IDevice
    {
        // Those addresses are taken from DSDT for VLV0100
        // and might change at any time with a BIOS update
        // Purpose: https://lore.kernel.org/lkml/20220206022023.376142-1-andrew.smirnov@gmail.com/
        // Addresses: DSDT.txt
        // Author: Kamil Trzciński, 2022 (https://github.com/ayufan/steam-deck-tools/)
        private static IntPtr FSLO_FSHI = new IntPtr(0xFE700B00 + 0x92);
        private static IntPtr GNLO_GNHI = new IntPtr(0xFE700B00 + 0x95);
        private static IntPtr FRPR = new IntPtr(0xFE700B00 + 0x97);
        private static IntPtr FNRL_FNRH = new IntPtr(0xFE700300 + 0xB0);
        private static IntPtr FNCK = new IntPtr(0xFE700300 + 0x9F);
        private static IntPtr BATH_BATL = new IntPtr(0xFE700400 + 0x6E);
        private static IntPtr PDFV = new IntPtr(0xFE700C00 + 0x4C);
        private static IntPtr XBID = new IntPtr(0xFE700300 + 0xBD);
        private static IntPtr PDCT = new IntPtr(0xFE700C00 + 0x01);
        private const ushort IO6C = 0x6C;
        public const ushort MAX_FAN_RPM = 0x1C84;

        public static readonly ushort[] SupportedFirmwares = {
            0xB030 // 45104
        };

        public static readonly byte[] SupportedBoardID = {
            6
        };

        public static readonly byte[] SupportedPDCS = {
            0x2B // 43
        };
        
        private InpOut inpOut;

        public static ushort FirmwareVersion { get; private set; }
        public static byte BoardID { get; private set; }
        public static byte PDCS { get; private set; }

        public SteamDeck() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_valve_jupiter";
            this.ProductModel = "SteamDeck";

            // Steam Controller Neptune
            this.Capacities = DeviceCapacities.ControllerSensor | DeviceCapacities.Trackpads | DeviceCapacities.FanControl;

            // https://www.steamdeck.com/en/tech
            this.nTDP = new double[] { 10, 10, 15 };
            this.cTDP = new double[] { 4, 15 };

            // https://www.techpowerup.com/gpu-specs/steam-deck-gpu.c3897
            this.GfxClock = new double[] { 100, 1600 };

            OEMChords.Add(new DeviceChord("STEAM",
                new(), new(),
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("...",
                new(), new(),
                false, ButtonFlags.OEM2
                ));
        }

        public override bool IsOpen
        {
            get { return inpOut is not null; }
        }

        public override bool IsSupported
        {
            get
            {
                return SupportedFirmwares.Contains(FirmwareVersion) &&
                    SupportedBoardID.Contains(BoardID);
            }
        }

        public override bool Open()
        {
            if (inpOut != null)
                return true;

            try
            {
                inpOut = new InpOut();

                var data = inpOut?.ReadMemory(PDFV, 2);
                if (data is not null)
                    FirmwareVersion = BitConverter.ToUInt16(data);
                else
                    FirmwareVersion = 0xFFFF;

                data = inpOut?.ReadMemory(XBID, 1);
                if (data is not null)
                    BoardID = data[0];
                else
                    BoardID = 0xFF;

                data = inpOut?.ReadMemory(PDCT, 1);
                if (data is not null)
                    PDCS = data[0];
                else
                    PDCS = 0xFF;

                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialise VLV0100. ErrorCode: {0}", ex.Message);
                Close();
                return false;
            }
        }

        public override void Close()
        {
            inpOut.Dispose();
            inpOut = null;
        }

        private void SetGain(ushort gain)
        {
            if (!IsOpen || !IsSupported)
                return;

            byte[] data = BitConverter.GetBytes(gain);
            inpOut.WriteMemory(GNLO_GNHI, data);
        }

        private void SetRampRate(byte rampRate)
        {
            if (!IsOpen || !IsSupported)
                return;

            byte[] data = BitConverter.GetBytes((short)rampRate);
            inpOut.WriteMemory(FRPR, data);
        }

        public override void SetFanControl(bool enable)
        {
            if (!IsOpen || !IsSupported)
                return;

            SetGain(10);
            SetRampRate(enable ? (byte)10 : (byte)20);

            inpOut.DlPortWritePortUchar(IO6C, enable ? (byte)0xCC : (byte)0xCD);
        }

        public override void SetFanDuty(double percent)
        {
            if (!IsOpen || !IsSupported)
                return;

            ushort rpm = (ushort)(MAX_FAN_RPM * percent / 100.0d);
            if (rpm > MAX_FAN_RPM)
                rpm = MAX_FAN_RPM;

            byte[] data = BitConverter.GetBytes(rpm);
            inpOut.WriteMemory(FSLO_FSHI, data);
        }
    }
}
