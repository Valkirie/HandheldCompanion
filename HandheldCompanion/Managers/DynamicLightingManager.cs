using HandheldCompanion.Devices;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
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

    private static Device device;
    private static Surface surface;
    private static DataRectangle dataRectangle;
    private static IntPtr dataPointer;

    private static int screenWidth;
    private static int screenHeight;

    private static int squareSize = 100;
    private const int squareStep = 10;

    private static Thread ambilightThread;
    private static bool ambilightThreadRunning;
    private static int ambilightThreadDelay = defaultThreadDelay;
    private const int defaultThreadDelay = 33;

    private static bool VerticalBlackBarDetectionEnabled;

    static DynamicLightingManager()
    {
        // Keep track of left and right LEDs history
        leftLedTracker = new ColorTracker();
        rightLedTracker = new ColorTracker();

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        IDevice.GetCurrent().PowerStatusChanged += CurrentDevice_PowerStatusChanged;

        ambilightThread = new Thread(ambilightThreadLoop);
        ambilightThread.IsBackground = true;

        DynamicLightingTimer = new(125)
        {
            AutoReset = false
        };
        DynamicLightingTimer.Elapsed += (sender, e) => UpdateLED();
    }

    public static void Start()
    {
        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "DynamicLightingManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        StopAmbilight();

        ReleaseDirect3DDevice();

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "DynamicLightingManager");
    }

    private static void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        // Update the screen width and height values when display changes
        // Get the primary screen dimensions
        screenWidth = resolution.Width;
        screenHeight = resolution.Height;

        squareSize = (int)Math.Floor((decimal)screenWidth / 10);

        LEDLevel LEDSettingsLevel = (LEDLevel)SettingsManager.GetInt("LEDSettingsLevel");
        bool ambilightOn = LEDSettingsLevel == LEDLevel.Ambilight;

        // stop ambilight if running
        if (ambilightOn)
            StopAmbilight();

        // (re)create the Direct3D device
        InitializeDirect3DDevice();

        // restart ambilight if it was running
        if (ambilightOn)
            StartAmbilight();
    }

    private static async void InitializeDirect3DDevice()
    {
        try
        {
            // Create a device to access the screen
            device = new Device(new Direct3D(), 0, DeviceType.Hardware, IntPtr.Zero, CreateFlags.SoftwareVertexProcessing, new PresentParameters(screenWidth, screenHeight));

            // Create a surface to capture the screen
            surface = Surface.CreateOffscreenPlain(device, screenWidth, screenHeight, Format.A8R8G8B8, Pool.Scratch);
        }
        catch (SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.DeviceLost)
            {
                while (device is not null && device.TestCooperativeLevel() == ResultCode.DeviceLost)
                    await Task.Delay(100);

                // Recreate the device and resources
                ReleaseDirect3DDevice();
                InitializeDirect3DDevice();
            }
            else
            {
                // Handle other exceptions here
            }
        }
    }

    private static void ReleaseDirect3DDevice()
    {
        if (device is not null)
            device.Dispose();
        device = null;
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
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
                RequestUpdate();
                break;

            case "LEDAmbilightVerticalBlackBarDetection":
                VerticalBlackBarDetectionEnabled = Convert.ToBoolean(value);
                break;
        }
    }

    private static void CurrentDevice_PowerStatusChanged(Devices.IDevice device)
    {
        RequestUpdate();
    }

    private static void RequestUpdate()
    {
        DynamicLightingTimer.Stop();
        DynamicLightingTimer.Start();
    }

    private static void UpdateLED()
    {
        bool LEDSettingsEnabled = SettingsManager.GetBoolean("LEDSettingsEnabled");
        IDevice.GetCurrent().SetLedStatus(LEDSettingsEnabled);

        if (LEDSettingsEnabled)
        {
            LEDLevel LEDSettingsLevel = (LEDLevel)SettingsManager.GetInt("LEDSettingsLevel");
            int LEDBrightness = SettingsManager.GetInt("LEDBrightness");
            int LEDSpeed = SettingsManager.GetInt("LEDSpeed");

            // Set brightness and color based on settings
            IDevice.GetCurrent().SetLedBrightness(LEDBrightness);

            // Get colors
            Color LEDMainColor = SettingsManager.GetColor("LEDMainColor");
            Color LEDSecondColor = SettingsManager.GetColor("LEDSecondColor");
            bool useSecondColor = SettingsManager.GetBoolean("LEDUseSecondColor");

            switch (LEDSettingsLevel)
            {
                case LEDLevel.SolidColor:
                case LEDLevel.Breathing:
                case LEDLevel.Rainbow:
                    {
                        StopAmbilight();

                        IDevice.GetCurrent().SetLedColor(LEDMainColor, useSecondColor ? LEDSecondColor : LEDMainColor, LEDSettingsLevel, LEDSpeed);
                    }
                    break;

                case LEDLevel.Wave:
                case LEDLevel.Wheel:
                case LEDLevel.Gradient:
                    {
                        StopAmbilight();

                        IDevice.GetCurrent().SetLedColor(LEDMainColor, LEDSecondColor, LEDSettingsLevel, LEDSpeed);
                    }
                    break;

                case LEDLevel.Ambilight:
                    {
                        // Start adjusting LED colors based on screen content
                        if (!ambilightThreadRunning)
                        {
                            StartAmbilight();

                            // Provide LEDs with initial brightness
                            IDevice.GetCurrent().SetLedBrightness(100);
                            IDevice.GetCurrent().SetLedColor(Colors.Black, Colors.Black, LEDLevel.SolidColor);
                        }

                        ambilightThreadDelay = (int)((double)defaultThreadDelay / 100.0d * LEDSpeed);
                    }
                    break;
            }
        }
        else
        {
            StopAmbilight();

            // Set both brightness to 0 and color to black
            IDevice.GetCurrent().SetLedBrightness(0);
            IDevice.GetCurrent().SetLedColor(Colors.Black, Colors.Black, LEDLevel.SolidColor);
        }
    }

    private static void ambilightThreadLoop(object? obj)
    {
        while (ambilightThreadRunning)
        {
            try
            {
                // Capture the screen
                device.GetFrontBufferData(0, surface);

                // Lock the surface to access the pixel data
                dataRectangle = surface.LockRectangle(LockFlags.None);

                // Get the data pointer
                dataPointer = dataRectangle.DataPointer;

                // Apply vertical black bar detection if enabled
                int VerticalBlackBarWidth = VerticalBlackBarDetectionEnabled ? DynamicLightingManager.VerticalBlackBarWidth() : 0;

                Color currentColorLeft = CalculateColorAverage(1 + VerticalBlackBarWidth, 1);
                Color currentColorRight = CalculateColorAverage(screenWidth - squareSize - VerticalBlackBarWidth, ((screenHeight / 2) - (squareSize / 2)));

                // Unlock the surface
                surface.UnlockRectangle();

                leftLedTracker.AddColor(currentColorLeft);
                rightLedTracker.AddColor(currentColorRight);

                // Calculate the average colors based on previous colors for the left and right LEDs
                Color averageColorLeft = leftLedTracker.CalculateAverageColor();
                Color averageColorRight = rightLedTracker.CalculateAverageColor();

                // Only send HID update instruction if the color is different
                if (averageColorLeft != previousColorLeft || averageColorRight != previousColorRight)
                {
                    // Change LED colors of the device
                    IDevice.GetCurrent().SetLedColor(averageColorLeft, averageColorRight, LEDLevel.Ambilight);

                    // Update the previous colors for next time
                    previousColorLeft = averageColorLeft;
                    previousColorRight = averageColorRight;
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

        ambilightThread = new Thread(ambilightThreadLoop);
        ambilightThread.IsBackground = true;
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
                ambilightThread.Join();
            ambilightThread = null;
        }
    }

    private static Color CalculateColorAverage(int x, int y)
    {
        // Initialize the variables to store the sum of color values for the square
        int squareRedSum = 0;
        int squareGreenSum = 0;
        int squareBlueSum = 0;

        List<Color> colorList = new List<Color>();

        // Count the number of red green and blue occurences in the grid, use step size
        for (int xO = 0; xO < squareSize; xO += squareStep)
        {
            for (int yO = 0; yO < squareSize; yO += squareStep)
            {
                Color firstPixelColor = GetPixelColor(x + xO, y + yO);

                // todo: maybe we should ponderate black pixels weight
                // if (firstPixelColor != Colors.Black)
                colorList.Add(firstPixelColor);
            }
        }

        foreach (Color color in colorList)
        {
            squareRedSum += color.R;
            squareGreenSum += color.G;
            squareBlueSum += color.B;
        }

        // Calculate the average color value for the square by dividing by the number of pixels
        int squareRedAverage = squareRedSum / colorList.Count;
        int squareGreenAverage = squareGreenSum / colorList.Count;
        int squareBlueAverage = squareBlueSum / colorList.Count;

        // Convert the individual color values to a Color object
        return Color.FromRgb((byte)squareRedAverage, (byte)squareGreenAverage, (byte)squareBlueAverage);
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