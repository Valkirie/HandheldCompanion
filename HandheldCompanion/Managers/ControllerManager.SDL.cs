using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Lenovo;
using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Shared;
using SDL3;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers;

public static partial class ControllerManager
{
    private static void SDL_GamepadAdded(uint deviceIndex)
    {
        var addTask = Task.Run(async () =>
        {
            // If a removal is running for this SDL slot, wait it out first (with timeout to avoid deadlock)
            if (sdlRemovalInProgress.TryGetValue(deviceIndex, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { /* swallow */ }

            try
            {
                if (!SDL.IsGamepad(deviceIndex))
                {
                    LogManager.LogError("Controller at index: {0} is not a recognized game controller", deviceIndex);
                    return;
                }

                nint gamepad = SDL.OpenGamepad(deviceIndex);
                if (gamepad == IntPtr.Zero)
                {
                    LogManager.LogError("Failed to open controller {0}: {1}", deviceIndex, SDL.GetError());
                }
                else
                {
                    string? name = SDL.GetGamepadName(gamepad);
                    string? path = SDL.GetGamepadPath(gamepad);
                    uint userIndex = (uint)SDL.GetGamepadPlayerIndex(gamepad);

                    if (string.IsNullOrEmpty(path))
                        return;

                    if (path.Contains("XInput"))
                        path = DeviceManager.GetPathFromUserIndex(userIndex);

                    if (DeviceManager.TryExtractInterfaceGuid(path, out Guid interfaceGuid))
                        path = DeviceManager.SymLinkToInstanceId(path, interfaceGuid.ToString());

                    PnPDetails? details = await DeviceManager.GetDeviceFromInstanceIdAsync(path).ConfigureAwait(false);
                    if (details is null)
                    {
                        LogManager.LogError("Failed to retrieve PnPDetails for controller {0}", deviceIndex);
                        return;
                    }

                    try
                    {
                        Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                        if (controller != null)
                        {
                            if (controller is XInputController or LegionControllerXInput) return;
                            if (controller is DInputController) return;

                            if (controller is SDLController SDLController)
                            {
                                SDLController.gamepad = gamepad;
                                SDLController.deviceIndex = deviceIndex;
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
                            SDL.GamepadType type = SDL.GetGamepadType(gamepad);
                            switch (type)
                            {
                                default:
                                case SDL.GamepadType.Unknown:
                                case SDL.GamepadType.Standard:
                                    {
                                        int VendorId = details.VendorID;
                                        int ProductId = details.ProductID;

                                        switch (VendorId)
                                        {
                                            case 0x28DE:
                                                switch (ProductId)
                                                {
                                                    case 0x1102:
                                                    case 0x1142:
                                                    case 0x1205: // Steam Deck Controller (Neptune)
                                                    case 0x12f0: // SteamOS Handheld Controller
                                                        break;

                                                    case 0x1302: // Steam Controller 2026 (Wired)
                                                    case 0x1304: // Steam Controller 2026 (Wireless)
                                                        controller = new SteamController2026(gamepad, deviceIndex, details);
                                                        break;
                                                }
                                                break;

                                            default:
                                                controller = new Xbox360Controller(gamepad, deviceIndex, details);
                                                break;
                                        }
                                    }
                                    break;

                                case SDL.GamepadType.Xbox360:
                                case SDL.GamepadType.XboxOne:
                                    // XInput controllers are handled exclusively by the XInput pipeline (XUsbDeviceArrived).
                                    // SDL detection is expected; skip silently and let XInput manage it.
                                    return;

                                case SDL.GamepadType.PS3:
                                case SDL.GamepadType.PS4:
                                    controller = new DualShock4Controller(gamepad, deviceIndex, details);
                                    break;
                                case SDL.GamepadType.PS5:
                                    controller = new DualSenseController(gamepad, deviceIndex, details);
                                    break;

                                case SDL.GamepadType.GameCube:
                                case SDL.GamepadType.NintendoSwitchPro:
                                    controller = new NintendoSwitchProController(gamepad, deviceIndex, details);
                                    break;
                            }
                        }

                        if (controller == null)
                        {
                            LogManager.LogWarning("Unsupported SDL controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                            return;
                        }

                        // controller is gone ?
                        if (await IsControllerGoneAsync(controller))
                        {
                            LogManager.LogWarning("SDL controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                            controller.Gone();
                            return;
                        }

                        string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                        bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                        controller.IsBusy = false;

                        if (controller is not SDLController sdlController)
                            return;

                        Controllers[baseContainerDeviceInstanceId] = controller;
                        SDLControllers[deviceIndex] = sdlController;

                        LogManager.LogInformation("SDL controller {0} plugged", controller.ToString());
                        ControllerPlugged?.Invoke(controller, wasPowerCycling);

                        bool isPhysical = controller.IsPhysical();
                        if (isPhysical)
                        {
                            if (!wasPowerCycling && PlugBehavior == ControllerPlugBehavior.AlwaysAsk)
                                ShowDetectedToast(controller, wasPowerCycling);

                            PickTargetController();
                        }
                    }
                    finally { }
                }
            }
            finally
            {
                sdlArrivalInProgress.TryRemove(deviceIndex, out _);
            }
        });

        sdlArrivalInProgress[deviceIndex] = addTask;
    }

    private static void SDL_GamepadRemoved(uint deviceIndex)
    {
        var removeTask = Task.Run(async () =>
        {
            // If add is still running, wait before removing (with timeout to avoid deadlock)
            if (sdlArrivalInProgress.TryGetValue(deviceIndex, out var pendingAdd))
                try { await pendingAdd.WaitAsync(CrossWaitTimeout); } catch { }

            try
            {
                if (SDLControllers.TryGetValue(deviceIndex, out SDLController? controller))
                {
                    string path = controller.GetContainerInstanceId();

                    try
                    {
                        SDL.CloseGamepad(controller.gamepad);
                        controller.gamepad = IntPtr.Zero;

                        PowerCyclers.TryGetValue(path, out bool IsPowerCycling);
                        bool WasTarget = IsTargetController(controller.GetInstanceId());

                        LogManager.LogInformation("SDL controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                        ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                        if (!IsPowerCycling)
                        {
                            Controllers.TryRemove(path, out _);
                            SDLControllers.TryRemove(deviceIndex, out _);

                            bool isPhysical = controller.IsPhysical();

                            controller.Gone();

                            if (isPhysical && HIDuncloakondisconnect)
                                controller.Unhide(false);

                            if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
                                PickTargetController();
                            else
                                controller.Dispose();
                        }
                    }
                    finally { }
                }
            }
            finally
            {
                sdlRemovalInProgress.TryRemove(deviceIndex, out _);
            }
        });

        sdlRemovalInProgress[deviceIndex] = removeTask;
    }
}
