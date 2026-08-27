using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Helpers;

public sealed class ControllerSlotHelper
{
    private const int MaxAttempts = 4;
    private const int MaxConsecutiveFailures = 3;
    private static readonly TimeSpan CrossWaitTimeout = TimeSpan.FromSeconds(5);

    private readonly Action<ControllerManager.ControllerManagerStatus, int> updateStatus;
    private readonly Action<bool, string> updateIssue;
    private readonly object watchdogLock = new();
    private readonly object monitorLock = new();
    private readonly SemaphoreSlim stateSemaphore = new(1, 1);
    private Thread? watchdogThread;
    private Thread? monitorThread;
    private volatile bool watchdogRunning;
    private volatile bool monitorRunning;
    private volatile bool settling;
    private int watchdogStarted;
    private int monitorStarted;
    private int attempts;
    private int consecutiveFailures;
    private DateTime ignoreUntilUtc = DateTime.MinValue;
    private DateTime lastPromptUtc = DateTime.MinValue;
    private ControllerManager.ControllerSlotManagementMode mode = ControllerManager.ControllerSlotManagementMode.Manual;
    private readonly List<IController> invalidAssignments = new();

    public bool HasSlotIssue { get; private set; }
    public bool HasVirtualSlot1Issue { get; private set; }
    public string SlotIssueReason { get; private set; } = string.Empty;
    public event Action<bool, string>? SlotIssueChanged;

    public ControllerSlotHelper(Action<ControllerManager.ControllerManagerStatus, int> updateStatus, Action<bool, string> updateIssue)
    {
        this.updateStatus = updateStatus;
        this.updateIssue = updateIssue;
    }

