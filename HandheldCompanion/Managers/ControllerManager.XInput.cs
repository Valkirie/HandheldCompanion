using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.GameSir;
using HandheldCompanion.Controllers.Lenovo;
using HandheldCompanion.Controllers.MSI;
using HandheldCompanion.Controllers.Zotac;
using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers;

public static partial class ControllerManager
{
    private static void XUsbDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;
        LogManager.LogTrace("ControllerManager XUSB arrival received: key={0}, device={1}, virtual={2}, slot={3}",
            key, details.deviceInstanceId, details.isVirtual, details.XInputUserIndex);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        xusbArrivalInProgress[key] = completion.Task;

        _ = Task.Run(async () =>
        {
            // If a removal is running for this controller, wait first (with timeout to avoid deadlock)
            if (xusbRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller != null)
                    {
                        if (controller is DInputController)
                        {
                            LogManager.LogTrace("ControllerManager XUSB arrival ignored: key={0}, reason=dinput-owned, type={1}", key, controller.GetType().Name);
                            return;
                        }
                        if (controller is SDLController)
                        {
                            LogManager.LogTrace("ControllerManager XUSB arrival ignored: key={0}, reason=sdl-owned, type={1}", key, controller.GetType().Name);
                            return;
                        }
                        if (controller is not IXInputController)
                        {
                            LogManager.LogTrace("ControllerManager XUSB arrival ignored: key={0}, reason=not-xinput, type={1}", key, controller.GetType().Name);
                            return;
                        }

                        controller.AttachDetails(details);

                        if (controller.GetInstanceId() != details.deviceInstanceId)
                        {
                            if (controller.IsHidden())
                                controller.Hide(false);
                            else
                                controller.Unhide(false);
                        }

                    }
                    else
                    {
                        switch (details.GetVendorID())
                        {
                            // Asus
                            case "0x0B05":
                                {
                                    switch (details.GetProductID())
                                    {
                                        case "0x1ABE": // ASUS Xbox Adaptive Controller
                                        case "0x1B4C": // ASUS Xbox Adaptive Controller
                                            try { controller = new XboxAdaptiveController(details); } catch { }
                                            break;
                                    }
                                }
                                break;

                            // Lenovo
                            case "0x17EF":
                            case "0x1A86":
                                switch (details.GetProductID())
                                {
                                    case "0x6182":
                                    case "0x61EB":
                                        try { controller = new LegionControllerXInput(details); } catch { }
                                        break;

                                    case "0xE310":
                                        try { controller = new LegionControllerS(details); } catch { }
                                        break;

                                    default:
                                        try { controller = new XInputController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x3537":
                                switch (details.GetProductID())
                                {
                                    case "0x1099":
                                    case "0x103E":
                                        details.isDongle = true;
                                        goto case "0x1050";
                                    default:
                                    case "0x1050":
                                        try { controller = new TarantulaProController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x0DB0":
                                switch (details.GetProductID())
                                {
                                    case "0x1901":
                                        try { controller = new XClawController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x1EE9":
                                switch (details.GetProductID())
                                {
                                    case "0x1590":
                                        try { controller = new ZoneController(details); } catch { }
                                        break;
                                }
                                break;
                        }
                    }

                    if (controller is null)
                    {
                        try
                        {
                            controller = IDevice.GetCurrent().CreateController(details) ?? new XInputController(details);
                        }
                        catch
                        {
                            LogManager.LogWarning("Unsupported XInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                            return;
                        }
                    }

                    // controller is gone ?
                    if (await IsControllerGoneAsync(controller))
                    {
                        LogManager.LogWarning("XInput controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogTrace("ControllerManager XUSB registry upsert: key={0}, instance={1}, type={2}, cycling={3}, count={4}",
                        baseContainerDeviceInstanceId, controller.GetInstanceId(), controller.GetType().Name, wasPowerCycling, Controllers.Count);
                    LogManager.LogInformation("XInput controller {0} plugged", controller.ToString());
                    LogManager.LogTrace("ControllerManager XUSB plugged emitting: key={0}, instance={1}, cycling={2}",
                        baseContainerDeviceInstanceId, controller.GetInstanceId(), wasPowerCycling);
                    ControllerPlugged?.Invoke(controller, wasPowerCycling);

                    bool isPhysical = controller.IsPhysical();
                    if (isPhysical)
                    {
                        if (!wasPowerCycling)
                            ShowDetectedToast(controller, wasPowerCycling);

                        PickTargetController();
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("ControllerManager XUSB arrival failed: key={0}, error={1}", key, ex);
                }
                finally { }
            }
            finally
            {
                ((ICollection<KeyValuePair<string, Task>>)xusbArrivalInProgress).Remove(new(key, completion.Task));
                completion.TrySetResult();
            }
        });
    }

    private static void XUsbDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;
        LogManager.LogTrace("ControllerManager XUSB removal received: key={0}, device={1}, virtual={2}, slot={3}",
            key, details.deviceInstanceId, details.isVirtual, details.XInputUserIndex);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!xusbRemovalInProgress.TryAdd(key, completion.Task))
        {
            LogManager.LogTrace("ControllerManager XUSB removal suppressed: key={0}, reason=removal-in-progress", key);
            return;
        }

        _ = Task.Run(async () =>
        {
            // If add is still running for this controller, wait before removing (with timeout to avoid deadlock)
            if (xusbArrivalInProgress.TryGetValue(key, out var pendingAdd))
                try { await pendingAdd.WaitAsync(CrossWaitTimeout); } catch { }

            try
            {
                try
                {
                    IController? controller = null;

                    Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
                    while (!timeout.IsCompleted && controller == null)
                    {
                        if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                            break;

                        await Task.Delay(100);
                    }

                    if (controller == null)
                    {
                        LogManager.LogWarning("ControllerManager XUSB removal unresolved: key={0}, registryCount={1}", key, Controllers.Count);
                        return;
                    }
                    if (controller is DInputController)
                    {
                        LogManager.LogTrace("ControllerManager XUSB removal ignored: key={0}, reason=dinput-owned, type={1}", key, controller.GetType().Name);
                        return;
                    }
                    if (controller is SDLController)
                    {
                        LogManager.LogTrace("ControllerManager XUSB removal ignored: key={0}, reason=sdl-owned, type={1}", key, controller.GetType().Name);
                        return;
                    }

                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
                    bool WasTarget = IsTargetController(controller.GetInstanceId());

                    LogManager.LogInformation("XInput controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                    LogManager.LogTrace("ControllerManager XUSB unplugged emitting: key={0}, instance={1}, cycling={2}, target={3}",
                        key, controller.GetInstanceId(), IsPowerCycling, WasTarget);
                    ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                    if (!IsPowerCycling)
                    {
                        // Remove from the dictionary first so PickTargetController and any
                        // callbacks triggered by Gone()/Dispose() never see this controller.
                        bool removed = Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);
                        LogManager.LogTrace("ControllerManager XUSB registry removal: key={0}, removed={1}, remaining={2}",
                            key, removed, Controllers.Count);

                        bool isPhysical = controller.IsPhysical();

                        controller.Gone();

                        if (isPhysical && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        // Atomically check-and-clear under targetLock to avoid clearing a
                        // controller that SetTargetController just switched to on another thread.
                        if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
                            PickTargetController();
                        else
                            controller.Dispose();
                    }
                    else
                    {
                        LogManager.LogTrace("ControllerManager XUSB registry retained: key={0}, reason=power-cycling", key);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("ControllerManager XUSB removal failed: key={0}, error={1}", key, ex);
                }
                finally { }
            }
            finally
            {
                ((ICollection<KeyValuePair<string, Task>>)xusbRemovalInProgress).Remove(new(key, completion.Task));
                completion.TrySetResult();
            }
        });
    }
}
