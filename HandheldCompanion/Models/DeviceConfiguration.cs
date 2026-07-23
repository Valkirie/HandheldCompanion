using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HandheldCompanion.Models
{
    public class DeviceConfiguration
    {
        public string DeviceClass { get; set; } = "";
        public IMUMatrixData? GyroMatrix { get; set; }
        public IMUMatrixData? AcceleroMatrix { get; set; }

        // Hardware specifications
        [JsonPropertyName("cTDP")]
        public double[]? cTDP { get; set; }  // Configurable TDP (down, up)

        [JsonPropertyName("nTDP")]
        public double[]? nTDP { get; set; }  // Nominal TDP (slow, slow, fast)

        [JsonPropertyName("gfxClock")]
        public double[]? GfxClock { get; set; }  // GPU clock frequency limits

        [JsonPropertyName("cpuClock")]
        public uint? CpuClock { get; set; }  // CPU clock frequency

        [JsonPropertyName("tjmax")]
        public double? Tjmax { get; set; }  // Maximum operating temperature
    }

    public class IMUMatrixData
    {
        public Vector3Data? Axis { get; set; }
        public Dictionary<string, string>? AxisSwap { get; set; }
    }

    public class Vector3Data
    {
        public float X { get; set; } = 1.0f;
        public float Y { get; set; } = 1.0f;
        public float Z { get; set; } = 1.0f;
    }
}
