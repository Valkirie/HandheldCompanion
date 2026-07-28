using HandheldCompanion.Devices;
using HandheldCompanion.Models;
using HandheldCompanion.Shared;
using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Sensors;

internal enum WindowsSensorKind
{
    Accelerometer,
    Gyrometer
}

internal sealed class WindowsSensorHandle : IDisposable
{
    private IntPtr sensorPointer;
    private bool disposed;

    internal WindowsSensorHandle(IntPtr sensorPointer, WindowsSensorKind kind, string friendlyName, Guid sensorType, WindowsSensorManager.PROPERTYKEY[] fields)
    {
        this.sensorPointer = sensorPointer;
        Kind = kind;
        FriendlyName = friendlyName;
        SensorType = sensorType;
        this.fields = fields;
    }

    internal WindowsSensorKind Kind { get; }
    internal string FriendlyName { get; }
    internal Guid SensorType { get; }
    private WindowsSensorManager.PROPERTYKEY[] fields { get; }

    internal (double X, double Y, double Z) Read()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return WindowsSensorManager.Read(sensorPointer, fields);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (sensorPointer != IntPtr.Zero)
        {
            Marshal.Release(sensorPointer);
            sensorPointer = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~WindowsSensorHandle()
    {
        Dispose();
    }
}

internal static class WindowsSensorManager
{
    private static readonly Guid SensorCategoryAll = new("C317C286-C468-4288-9975-D4C4587C442C");
    private static readonly Guid AccelerometerType = new("C2FB0F5F-E2D2-4C78-BCD0-352A9582819D");
    private static readonly Guid GyrometerType = new("09485F5A-759E-42C2-BD4B-A349B75C8643");
    private static readonly Guid MotionFormat = new("3F8A69A2-07C5-4E48-A965-CD797AAB56D5");
    private static readonly PROPERTYKEY AccelerationX = new(MotionFormat, 2);
    private static readonly PROPERTYKEY AccelerationY = new(MotionFormat, 3);
    private static readonly PROPERTYKEY AccelerationZ = new(MotionFormat, 4);
    private static readonly PROPERTYKEY AngularVelocityX = new(MotionFormat, 10);
    private static readonly PROPERTYKEY AngularVelocityY = new(MotionFormat, 11);
    private static readonly PROPERTYKEY AngularVelocityZ = new(MotionFormat, 12);

