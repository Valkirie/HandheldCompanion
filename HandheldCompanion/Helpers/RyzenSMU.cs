using HandheldCompanion.Managers;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HandheldCompanion.Helpers
{
    public class RyzenSMU : IDisposable
    {
        public IntPtr MMIO_ADDR;
        public uint MMIO_SIZE;
        public uint RES_ADDR, MSG_ADDR, PARAM_ADDR;
        public uint INDEX_ADDR, DATA_ADDR;

        IntPtr mappedAddress;
        IntPtr physicalHandle;
        InpOut? inpOut;

        public RyzenSMU()
        {
        }

        ~RyzenSMU()
        {
            Dispose();
        }

        public bool Opened
        {
            get { return physicalHandle != IntPtr.Zero && mappedAddress != IntPtr.Zero; }
        }

        public bool Open()
        {
            if (physicalHandle != IntPtr.Zero)
                return true;
            if (MMIO_ADDR == IntPtr.Zero || MMIO_SIZE == 0)
                return false;

            try
            {
                inpOut = new InpOut();
                mappedAddress = inpOut.MapPhysToLin(MMIO_ADDR, MMIO_SIZE, out physicalHandle);
            }
            catch (Exception e)
            {
                LogManager.LogError("RyzenSMU", e);
                return false;
            }

            return Opened;
        }

        public void Dispose()
        {
            if (physicalHandle == IntPtr.Zero)
                return;

            GC.SuppressFinalize(this);
            inpOut?.UnmapPhysicalMemory(physicalHandle, mappedAddress);
            mappedAddress = IntPtr.Zero;
            physicalHandle = IntPtr.Zero;
            using (inpOut) { }
            inpOut = null;
        }

        private uint RregRaw(uint reg)
        {
            return (uint)Marshal.ReadInt32(mappedAddress, (int)reg);
        }

        private void WregRaw(uint reg, uint value)
        {
            Marshal.WriteInt32(mappedAddress, (int)reg, (int)value);
        }

        private bool WregCheckedRaw(uint reg, uint value)
        {
            WregRaw(reg, value);
            return RregRaw(reg) == value;
        }

        private bool Wreg(uint reg, uint value)
        {
            if (!Opened)
                return false;

            bool success = false;

            try
            {
                if (reg < MMIO_SIZE)
                {
                    return success = WregCheckedRaw(reg, value);
                }
                else
                {
                    if (!WregCheckedRaw(INDEX_ADDR, reg))
                        return false;
                    if (!WregCheckedRaw(DATA_ADDR, value))
                        return false;
                }

                return success = true;
            }
            finally
            {
                LogManager.LogTrace("Wreg: reg={0:X}, value={1:X} => success={2}",
                       reg, value, success);
            }
        }

        private bool Rreg(uint reg, out uint value)
        {
            value = default;

            if (!Opened)
                return false;

            bool success = false;

            try
            {
                if (reg < MMIO_SIZE)
                {
                    value = RregRaw(reg);
                }
                else
                {
                    if (!WregCheckedRaw(INDEX_ADDR, reg))
                        return false;
                    value = RregRaw(DATA_ADDR);
                }

                return success = true;
            }
            finally
            {
                LogManager.LogTrace("Rreg: reg={0:X} => read={1}/{1:X}, success={2}",
                       reg, value, success);
            }
        }

        private uint WaitForResponse()
        {
            const int timeout = 20;
            for (int i = 0; i < timeout; i++)
            {
                uint value;
                if (!Rreg(RES_ADDR, out value))
                    return 0;
                if (value != 0)
                    return value;
                Thread.SpinWait(100);
            }
            return 0;
        }

        public bool SendMsg(ushort msg, uint param)
        {
            return SendMsg(msg, param, out _);
        }

        public bool SendMsg(ushort msg, uint param, out uint arg)
        {
            bool success = false;

            arg = 0;

            try
            {
                var res = WaitForResponse();
                if (res != 0x1)
                {
                    // Reset SMU state
                    if (res != 0)
                        Wreg(RES_ADDR, 1);
                    return false;
                }

                Wreg(RES_ADDR, 0);
                Wreg(PARAM_ADDR, param);
                Wreg(MSG_ADDR, msg);

                res = WaitForResponse();
                if (res != 0x1)
                    return false;

                success = Rreg(PARAM_ADDR, out arg);
                return success;
            }
            finally
            {
                LogManager.LogTrace(">> SendMsg: msg={0:X}, param={1:X} => arg={2}/{2:X}, success={3}",
                       msg, param, arg, success);
            }
        }

        public bool SendMsg<T>(T msg, uint param)
        {
            return SendMsg((ushort)(object)msg, param, out _);
        }

        public bool SendMsg<T>(T msg, uint param, out uint arg) where T : unmanaged
        {
            return SendMsg((ushort)(object)msg, param, out arg);
        }
    }
}
