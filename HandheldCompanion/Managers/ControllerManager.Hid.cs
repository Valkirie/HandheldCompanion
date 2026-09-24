using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.AYANEO;
using HandheldCompanion.Controllers.Lenovo;
using HandheldCompanion.Controllers.MSI;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers;

public static partial class ControllerManager
{
    private static void HidDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;
        LogManager.LogTrace("ControllerManager HID arrival received: key={0}, device={1}, gaming={2}",
            key, details.deviceInstanceId, details.isGaming);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hidArrivalInProgress[key] = completion.Task;

        _ = Task.Run(async () =>
        {
            // If a removal is running for this device, wait it out (with timeout to avoid deadlock)
            if (hidRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                if (!details.isGaming)
                {
                    LogManager.LogTrace("ControllerManager HID arrival ignored: key={0}, reason=not-gaming", key);
                    return;
                }

                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller is not null)
                    {
                        if (controller is XInputController or LegionControllerXInput)
                        {
                            LogManager.LogTrace("ControllerManager HID arrival ignored: key={0}, reason=xinput-owned, type={1}", key, controller.GetType().Name);
                            return;
                        }
                        if (controller is SDLController)
                        {
                            LogManager.LogTrace("ControllerManager HID arrival ignored: key={0}, reason=sdl-owned, type={1}", key, controller.GetType().Name);
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
                        int VendorId = details.VendorID;
                        int ProductId = details.ProductID;

                        switch (VendorId)
                        {
                            // Valve
                            case 0x28DE:
                                switch (ProductId)
                                {
                                    case 0x1102:
                                        if (details.GetMI() == 2) // Steam Controller has a 3-interface composite HID device, with the Valve feature-report controller surface on interface 2
                                            try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1142:
                                        try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1205: // Steam Deck Controller (Neptune)
                                    case 0x12f0: // SteamOS Handheld Controller
                                        try { controller = new NeptuneController(details); } catch { }
                                        break;
                                    case 0x1302: // Steam Controller 2026 (Wired)
                                    case 0x1304: // Steam Controller 2026 (Wireless)
                                        break;
                                }
                                break;

                            case 0x057E:
                                switch (ProductId)
                                {
                                    case 0x2009:
                                        break;
                                }
                                break;

                            // Lenovo
                            case 0x17EF:
                                switch (ProductId)
                                {
                                    case 0x6184: // dual_dinput
                                    case 0x61ED: // dual_dinput (2025 FW)
                                        if (details.GetMI() == 2)
                                        {
                                            details.isDongle = true;
                                            try { controller = new LegionControllerDInput(details); } catch { }
                                        }
                                        break;
                                    case 0x6183: // dinput
                                    case 0x61EC: // dinput (2025 FW)
                                        try { controller = new LegionControllerDInput(details); } catch { }
                                        break;
                                    case 0xE311:
                                        break;
                                }
                                break;

                            // MSI
                            case 0x0DB0:
                                switch (ProductId)
                                {
                                    case 0x1902:
                                    case 0x1903:
                                        try { controller = new DClawController(details); } catch { }
                                        break;
                                }
                                break;

                            // AYANEO
                            case 0x4001:
                                switch (ProductId)
                                {
                                    case 0x0428: // AYANEO Next II DInput
                                        try { controller = new AYANEODController(details); } catch { }
                                        break;
                                }
                                break;
                        }
                    }

                    if (controller == null)
                    {
                        LogManager.LogWarning("Unsupported Generic controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                        return;
                    }

                    // controller is gone ?
                    if (await IsControllerGoneAsync(controller))
                    {
                        LogManager.LogWarning("Generic controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = controller.GetContainerInstanceId();
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogTrace("ControllerManager HID registry upsert: key={0}, instance={1}, type={2}, cycling={3}, count={4}",
                        baseContainerDeviceInstanceId, controller.GetInstanceId(), controller.GetType().Name, wasPowerCycling, Controllers.Count);
                    LogManager.LogInformation("Generic controller {0} plugged", controller.ToString());
                    LogManager.LogTrace("ControllerManager HID plugged emitting: key={0}, instance={1}, cycling={2}",
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
                    LogManager.LogError("ControllerManager HID arrival failed: key={0}, error={1}", key, ex);
                }
                finally { }
            }
            finally
            {
                ((ICollection<KeyValuePair<string, Task>>)hidArrivalInProgress).Remove(new(key, completion.Task));
                completion.TrySetResult();
            }
        });
    }

    private static void HidDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;
        LogManager.LogTrace("ControllerManager HID removal received: key={0}, device={1}", key, details.deviceInstanceId);

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!hidRemovalInProgress.TryAdd(key, completion.Task))
        {
            LogManager.LogTrace("ControllerManager HID removal suppressed: key={0}, reason=removal-in-progress", key);
            return;
        }

        _ = Task.Run(async () =>
        {
            // If add is still running for this HID device, wait before removing (with timeout to avoid deadlock)
            if (hidArrivalInProgress.TryGetValue(key, out var pendingAdd))
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
                        LogManager.LogWarning("ControllerManager HID removal unresolved: key={0}, registryCount={1}", key, Controllers.Count);
                        return;
                    }
                    if (controller is XInputController or LegionControllerXInput)
                    {
                        LogManager.LogTrace("ControllerManager HID removal ignored: key={0}, reason=xinput-owned, type={1}", key, controller.GetType().Name);
                        return;
                    }
                    if (controller is SDLController)
                    {
                        LogManager.LogTrace("ControllerManager HID removal ignored: key={0}, reason=sdl-owned, type={1}", key, controller.GetType().Name);
                        return;
                    }

                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
                    bool WasTarget = IsTargetController(controller.GetInstanceId());

                    LogManager.LogInformation("Generic controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                    LogManager.LogTrace("ControllerManager HID unplugged emitting: key={0}, instance={1}, cycling={2}, target={3}",
                        key, controller.GetInstanceId(), IsPowerCycling, WasTarget);
                    ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                    if (!IsPowerCycling)
                    {
                        bool removed = Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);
                        LogManager.LogTrace("ControllerManager HID registry removal: key={0}, removed={1}, remaining={2}",
                            key, removed, Controllers.Count);

                        bool isPhysical = controller.IsPhysical();

                        controller.Gone();

                        if (isPhysical && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
                            PickTargetController();
                        else
                            controller.Dispose();
                    }
                    else
                    {
                        LogManager.LogTrace("ControllerManager HID registry retained: key={0}, reason=power-cycling", key);
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("ControllerManager HID removal failed: key={0}, error={1}", key, ex);
                }
                finally { }
            }
            finally
            {
                ((ICollection<KeyValuePair<string, Task>>)hidRemovalInProgress).Remove(new(key, completion.Task));
                completion.TrySetResult();
            }
        });
    }
}