    public void SetMode(ControllerManager.ControllerSlotManagementMode value)
    {
        mode = value;
        if (mode == ControllerManager.ControllerSlotManagementMode.Automatic)
            consecutiveFailures = 0;
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref monitorStarted, 1) == 1)
            return;
        lock (monitorLock)
        {
            monitorRunning = true;
            monitorThread = new Thread(MonitorLoop) { IsBackground = true, Name = "ControllerSlotMonitor" };
            monitorThread.Start();
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref monitorStarted, 0) == 0)
            return;
        monitorRunning = false;
        if (monitorThread?.IsAlive == true)
            monitorThread.Join(2000);
        monitorThread = null;
        StopWatchdog();
    }

    public void HandleTopologyChanged()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            if (watchdogRunning || settling)
                return;
            SlotProbeResult probe = await ProbeAsync().ConfigureAwait(false);
            if (!probe.IsAvailable)
                return;
            SetIssue(probe.NeedsFix, probe.Reason);
            if (!probe.NeedsFix)
                return;
            if (mode == ControllerManager.ControllerSlotManagementMode.Automatic)
                StartWatchdog(false, ControllerManager.SlotFixTrigger.Automatic);
            else
                SendPrompt(probe.Reason);
        });
    }

    public void TriggerFix(bool resetAttempts) => StartWatchdog(resetAttempts, ControllerManager.SlotFixTrigger.Manual);

    public void SetIgnoreWindow() => ignoreUntilUtc = DateTime.UtcNow.AddMinutes(5);

    public bool AssignXInputSlot(XInputController controller, byte targetSlot)
    {
        if (controller.UserIndex == targetSlot)
            return true;
        XInputController? displaced = ControllerManager.GetControllerFromSlot<XInputController>((UserIndex)targetSlot, true) ?? ControllerManager.GetControllerFromSlot<XInputController>((UserIndex)targetSlot, false);
        if (OpenXInput.SetUserIndex(controller.GetContainerPath(), targetSlot, false) != OpenXInput.ERROR_SUCCESS)
            return false;
        if (displaced is not null && !ReferenceEquals(displaced, controller))
            displaced.CyclePort();
        controller.CyclePort();
        return true;
    }

    private void StartWatchdog(bool reset, ControllerManager.SlotFixTrigger trigger)
    {
        if (reset)
        {
            Interlocked.Exchange(ref attempts, 0);
            consecutiveFailures = 0;
            settling = false;
        }
        if (Interlocked.Exchange(ref watchdogStarted, 1) == 1)
            return;
        lock (watchdogLock)
        {
            watchdogRunning = true;
            watchdogThread = new Thread(() => WatchdogLoop(trigger)) { IsBackground = true, Name = "ControllerSlotFix" };
            watchdogThread.Start();
        }
    }

    public void StopWatchdog()
    {
        if (Interlocked.Exchange(ref watchdogStarted, 0) == 0)
            return;
        watchdogRunning = false;
        if (watchdogThread?.IsAlive == true)
            watchdogThread.Join(3000);
        watchdogThread = null;
    }

    private void MonitorLoop()
    {
        while (monitorRunning)
        {
            Thread.Sleep(1000);
            if (watchdogRunning)
                continue;
            if (settling)
            {
                if (ControllerManager.PowerCyclers.IsEmpty)
                    settling = false;
                continue;
            }
            SlotProbeResult probe = ProbeAsync().GetAwaiter().GetResult();
            if (!probe.IsAvailable)
                continue;
            SetIssue(probe.NeedsFix, probe.Reason);
            if (!probe.NeedsFix)
            {
                MarkSuccess();
                continue;
            }
            if (mode == ControllerManager.ControllerSlotManagementMode.Automatic)
                StartWatchdog(false, ControllerManager.SlotFixTrigger.Automatic);
        }
    }

    private async Task<SlotProbeResult> ProbeAsync()
    {
        if (!await stateSemaphore.WaitAsync(CrossWaitTimeout).ConfigureAwait(false))
            return SlotProbeResult.Unavailable;
        try
        {
            Dictionary<byte, IController> owners = new();
            List<IController> invalid = new();
            Task[] tasks = GetSlotControllers().Where(c => !c.IsBusy).Select(controller => Task.Run(() =>
            {
                byte index = DeviceManager.GetXInputIndex(controller.GetContainerPath());
                if (index == byte.MaxValue)
                    return;
                ((IXInputController)controller).AttachController(index);
                lock (owners)
                {
                    if (owners.TryGetValue(index, out IController? first))
                    {
                        lock (invalid)
                        {
                            if (!invalid.Contains(first)) invalid.Add(first);
                            invalid.Add(controller);
                        }
                    }
                    else owners[index] = controller;
                }
            })).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            invalidAssignments.Clear();
            invalidAssignments.AddRange(invalid);
            bool ensureVirtual = VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected && (HasSlotController(true) || HasSlotController(false));
            bool virtualInOne = !ensureVirtual || GetSlotController(UserIndex.One, false) is not null;
            bool needsFix = invalid.Count > 0 || !virtualInOne;
            string reason = invalid.Count > 0 ? "Duplicate controller slot assignment detected." : !virtualInOne ? "Virtual controller is not occupying slot 1." : string.Empty;
            return new SlotProbeResult(needsFix, ensureVirtual, virtualInOne, invalid.Count > 0, invalid.Any(c => c.IsVirtual()), reason, true);
        }
        finally { stateSemaphore.Release(); }
    }

    private void WatchdogLoop(ControllerManager.SlotFixTrigger trigger)
    {
        try
        {
            Interlocked.Exchange(ref attempts, 0);
            updateStatus(ControllerManager.ControllerManagerStatus.Busy, 0);
            for (int attempt = 1; attempt <= MaxAttempts && watchdogRunning; attempt++)
            {
                Interlocked.Exchange(ref attempts, attempt);
                updateStatus(ControllerManager.ControllerManagerStatus.Busy, attempt);
                SlotProbeResult probe = ProbeAsync().GetAwaiter().GetResult();
                if (!probe.IsAvailable) { Thread.Sleep(100); continue; }
                if (!probe.NeedsFix) { MarkSuccess(); return; }
                if (probe.HasInvalidControllers) FixDuplicates(probe);
                if (probe.EnsureVirtualSlot1 && !probe.VirtualInSlot1 && !FixVirtualSlot(attempt)) break;
                Thread.Sleep(1000);
                probe = ProbeAsync().GetAwaiter().GetResult();
                if (probe.IsAvailable && !probe.NeedsFix) { MarkSuccess(); return; }
            }
            FinalizeFailure();
        }
        catch { FinalizeFailure(); }
        finally
        {
            settling = true;
            watchdogRunning = false;
            Interlocked.Exchange(ref watchdogStarted, 0);
            watchdogThread = null;
        }
    }

    private void FixDuplicates(SlotProbeResult probe)
    {
        if (probe.HasInvalidVirtual)
        {
            VirtualManager.Suspend(false).GetAwaiter().GetResult();
            Thread.Sleep(1000);
            VirtualManager.Resume(false).GetAwaiter().GetResult();
            WaitUntil(() => HasSlotController(false), TimeSpan.FromSeconds(4));
        }
        foreach (IController controller in invalidAssignments)
            if (!controller.IsVirtual()) { controller.CyclePort(); Thread.Sleep(500); }
    }

    private bool FixVirtualSlot(int attempt)
    {
        if (!HasSlotController(true))
        {
            if (HasSlotController(false) && GetSlotController(UserIndex.One, false) is null)
            {
                VirtualManager.Suspend(false).GetAwaiter().GetResult(); Thread.Sleep(1000); VirtualManager.Resume(false).GetAwaiter().GetResult();
                WaitUntil(() => GetSlotControllers(false).Any(c => c.GetVendorID() == VirtualManager.VendorId && c.GetProductID() == VirtualManager.ProductId), TimeSpan.FromSeconds(4));
            }
            return true;
        }
        IController? physical = new[] { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four, UserIndex.Any }.Select(slot => GetSlotController(slot, true)).FirstOrDefault(c => c is not null);
        if (physical is null)
            return false;
        if (GetSlotControllers(true).FirstOrDefault(c => c.IsBluetooth() && c.IsBusy) is IController busy && !ControllerManager.PowerCyclers.ContainsKey(busy.GetContainerInstanceId()))
            return false;
        ControllerManager.SuspendController(physical.GetContainerInstanceId());
        WaitUntil(() => GetSlotController((UserIndex)physical.UserIndex, true) is null, TimeSpan.FromSeconds(4));
        VirtualManager.SetControllerMode(HIDmode.NoController).GetAwaiter().GetResult();
        WaitUntil(() => !HasSlotController(false), TimeSpan.FromSeconds(4));
        if (attempt > 1)
        {
            int used = VirtualManager.CreateTemporaryControllers(XInputController.MaxControllers);
            WaitUntil(() => ControllerManager.GetVirtualControllers<XInputController>().Count() >= used, TimeSpan.FromSeconds(4));
            VirtualManager.DisposeTemporaryControllers();
            WaitUntil(() => ControllerManager.GetVirtualControllers<XInputController>().Count() <= used, TimeSpan.FromSeconds(4));
        }
        VirtualManager.SetControllerMode(HIDmode.Xbox360Controller).GetAwaiter().GetResult();
        WaitUntil(() => HasSlotController(false), TimeSpan.FromSeconds(4));
        return true;
    }

    private void MarkSuccess()
    {
        ControllerManager.ResumeControllers();
        SetIssue(false, string.Empty);
        consecutiveFailures = 0;
        updateStatus(ControllerManager.ControllerManagerStatus.Succeeded, 0);
        Interlocked.Exchange(ref attempts, 0);
    }

    private void FinalizeFailure()
    {
        ControllerManager.ResumeControllers();
        if (++consecutiveFailures >= MaxConsecutiveFailures && mode == ControllerManager.ControllerSlotManagementMode.Automatic)
        {
            mode = ControllerManager.ControllerSlotManagementMode.Manual;
            ManagerFactory.settingsManager.SetProperty("ControllerSlotManagementMode", (int)mode);
        }
        try
        {
            SlotProbeResult probe = ProbeAsync().GetAwaiter().GetResult();
            if (probe.IsAvailable) SetIssue(probe.NeedsFix, probe.Reason);
        }
        catch { }
        updateStatus(ControllerManager.ControllerManagerStatus.Failed, 0);
        Interlocked.Exchange(ref attempts, 0);
    }

    private void SetIssue(bool hasIssue, string reason)
    {
        reason ??= string.Empty;
        if (HasSlotIssue == hasIssue && SlotIssueReason == reason) return;
        HasSlotIssue = hasIssue;
        HasVirtualSlot1Issue = hasIssue && reason == "Virtual controller is not occupying slot 1.";
        SlotIssueReason = reason;
        updateIssue(hasIssue, reason);
        SlotIssueChanged?.Invoke(hasIssue, reason);
    }

    private void SendPrompt(string reason)
    {
        if (DateTime.UtcNow < ignoreUntilUtc || DateTime.UtcNow - lastPromptUtc < TimeSpan.FromSeconds(30)) return;
        lastPromptUtc = DateTime.UtcNow;
        ToastManager.SendToast(new ToastRequest { Title = "Controller slot management", Content = string.IsNullOrWhiteSpace(reason) ? "A controller slot issue was detected. Click Fix to attempt a reset." : $"A controller slot issue was detected: {reason} Click Fix to attempt a reset.", ActivationCommand = "OpenControllerPage", Actions = { new ToastAction { Label = "Adjust order", Command = "SlotFixReset", Callback = _ => TriggerFix(true) }, new ToastAction { Label = "Ignore", Command = "SlotFixIgnore", Callback = _ => SetIgnoreWindow() } } });
    }

    private static IEnumerable<IController> GetSlotControllers(bool? physical = null) => physical switch
    {
        true => ControllerManager.GetPhysicalControllers<IController>().Where(c => c is IXInputController),
        false => ControllerManager.GetVirtualControllers<IController>().Where(c => c is IXInputController),
        null => ControllerManager.GetPhysicalControllers<IController>().Where(c => c is IXInputController)
            .Concat(ControllerManager.GetVirtualControllers<IController>().Where(c => c is IXInputController))
    };
    private static bool HasSlotController(bool physical) => GetSlotControllers(physical).Any();
    private static IController? GetSlotController(UserIndex slot, bool physical) => GetSlotControllers(physical).FirstOrDefault(c => c.GetUserIndex() == (int)slot);
    private static void WaitUntil(Func<bool> condition, TimeSpan timeout) { DateTime deadline = DateTime.UtcNow + timeout; while (DateTime.UtcNow < deadline && !condition()) Thread.Sleep(100); }

    private sealed record SlotProbeResult(bool NeedsFix, bool EnsureVirtualSlot1, bool VirtualInSlot1, bool HasInvalidControllers, bool HasInvalidVirtual, string Reason, bool IsAvailable)
    {
        public static readonly SlotProbeResult Unavailable = new(false, false, true, false, false, string.Empty, false);
    }
}
