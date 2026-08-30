using HandheldCompanion.Processors.AMD;
using HandheldCompanion.Shared;
using System;
using System.Reflection;
using System.Windows.Media;

namespace HandheldCompanion.Processors.Valve;

public sealed class ValveLedService : IDisposable
{
    private const int LedCount = 17;
    private const int SolidColorMode = 3;
    private const int ManualMode = 7;
    private const int MaxBrightness = 255;
    private const string ModuleResourceName = "HandheldCompanion.Resources.PawnIO.LedsValve.bin";

    private readonly PawnIOWrapper pawnIO = new();
    private bool initialized;

    public bool IsInitialized => initialized;

    public bool Initialize()
    {
        if (initialized)
            return true;

        if (!pawnIO.Connect() ||
            !pawnIO.LoadModuleFromResource(Assembly.GetExecutingAssembly(), ModuleResourceName) ||
            !Execute("ioctl_init"))
        {
            pawnIO.Dispose();
            return false;
        }

        initialized = true;
        return true;
    }

    public bool SetEnabled(bool enabled) => Execute("ioctl_set_enabled", enabled ? 1u : 0u);

    public bool SetBrightness(int brightness)
    {
        brightness = Math.Clamp(brightness, 0, 100);
        return Execute("ioctl_set_brightness_scale", (uint)(brightness * MaxBrightness / 100));
    }

    public bool SetSolidColor(Color color)
    {
        return SetColors(color, color, SolidColorMode);
    }

    public bool SetAmbilightColor(Color leftColor, Color rightColor)
    {
        return SetColors(leftColor, rightColor, ManualMode);
    }

    private bool SetColors(Color startColor, Color endColor, int mode)
    {
        if (!initialized)
            return false;

        for (uint led = 0; led < LedCount; led++)
        {
            int red = startColor.R + (endColor.R - startColor.R) * (int)led / (LedCount - 1);
            int green = startColor.G + (endColor.G - startColor.G) * (int)led / (LedCount - 1);
            int blue = startColor.B + (endColor.B - startColor.B) * (int)led / (LedCount - 1);

            if (!Execute("ioctl_set_led_color", led, (uint)red, (uint)green, (uint)blue))
                return false;
        }

        return Execute("ioctl_set_effect", (uint)mode);
    }

    public void Dispose()
    {
        pawnIO.Dispose();
        initialized = false;
    }

    private bool Execute(string functionName, params uint[] input)
    {
        if (!initialized && functionName != "ioctl_init")
            return false;

        return pawnIO.ExecuteFunction(functionName, Array.ConvertAll(input, static value => (ulong)value), []);
    }
}