    [ComImport]
    [Guid("BD77DB67-45A8-42DC-8D00-6DCF15F8377A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISensorManager
    {
        [PreserveSig] int GetSensorsByCategory([In] ref Guid sensorCategory, out IntPtr sensors);
        [PreserveSig] int GetSensorsByType([In] ref Guid sensorType, out IntPtr sensors);
        [PreserveSig] int GetSensorByID([In] ref Guid sensorId, out IntPtr sensor);
        [PreserveSig] int SetEventSink(IntPtr events);
        [PreserveSig] int RequestPermissions(IntPtr hWndParent, IntPtr sensorCollection, [MarshalAs(UnmanagedType.Bool)] bool modal);
    }

    [ComImport]
    [Guid("77A1C827-FCD2-4689-8915-9D613CC5FA3E")]
    private class SensorManagerCoClass
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct PROPERTYKEY(Guid formatId, uint propertyId)
    {
        internal readonly Guid FormatId = formatId;
        internal readonly uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] internal ushort VariantType;
        [FieldOffset(8)] internal int Int32;
        [FieldOffset(8)] internal uint UInt32;
        [FieldOffset(8)] internal float Single;
        [FieldOffset(8)] internal double Double;

        internal double? AsDouble() => VariantType switch
        {
            3 => Int32,
            19 => UInt32,
            4 => Single,
            5 => Double,
            _ => null
        };
    }

    private delegate int GetCountDelegate(IntPtr self, out uint count);
    private delegate int GetAtDelegate(IntPtr self, uint index, out IntPtr sensor);
    private delegate int GetSensorTypeDelegate(IntPtr self, out Guid sensorType);
    private delegate int GetFriendlyNameDelegate(IntPtr self, [MarshalAs(UnmanagedType.BStr)] out string name);
    private delegate int GetSupportedDataFieldsDelegate(IntPtr self, out IntPtr fields);
    private delegate int GetDataDelegate(IntPtr self, out IntPtr report);
    private delegate int SupportsDataFieldDelegate(IntPtr self, [In] ref PROPERTYKEY key, out short supported);
    private delegate int GetKeyCountDelegate(IntPtr self, out uint count);
    private delegate int GetKeyAtDelegate(IntPtr self, uint index, ref PROPERTYKEY key);
    private delegate int GetSensorValueDelegate(IntPtr self, [In] ref PROPERTYKEY key, out PROPVARIANT value);

    internal static WindowsSensorHandle? Find(WindowsSensorKind kind)
    {
        object? managerObject = null;
        IntPtr collection = IntPtr.Zero;

        try
        {
            managerObject = new SensorManagerCoClass();
            ISensorManager manager = (ISensorManager)managerObject;
            Guid requestedType = kind == WindowsSensorKind.Accelerometer ? AccelerometerType : GyrometerType;
            int typeResult = manager.GetSensorsByType(ref requestedType, out collection);
            if (typeResult < 0 || collection == IntPtr.Zero)
            {
                Guid category = SensorCategoryAll;
                if (manager.GetSensorsByCategory(ref category, out collection) < 0)
                    return null;
            }
            if (collection == IntPtr.Zero)
                return null;

            uint count = Invoke<GetCountDelegate>(collection, 4)(collection, out uint sensorCount) == 0
                ? sensorCount
                : throw new COMException("Unable to enumerate Windows sensors.");

            for (uint index = 0; index < count; index++)
            {
                IntPtr sensor = IntPtr.Zero;
                try
                {
                    ThrowIfFailed(Invoke<GetAtDelegate>(collection, 3)(collection, index, out sensor));
                    string name = GetFriendlyName(sensor);
                    WindowsSensorKind? discoveredKind = GetKind(name);
                    if (discoveredKind != kind)
                    {
                        Marshal.Release(sensor);
                        sensor = IntPtr.Zero;
                        continue;
                    }

                    Guid sensorType = GetSensorType(sensor);
                    if (!CanRead(sensor, kind, name, out PROPERTYKEY[]? fields))
                    {
                        Marshal.Release(sensor);
                        sensor = IntPtr.Zero;
                        continue;
                    }

                    LogManager.LogInformation("Found legacy Windows {0}: {1} ({2})", kind, name, sensorType);
                    return new WindowsSensorHandle(sensor, kind, name, sensorType, fields);
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException)
                {
                    if (sensor != IntPtr.Zero)
                        Marshal.Release(sensor);
                    LogManager.LogTrace("Unable to inspect legacy Windows sensor {0}: {1}", index, ex.Message);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException)
        {
            LogManager.LogTrace("Legacy Windows Sensor Manager is unavailable: {0}", ex.Message);
        }
        finally
        {
            if (collection != IntPtr.Zero)
                Marshal.Release(collection);
            if (managerObject is not null && Marshal.IsComObject(managerObject))
                Marshal.ReleaseComObject(managerObject);
        }

        return null;
    }

    internal static (double X, double Y, double Z) Read(IntPtr sensor, PROPERTYKEY[] fields)
    {
        ThrowIfFailed(Invoke<GetDataDelegate>(sensor, 13)(sensor, out IntPtr report));
        try
        {
            return (ReadValue(report, fields[0]), ReadValue(report, fields[1]), ReadValue(report, fields[2]));
        }
        finally
        {
            if (report != IntPtr.Zero)
                Marshal.Release(report);
        }
    }

    private static bool CanRead(IntPtr sensor, WindowsSensorKind kind, string friendlyName, out PROPERTYKEY[]? fields)
    {
        fields = null;
        try
        {
            PROPERTYKEY[]? configuredFields = GetConfiguredFields(kind);
            if (configuredFields is not null && SupportsDataFields(sensor, configuredFields))
            {
                fields = configuredFields;
                return true;
            }

            PROPERTYKEY[] keys = kind == WindowsSensorKind.Accelerometer
                ? [AccelerationX, AccelerationY, AccelerationZ]
                : [AngularVelocityX, AngularVelocityY, AngularVelocityZ];

            if (SupportsDataFields(sensor, keys))
            {
                fields = keys;
                return true;
            }

            // If the sensor doesn't support the expected fields, log a warning and return false
            LogSupportedDataFields(sensor);
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static PROPERTYKEY[]? GetConfiguredFields(WindowsSensorKind kind)
    {
        SensorFieldMappingData? mapping = kind == WindowsSensorKind.Accelerometer
            ? IDevice.GetCurrent().WindowsAccelerometerFields
            : IDevice.GetCurrent().WindowsGyrometerFields;

        if (mapping is null || mapping.PropertyIds.Length != 3 || !Guid.TryParse(mapping.FormatId, out Guid formatId))
            return null;

        return [
            new PROPERTYKEY(formatId, mapping.PropertyIds[0]),
            new PROPERTYKEY(formatId, mapping.PropertyIds[1]),
            new PROPERTYKEY(formatId, mapping.PropertyIds[2])
        ];
    }

    private static bool SupportsDataFields(IntPtr sensor, PROPERTYKEY[] keys)
    {
        foreach (PROPERTYKEY key in keys)
        {
            if (!SupportsDataField(sensor, key))
                return false;
        }

        return true;
    }

    private static bool SupportsDataField(IntPtr sensor, PROPERTYKEY key)
    {
        ThrowIfFailed(Invoke<SupportsDataFieldDelegate>(sensor, 11)(sensor, ref key, out short supported));
        return supported != 0;
    }

    private static void LogSupportedDataFields(IntPtr sensor)
    {
        int result = Invoke<GetSupportedDataFieldsDelegate>(sensor, 9)(sensor, out IntPtr fields);
        if (result >= 0 && fields != IntPtr.Zero)
        {
            try
            {
                ThrowIfFailed(Invoke<GetKeyCountDelegate>(fields, 3)(fields, out uint count));
                for (uint index = 0; index < count; index++)
                {
                    PROPERTYKEY key = default;
                    ThrowIfFailed(Invoke<GetKeyAtDelegate>(fields, 4)(fields, index, ref key));
                    LogManager.LogDebug("Legacy sensor data field {0}: {1} ({2})", index, key.FormatId, key.PropertyId);
                }
            }
            finally
            {
                Marshal.Release(fields);
            }
        }
    }

    private static double ReadValue(IntPtr report, PROPERTYKEY key)
    {
        ThrowIfFailed(Invoke<GetSensorValueDelegate>(report, 4)(report, ref key, out PROPVARIANT value));
        return value.AsDouble() ?? throw new InvalidOperationException("Windows sensor value is not numeric.");
    }

    private static string GetFriendlyName(IntPtr sensor)
    {
        ThrowIfFailed(Invoke<GetFriendlyNameDelegate>(sensor, 6)(sensor, out string name));
        return name;
    }

    private static Guid GetSensorType(IntPtr sensor)
    {
        ThrowIfFailed(Invoke<GetSensorTypeDelegate>(sensor, 5)(sensor, out Guid sensorType));
        return sensorType;
    }

    private static WindowsSensorKind? GetKind(string name)
    {
        SensorFieldMappingData? accelerometerMapping = IDevice.GetCurrent().WindowsAccelerometerFields;
        if (string.Equals(name, accelerometerMapping?.FriendlyName, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("accelerometer", StringComparison.OrdinalIgnoreCase))
            return WindowsSensorKind.Accelerometer;

        SensorFieldMappingData? gyrometerMapping = IDevice.GetCurrent().WindowsGyrometerFields;
        if (string.Equals(name, gyrometerMapping?.FriendlyName, StringComparison.OrdinalIgnoreCase) ||
            name.Contains("gyrometer", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("gyroscope", StringComparison.OrdinalIgnoreCase))
            return WindowsSensorKind.Gyrometer;

        return null;
    }

    private static T Invoke<T>(IntPtr instance, int slot) where T : Delegate
    {
        IntPtr vtable = Marshal.ReadIntPtr(instance);
        IntPtr method = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult);
    }
}
