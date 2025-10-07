using HandheldCompanion.Devices;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Models;
using HandheldCompanion.Shared;
using Microsoft.Win32;
using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class DynamicLightingManager
{
    public static bool IsInitialized;
    private static ColorTracker leftLedTracker;
    private static ColorTracker rightLedTracker;
    private static Color previousColorLeft;
    private static Color previousColorRight;

    private static readonly Timer DynamicLightingTimer;

    private static Direct3D? direct3D;
    private static Device? device;
    private static Surface? surface;

    private static DataRectangle dataRectangle;
    private static IntPtr dataPointer;

    private static int screenWidth;
    private static int screenHeight;

    private static int squareSize = 100;
    private const int squareStep = 10;

    private static Thread ambilightThread;
    private static bool ambilightThreadRunning;
    private static int ambilightThreadDelay = 33;

    private static readonly object d3dLock = new object();

    private static bool VerticalBlackBarDetectionEnabled;

    private static bool OSAmbientLightingEnabled = false;
    private const string OSAmbientLightingEnabledKey = "AmbientLightingEnabled";

    private static bool LEDSettingsEnabled => ManagerFactory.settingsManager.GetBoolean("LEDSettingsEnabled");
    private static bool AmbilightEnabled => LEDSettingsLevel == LEDLevel.Ambilight;

    private static LEDLevel LEDSettingsLevel => (LEDLevel)ManagerFactory.settingsManager.GetInt("LEDSettingsLevel");
    private static int LEDBrightness => ManagerFactory.settingsManager.GetInt("LEDBrightness");
    private static int LEDSpeed => ManagerFactory.settingsManager.GetInt("LEDSpeed");
    private static Color LEDMainColor => ManagerFactory.settingsManager.GetColor("LEDMainColor");
    private static Color LEDSecondColor => ManagerFactory.settingsManager.GetColor("LEDSecondColor");
    private static bool useSecondColor => ManagerFactory.settingsManager.GetBoolean("LEDUseSecondColor");
    private static int LEDPresetIndex => ManagerFactory.settingsManager.GetInt("LEDPresetIndex");

    static DynamicLightingManager()
    {
        // Keep track of left and right LEDs history
        leftLedTracker = new ColorTracker();
        rightLedTracker = new ColorTracker();

        ambilightThread = new Thread(ambilightThreadLoop)
        {
            IsBackground = true
        };

        DynamicLightingTimer = new(125)
        {
            AutoReset = false
        };
        DynamicLightingTimer.Elapsed += (sender, e) => UpdateLED();
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // store and disable system setting AmbientLightingEnabled
        OSAmbientLightingEnabled = GetAmbientLightingEnabled();
        SetAmbientLightingEnabled(false);

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        switch (ManagerFactory.multimediaManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.multimediaManager.Initialized += MultimediaManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryMedia();
                break;
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "DynamicLightingManager");
    }

    private static void QueryMedia()
    {
        // manage events
        ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

        if (ManagerFactory.multimediaManager.PrimaryDesktop is not null)
            MultimediaManager_DisplaySettingsChanged(ManagerFactory.multimediaManager.PrimaryDesktop, ManagerFactory.multimediaManager.PrimaryDesktop.GetResolution());
    }

    private static void MultimediaManager_Initialized()
    {
        QueryMedia();
    }

    private static void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private static void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("LEDAmbilightVerticalBlackBarDetection", ManagerFactory.settingsManager.GetString("LEDAmbilightVerticalBlackBarDetection"), false);
        RequestUpdate();
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
        ManagerFactory.multimediaManager.Initialized -= MultimediaManager_Initialized;

        StopAmbilight();

        // dispose resources
        DisposeDirect3DResources();

        // restore system setting AmbientLightingEnabled
        SetAmbientLightingEnabled(OSAmbientLightingEnabled);

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "DynamicLightingManager");
    }

    private static bool GetAmbientLightingEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Lighting"))
            if (key != null)
                return Convert.ToBoolean(key.GetValue(OSAmbientLightingEnabledKey));

        return false;
    }

    private static void SetAmbientLightingEnabled(bool enabled)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Lighting", writable: true))
            if (key != null)
                key.SetValue(OSAmbientLightingEnabledKey, enabled ? 1 : 0);
    }

    private static void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        // Update the screen width and height values when display changes
        // Get the primary screen dimensions
        screenWidth = resolution.Width;
        screenHeight = resolution.Height;
        squareSize = (int)Math.Floor((decimal)screenWidth / 10);

        // Restart Ambilight if necessary
        RequestUpdate();
    }

    private static void DisposeDirect3DResources()
    {
        // Dispose in the correct order: Child resources -> Device -> Direct3D
        surface?.Dispose();
        surface = null;

        device?.Dispose();
        device = null;

        direct3D?.Dispose();
        direct3D = null;
    }

    private static bool InitializeDirect3DDevice(int maxAttempts = 3)
    {
        // Try to enter the critical section without waiting.
        if (!Monitor.TryEnter(d3dLock, TimeSpan.Zero))
            return false;

        try
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Ensure clean-up before re-initialization
                    DisposeDirect3DResources();

                    // Create a Direct3D instance
                    direct3D = new Direct3D();

                    // Create a Device to access the screen
                    device = new Device(
                        direct3D,
                        0,
                        DeviceType.Hardware,
                        IntPtr.Zero,
                        CreateFlags.SoftwareVertexProcessing,
                        new PresentParameters(screenWidth, screenHeight)
                    );

                    // Create a Surface to capture the screen
                    surface = Surface.CreateOffscreenPlain(
                        device,
                        screenWidth,
                        screenHeight,
                        Format.A8R8G8B8,
                        Pool.SystemMemory
                    );

                    return true;
                }
                catch { }

                // Wait before retrying, if not the last attempt.
                if (attempt < maxAttempts)
                    Task.Delay(3000).Wait();
            }

            LogManager.LogError("Failed to initialize Direct3D resources after {0} attempts", maxAttempts);
            return false;
        }
        catch { return false; }
        finally
        {
            Monitor.Exit(d3dLock);
        }
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "LEDSettingsEnabled":
            case "LEDBrightness":
            case "LEDMainColor":
            case "LEDSecondColor":
            case "LEDSettingsLevel":
            case "LEDSpeed":
            case "LEDUseSecondColor":
            case "LEDPresetIndex":
                RequestUpdate();
                break;

            case "LEDAmbilightVerticalBlackBarDetection":
                VerticalBlackBarDetectionEnabled = Convert.ToBoolean(value);
                break;
        }
    }

    private static void RequestUpdate()
    {
        DynamicLightingTimer.Stop();
        DynamicLightingTimer.Start();
    }

    private static void UpdateLED()
    {
        IDevice device = IDevice.GetCurrent();
        device.SetLedStatus(LEDSettingsEnabled);

        if (!LEDSettingsEnabled)
        {
            StopAmbilight();
            device.SetLedBrightness(0);
            device.SetLedColor(Colors.Black, Colors.Black, LEDLevel.SolidColor);
            return;
        }

        // Get LED settings
        device.SetLedBrightness(LEDBrightness);

        // Get preset
        List<LEDPreset> presets = device.LEDPresets;
        LEDPreset? selectedPreset = LEDPresetIndex < presets.Count ? presets[LEDPresetIndex] : null;

        // Stop Ambilight if needed
        if (LEDSettingsLevel != LEDLevel.Ambilight)
            StopAmbilight();

        // Handle LED levels
        switch (LEDSettingsLevel)
        {
            case LEDLevel.SolidColor:
            case LEDLevel.Breathing:
            case LEDLevel.Rainbow:
            case LEDLevel.Wave:
            case LEDLevel.Wheel:
            case LEDLevel.Gradient:
                device.SetLedColor(LEDMainColor, useSecondColor ? LEDSecondColor : LEDMainColor, LEDSettingsLevel, LEDSpeed);
                break;

            case LEDLevel.Ambilight:
                {
                    device.SetLedColor(Colors.Black, Colors.Black, LEDLevel.SolidColor);

                    if (!ambilightThreadRunning)
                        if (InitializeDirect3DDevice())
                            StartAmbilight();
                }
                break;

            case LEDLevel.LEDPreset:
                device.SetLEDPreset(selectedPreset);
                break;
        }
    }

    private const int MinTotalDelta = 18; // ~6 per channel
    private const int MinUpdateIntervalMs = 25;
    private static long lastLedUpdate = 0;

    private static bool SignificantChange(Color a, Color b)
    {
        int d = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        return d >= MinTotalDelta;
    }

    private static void ambilightThreadLoop(object? obj)
    {
        while (ambilightThreadRunning)
        {
            try
            {
                if (!Monitor.TryEnter(d3dLock))
                {
                    Thread.Sleep(ambilightThreadDelay);
                    continue;
                }

                try
                {
                    if (device is null || surface is null)
                        continue;

                    // Capture the screen
                    device.GetFrontBufferData(0, surface);

                    // Lock the surface to access the pixel data (read-only, avoid global syslock)
                    dataRectangle = surface.LockRectangle(LockFlags.ReadOnly | LockFlags.NoSystemLock);

                    // Get the data pointer
                    dataPointer = dataRectangle.DataPointer;

                    // Apply vertical black bar detection if enabled
                    int VerticalBlackBarWidth = VerticalBlackBarDetectionEnabled ? DynamicLightingManager.VerticalBlackBarWidth() : 0;
                    int yBandTop = (screenHeight / 2) - (squareSize / 2);

                    Color currentColorLeft = CalculateColorAverage(0 + VerticalBlackBarWidth, yBandTop);
                    Color currentColorRight = CalculateColorAverage(screenWidth - squareSize - VerticalBlackBarWidth, yBandTop);

                    // Unlock the surface
                    surface.UnlockRectangle();

                    leftLedTracker.AddColor(currentColorLeft);
                    rightLedTracker.AddColor(currentColorRight);

                    // Calculate the average colors based on previous colors for the left and right LEDs
                    Color averageColorLeft = leftLedTracker.CalculateAverageColor();
                    Color averageColorRight = rightLedTracker.CalculateAverageColor();

                    // Skip tiny color changes + rate‑limit HID writes
                    long now = Environment.TickCount64;
                    bool changed = SignificantChange(averageColorLeft, previousColorLeft) || SignificantChange(averageColorRight, previousColorRight);

                    if (changed && (now - lastLedUpdate >= MinUpdateIntervalMs))
                    {
                        IDevice.GetCurrent().SetLedColor(averageColorLeft, averageColorRight, LEDLevel.Ambilight);
                        previousColorLeft = averageColorLeft;
                        previousColorRight = averageColorRight;
                        lastLedUpdate = now;
                    }
                }
                finally
                {
                    Monitor.Exit(d3dLock);
                }
            }
            catch { }

            Thread.Sleep(ambilightThreadDelay);
        }
    }

    private static void StartAmbilight()
    {
        if (ambilightThreadRunning)
            return;

        // Reset color histories for next time
        previousColorLeft = new Color();
        previousColorRight = new Color();
        leftLedTracker.Reset();
        rightLedTracker.Reset();

        ambilightThreadRunning = true;

        ambilightThread = new Thread(ambilightThreadLoop)
        {
            IsBackground = true
        };
        ambilightThread.Start();
    }

    private static void StopAmbilight()
    {
        // suspend watchdog
        if (ambilightThread is not null)
        {
            ambilightThreadRunning = false;
            // Ensure the thread has finished execution
            if (ambilightThread.IsAlive)
                ambilightThread.Join(3000);
            ambilightThread = null;
        }
    }

    private static unsafe Color CalculateColorAverage(int x, int y)
    {
        // Clamp ROI
        int xEnd = Math.Min(x + squareSize, screenWidth);
        int yEnd = Math.Min(y + squareSize, screenHeight);

        byte* basePtr = (byte*)dataPointer.ToPointer();
        int pitch = dataRectangle.Pitch;        // bytes per row
        const int bpp = 4;                      // BGRA

        long sumR = 0, sumG = 0, sumB = 0;
        int samples = 0;

        for (int yy = y; yy < yEnd; yy += squareStep)
        {
            byte* row = basePtr + (yy * pitch) + (x * bpp);
            for (int xx = x; xx < xEnd; xx += squareStep)
            {
                // BGRA
                byte b = row[0];
                byte g = row[1];
                byte r = row[2];

                sumR += r;
                sumG += g;
                sumB += b;
                samples++;

                row += bpp * squareStep;
            }
        }

        if (samples == 0) return Colors.Black;

        return Color.FromRgb(
            (byte)(sumR / samples),
            (byte)(sumG / samples),
            (byte)(sumB / samples));
    }

    // Get the pixel color at a given position
    static Color GetPixelColor(int x, int y)
    {
        // Calculate the offset of the pixel in bytes
        int offset = (y * dataRectangle.Pitch) + (x * 4);

        // Read the pixel value as an integer
        int value = Marshal.ReadInt32(dataPointer, offset);

        // Extract the bytes from the int value
        byte a = (byte)((value >> 24) & 0xFF); // alpha
        byte r = (byte)((value >> 16) & 0xFF); // red
        byte g = (byte)((value >> 8) & 0xFF); // green
        byte b = (byte)(value & 0xFF); // blue

        // Convert the pixel value to a color object
        return Color.FromArgb(a, r, g, b);
    }

    static int VerticalBlackBarWidth()
    {
        // Find the width of vertical black bars on the left and right sides
        // Inspired by Hyperion Project BlackBorderDetector.h

        int width = screenWidth;
        int height = screenHeight;
        int width33percent = width / 3;
        int yCenter = height / 2;

        // Find first X pixel of the image that is not black
        for (int x = 0; x < width33percent; ++x)
        {
            // Test centre and 33%, 66% of width/height
            // Centre will check right
            // 33 and 66 will check left
            if (!ColorTracker.IsBlack(GetPixelColor((width - x), yCenter))
                || !ColorTracker.IsBlack(GetPixelColor(x, height / 3))
                || !ColorTracker.IsBlack(GetPixelColor(x, 2 * (height / 3))))
            {
                return x;
            }
        }

        return 0; // No black bars detected
    }

    #region events

    public static event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler();

    #endregion
}