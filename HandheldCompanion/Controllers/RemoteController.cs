using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace HandheldCompanion.Controllers;

internal sealed class RemoteController : IController
{
    private readonly Guid id;
    private readonly string instanceId;
    private readonly object stateLock = new();
    private string name;
    private ControllerState latestState = new();
    private uint sequence;
    private long lastUpdateTicks;
    private long lastAdvertisementTicks;
    private bool hasReceivedState;
    private volatile bool manuallyDisconnected;
    private readonly Dictionary<ButtonFlags, string> buttonGlyphs = [];
    private readonly Dictionary<AxisFlags, string> axisGlyphs = [];
    private readonly Dictionary<AxisLayoutFlags, string> layoutGlyphs = [];
    private readonly Dictionary<ButtonFlags, string> fontFamilies = [];

    public RemoteController(Guid id, string name, byte userIndex)
    {
        this.id = id;
        this.name = name;
        UserIndex = userIndex;
        instanceId = $"HC-NET-{id:N}";
        lastUpdateTicks = Environment.TickCount64;
        lastAdvertisementTicks = lastUpdateTicks;
    }

    public void Update(string controllerName, byte userIndex, uint packetSequence, ControllerState state)
    {
        lock (stateLock)
        {
            if (packetSequence <= sequence && sequence != 0)
                return;

            sequence = packetSequence;
            name = controllerName;
            UserIndex = userIndex;
            latestState = state;
            lastUpdateTicks = Environment.TickCount64;
            hasReceivedState = true;
        }
    }

    private const long TimeoutMilliseconds = 2000;
    private const long AdvertisementTimeoutMilliseconds = 7000;

    public bool IsTimedOut
    {
        get
        {
            long lastUpdate = manuallyDisconnected
                ? Interlocked.Read(ref lastAdvertisementTicks)
                : Interlocked.Read(ref lastUpdateTicks);
            long timeout = manuallyDisconnected || !hasReceivedState
                ? AdvertisementTimeoutMilliseconds
                : TimeoutMilliseconds;
            return Environment.TickCount64 - lastUpdate > timeout;
        }
    }

    public void RefreshAdvertisement(string controllerName, byte userIndex)
    {
        lock (stateLock)
        {
            name = controllerName;
            UserIndex = userIndex;
            Interlocked.Exchange(ref lastUpdateTicks, Environment.TickCount64);
            Interlocked.Exchange(ref lastAdvertisementTicks, lastUpdateTicks);
        }
    }

    public override bool IsVirtual() => false;
    public override bool IsPhysical() => true;
    public override bool IsNetwork() => true;
    public override bool IsInternal() => false;
    public override bool IsConnected() => !IsTimedOut;
    public override string GetInstanceId() => instanceId;
    public Guid NetworkId => id;
    public override string GetContainerInstanceId() => instanceId;
    public override string GetPath() => instanceId;
    public override string GetContainerPath() => instanceId;
    public override bool IsHidden() => true;
    public override void Hide(bool powerCycle = true) { }
    public override void Unhide(bool powerCycle = true) { }
    public override string ToString() => name;

    public override void Plug()
    {
        manuallyDisconnected = false;
        Interlocked.Exchange(ref lastUpdateTicks, Environment.TickCount64);
        base.Plug();
    }

    public override void Unplug()
    {
        manuallyDisconnected = true;
    }

    public void UnplugFromRemoteSession()
    {
        manuallyDisconnected = false;
        base.Unplug();
    }

    public void ApplyMetadata(NetworkControllerMetadata metadata)
    {
        Capabilities = metadata.Capabilities | ControllerCapabilities.MotionSensor;
        SetSourceMetadata(metadata.SourceButtons, metadata.SourceAxis);
        buttonGlyphs.Clear();
        foreach (KeyValuePair<ButtonFlags, string> glyph in metadata.ButtonGlyphs)
            buttonGlyphs[glyph.Key] = glyph.Value;
        axisGlyphs.Clear();
        foreach (KeyValuePair<AxisFlags, string> glyph in metadata.AxisGlyphs)
            axisGlyphs[glyph.Key] = glyph.Value;
        layoutGlyphs.Clear();
        foreach (KeyValuePair<AxisLayoutFlags, string> glyph in metadata.LayoutGlyphs)
            layoutGlyphs[glyph.Key] = glyph.Value;
        fontFamilies.Clear();
        foreach (KeyValuePair<ButtonFlags, string> family in metadata.FontFamilies)
            fontFamilies[family.Key] = family.Value;
    }

    public override string GetGlyph(ButtonFlags button) => buttonGlyphs.TryGetValue(button, out string? glyph) ? glyph : base.GetGlyph(button);
    public override string GetGlyph(AxisFlags axis) => axisGlyphs.TryGetValue(axis, out string? glyph) ? glyph : base.GetGlyph(axis);
    public override string GetGlyph(AxisLayoutFlags axis) => layoutGlyphs.TryGetValue(axis, out string? glyph) ? glyph : base.GetGlyph(axis);
    public override string GetFontFamily(ButtonFlags button) => fontFamilies.TryGetValue(button, out string? family) ? family : base.GetFontFamily(button);

    public override void Tick(long ticks, float delta, bool commit = false)
    {
        if (IsBusy || _disposing || _disposed)
            return;

        lock (stateLock)
        {
            if (IsTimedOut)
            {
                ClearInputState();
                return;
            }

            Inputs.ButtonState.Clear();
            Inputs.AxisState.Clear();
            Inputs.GyroState.CopyFrom(latestState.GyroState);

            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion? gamepadMotion))
            {
                Vector3 gyro = latestState.GyroState.GetGyroscope(GyroState.SensorState.DSU);
                Vector3 accel = latestState.GyroState.GetAccelerometer(GyroState.SensorState.DSU);
                gamepadMotion.ProcessMotion(gyro.X, gyro.Y, gyro.Z, accel.X, accel.Y, accel.Z, delta);
            }

            Inputs.ButtonState.AddRange(latestState.ButtonState);
            Inputs.AxisState.AddRange(latestState.AxisState);
        }

        base.Tick(ticks, delta, commit);
    }

    public override void SetVibration(byte largeMotor, byte smallMotor)
    {
        NetworkControllerHelper.SendVibration(id, largeMotor, smallMotor);
    }
}
