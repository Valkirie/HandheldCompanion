using HandheldCompanion.Devices;
using HandheldCompanion.Processors.Intel;
using System;
using static HandheldCompanion.Processors.Intel.KX;

namespace HandheldCompanion.Processors
{
    public class IntelProcessor : Processor
    {
        public readonly IntelSignature Signature;
        public KX platform = new();

        public enum IntelMicroArch
        {
            Unknown = 0,
            LunarLake = 1,
        }

        public readonly IntelMicroArch MicroArch;

        public struct IntelSignature
        {
            public uint Raw;      // e.g. 0x000B06D1
            public int Family;    // adjusted per Intel rules
            public int Model;     // adjusted per Intel rules
            public int Stepping;  // low 4 bits
        }

        public IntelProcessor()
        {
            bool platformInit = platform.init();

            // Decode ProcessorID
            Signature = DecodeSignature(ProcessorID);

            // Identify micro-arch (avoid pinning to a single stepping)
            // Family 0x06 is Intel Core family; Model values vary by gen/step.
            // Keep this set extensible if you learn more model codes.
            if (Signature.Family == 0x06 && Signature.Model == 0xBD)
                MicroArch = IntelMicroArch.LunarLake;
            else
                MicroArch = IntelMicroArch.Unknown;

            // Capabilities
            CanChangeTDP = platformInit || HasOEMCPU;
            CanChangeGPU = platformInit || HasOEMGPU;

            IsInitialized = CanChangeTDP || CanChangeGPU;
        }

        private static IntelSignature DecodeSignature(string processorId)
        {
            var sig = new IntelSignature();

            if (string.IsNullOrWhiteSpace(processorId) || processorId.Length < 16)
                return sig;

            try
            {
                // first 8 = feature bits (unused here), last 8 = signature
                sig.Raw = Convert.ToUInt32(processorId.Substring(8), 16);

                int stepping = (int)(sig.Raw & 0xF);
                int model = (int)((sig.Raw >> 4) & 0xF);
                int family = (int)((sig.Raw >> 8) & 0xF);
                int extModel = (int)((sig.Raw >> 16) & 0xF);
                int extFamily = (int)((sig.Raw >> 20) & 0xFF);

                // Intel adjustments
                if (family == 0x0F) family += extFamily;
                if (family == 0x06 || family == 0x0F) model += (extModel << 4);

                sig.Stepping = stepping;
                sig.Model = model;
                sig.Family = family;
            }
            catch { }

            return sig;
        }

        public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
        {
            lock (updateLock)
            {
                if (!CanChangeTDP) return;

                IDevice device = IDevice.GetCurrent();

                // MSI Claw quirk
                bool forceOEM = device is ClawA1M claw && claw.GetOverBoost();

                if (HasOEMCPU && (UseOEM || forceOEM))
                {
                    switch (type)
                    {
                        case PowerType.Slow: device.set_long_limit((int)limit); break;
                        case PowerType.Fast: device.set_short_limit((int)limit); break;
                    }
                }
                else
                {
                    switch (type)
                    {
                        case PowerType.Slow: result = platform.set_long_limit((int)limit); break;
                        case PowerType.Fast: result = platform.set_short_limit((int)limit); break;
                    }
                }

                base.SetTDPLimit(type, limit, immediate, result);
            }
        }

        public void SetMSRLimit(double PL1, double PL2)
        {
            lock (updateLock)
            {
                if (!CanChangeTDP) return;

                if (HasOEMCPU && UseOEM)
                {
                    // OEM path if/when implemented
                }
                else
                {
                    platform.set_msr_limits((int)PL1, (int)PL2);
                }
            }
        }

        public bool SetMSRUndervolt(IntelUndervoltRail rail, int offsetMv)
        {
            lock (updateLock)
            {
                // Pick command field based on rail (same values as msr-cmd script)
                string commandHex = rail switch
                {
                    IntelUndervoltRail.Core => "0x80000011",
                    IntelUndervoltRail.Gpu => "0x80000111",
                    IntelUndervoltRail.Cache => "0x80000211",
                    IntelUndervoltRail.SystemAgent => "0x80000411",
                    _ => throw new ArgumentOutOfRangeException(nameof(rail), rail, null)
                };

                return platform.set_msr_undervolt(commandHex, offsetMv) == 0;
            }
        }

        public override void SetGPUClock(double clock, int result)
        {
            lock (updateLock)
            {
                if (!CanChangeGPU) return;

                if (HasOEMGPU)
                {
                    // OEM path if/when implemented
                }
                else
                {
                    result = platform.set_gfx_clk((int)clock);
                }

                base.SetGPUClock(clock, result);
            }
        }
    }
}