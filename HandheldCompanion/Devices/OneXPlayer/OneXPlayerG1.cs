using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices
{
    public class OneXPlayerG1 : OneXAOKZOE
    {
        protected HidDevice? hidDevice;
        protected const int PID_LED = 0xB001;

        public OneXPlayerG1()
        {
            vendorId = 0x1A2C;
            productIds = [PID_LED];
            hidFilters = new()
            {
                { PID_LED, new HidFilter(unchecked((short)0xFF01), unchecked(0x0001)) },
            };

            // Device capabilities
            Capabilities |= DeviceCapabilities.DynamicLighting;
            Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

            // LED features exposed in HC
            DynamicLightingCapabilities |= LEDLevel.SolidColor;
            DynamicLightingCapabilities |= LEDLevel.Rainbow;
        }

        public override bool IsReady()
        {
            // Bind the LED HID device (same pattern as OneXPlayerOneXFly)
            IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                    continue;

                if (device.Capabilities.UsagePage != hidFilter.UsagePage ||
                    device.Capabilities.Usage != hidFilter.Usage)
                    continue;

                hidDevice = device;
                break;
            }

            // We don’t require RGB to be considered "ready"
            return base.IsReady();
        }

        public override bool SetLedBrightness(int brightness)
        {
            // G1 uses the same HID v1 brightness command as X1 Mini / A1X
            return SendV1Brightness(hidDevice, brightness, 0x00);
        }

        public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
        {
            if (!DynamicLightingCapabilities.HasFlag(level))
                return false;

            return level switch
            {
                // HHD gen_rgb_solid(r,g,b, side=0x00)
                LEDLevel.SolidColor => SendV1SolidColor(hidDevice, mainColor, 0x00),

                // Map "Rainbow" to the built-in "flowing" effect (gen_rgb_mode("flowing"))
                LEDLevel.Rainbow => SendV1Rainbow(hidDevice, 0x00),

                _ => false
            };
        }
    }

    public class OneXPlayerG1AMD : OneXPlayerG1
    {
        public OneXPlayerG1AMD()
        {
            ProductIllustration = "device_onexplayer_g1";
            ProductModel = "ONEXPLAYERG1AMD";

            if (!string.IsNullOrEmpty(Processor))
            {
                // Ryzen AI 9 HX 370 – higher ceiling
                if (Processor.Contains("HX 370", StringComparison.OrdinalIgnoreCase))
                {
                    nTDP = new double[] { 25, 35, 65 };
                    cTDP = new double[] { 25, 65 };
                }
                // Ryzen 7 8840U – more modest handheld envelope
                else if (Processor.Contains("8840U", StringComparison.OrdinalIgnoreCase))
                {
                    nTDP = new double[] { 15, 28, 35 };
                    cTDP = new double[] { 15, 35 };
                }
            }

            // Same IMU orientation pattern as other modern ONEX devices
            GyrometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
            AccelerometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        }
    }

    public class OneXPlayerG1Intel : OneXPlayerG1
    {
        public OneXPlayerG1Intel()
        {
            ProductIllustration = "device_onexplayer_g1";
            ProductModel = "ONEXPLAYERG1INTEL";

            // Core Ultra 7 255H class – again, conservative handheld defaults
            nTDP = new double[] { 25, 35, 80 };
            cTDP = new double[] { 25, 80 };
            GfxClock = new double[] { 100, 2250 };
            CpuClock = 5100;

            GyrometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
            AccelerometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        }
    }
}
