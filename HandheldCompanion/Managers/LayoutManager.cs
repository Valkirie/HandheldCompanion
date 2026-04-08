using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Timers;

namespace HandheldCompanion.Managers;

public class LayoutManager : IManager
{
    public static List<LayoutTemplate> Templates =
    [
        LayoutTemplate.DefaultLayout,
        LayoutTemplate.DesktopLayout,
        LayoutTemplate.NintendoLayout,
        LayoutTemplate.KeyboardLayout,
        LayoutTemplate.GamepadMouseLayout,
        LayoutTemplate.GamepadJoystickLayout
    ];

    private readonly object updateLock = new();

    private Layout currentLayout = new();
    private Layout profileLayout = new();
    private Layout? defaultLayout = null;
    private Layout? desktopLayout = null;

    private readonly ControllerState outputState = new();

    private const string desktopLayoutFile = "desktop";

    public string TemplatesPath;

    public FileSystemWatcher layoutWatcher { get; set; }
    private readonly Timer layoutTimer;

    //  Mapping plans & caches (rebuilt when currentLayout changes) 
    private Dictionary<ButtonFlags, IActions[]> _buttonPlan = new();
    private Dictionary<AxisLayoutFlags, IActions[]> _axisPlan = new();
    private Dictionary<AxisLayoutFlags, IActions> _gyroPlan = new();  // one action per axis flag

    // X/Y AxisFlags for each AxisLayoutFlags — cached to avoid per-tick lookups
    private readonly Dictionary<AxisLayoutFlags, (AxisFlags X, AxisFlags Y)> _axisXY = new();

    // Frozen key arrays for fast index-based iteration in the hot path
    private ButtonFlags[] _plannedButtons = Array.Empty<ButtonFlags>();
    private AxisLayoutFlags[] _plannedAxes = Array.Empty<AxisLayoutFlags>();
    private AxisLayoutFlags[] _plannedGyroAxes = Array.Empty<AxisLayoutFlags>();

    //  Construction ─

    public LayoutManager()
    {
        ManagerPath = Path.Combine(App.SettingsPath, "layouts");
        TemplatesPath = Path.Combine(App.SettingsPath, "templates");

        Directory.CreateDirectory(ManagerPath);
        Directory.CreateDirectory(TemplatesPath);

        layoutWatcher = new FileSystemWatcher
        {
            Path = TemplatesPath,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };

        layoutTimer = new Timer(100) { AutoReset = false };
        layoutTimer.Elapsed += LayoutTimer_Elapsed;
    }

    //  Lifecycle ─

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // Load community templates from disk
        foreach (string file in Directory.GetFiles(TemplatesPath, "*.json", SearchOption.AllDirectories))
            ProcessLayoutTemplate(file);

        // Publish default (built-in) templates
        foreach (LayoutTemplate template in Templates)
            Updated?.Invoke(template);

        // Load or initialize the desktop layout
        string desktopFile = Path.Combine(ManagerPath, $"{desktopLayoutFile}.json");
        desktopLayout = ProcessLayout(desktopFile);
        if (desktopLayout is null)
        {
            desktopLayout = LayoutTemplate.DesktopLayout.Layout.Clone() as Layout;
            DesktopLayout_Updated(desktopLayout);
        }

        // manage layout watcher events
        desktopLayout.Updated += DesktopLayout_Updated;
        layoutWatcher.Created += LayoutWatcher_Template;
        layoutWatcher.Changed += LayoutWatcher_Template;

        // manage events
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        UIGamepad.GotFocus += GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus += GamepadFocusManager_FocusChanged;

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

        switch (ManagerFactory.profileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryProfile();
                break;
        }

        base.Start();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        layoutTimer.Stop();

        desktopLayout.Updated -= DesktopLayout_Updated;
        layoutWatcher.Created -= LayoutWatcher_Template;
        layoutWatcher.Changed -= LayoutWatcher_Template;

        ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
        ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        UIGamepad.GotFocus -= GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus -= GamepadFocusManager_FocusChanged;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;

        base.Stop();
    }

    // Event handlers
    private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessEx.ProcessFilter filter) => CheckProfileLayout();
    private void GamepadFocusManager_FocusChanged(string Name) => CheckProfileLayout();

    private void MultimediaManager_Initialized() => QueryMedia();
    private void SettingsManager_Initialized() => QuerySettings();
    private void ProfileManager_Initialized() => QueryProfile();
    private void DefaultLayout_Updated(Layout _) => UpdateInherit();

    private void QueryProfile()
    {
        defaultLayout = ManagerFactory.profileManager.GetDefault().Layout;
        defaultLayout.Updated += DefaultLayout_Updated;

        ManagerFactory.profileManager.Applied += ProfileManager_Applied;
        ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
    }

    private void QueryMedia()
    {
        // Reserved for future use
    }

    private void QuerySettings()
    {
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        bool desktopLayoutOnStart = ManagerFactory.settingsManager.GetBoolean("DesktopLayoutOnStart");
        if (desktopLayoutOnStart)
            ManagerFactory.settingsManager.SetProperty("LayoutMode", (int)LayoutModes.Desktop);
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        if (name == "LayoutMode")
            CheckProfileLayout();
    }

    private void DesktopLayout_Updated(Layout layout)
    {
        SerializeLayout(layout, desktopLayoutFile);
        desktopLayout = layout;
        CheckProfileLayout();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        profileLayout = profile.Layout;
        CheckProfileLayout();
    }

    //  Layout switching
    private void CheckProfileLayout()
    {
        // Debounce — restart the timer each time a change signal arrives
        layoutTimer.Stop();
        layoutTimer.Start();
    }

    private void LayoutTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        LayoutModes layoutMode = (LayoutModes)ManagerFactory.settingsManager.GetInt("LayoutMode");

        Layout? target = layoutMode switch
        {
            LayoutModes.Gamepad => profileLayout,
            LayoutModes.Desktop => desktopLayout,
            LayoutModes.Auto => ResolveAutoLayout(),
            _ => null,
        };

        if (target is not null && !currentLayout.Equals(target))
            SetActiveLayout(target);
    }

    /// <summary>Selects the appropriate layout for Auto mode.</summary>
    private Layout ResolveAutoLayout()
    {
        if (UIGamepad.HasFocus() && defaultLayout is not null)
            return defaultLayout;

        ProcessEx? process = ProcessManager.GetCurrent();
        bool useProfile = process is null
                       || process.IsGame()
                       || process.Filter == ProcessEx.ProcessFilter.HandheldCompanion;

        return useProfile ? profileLayout : desktopLayout!;
    }

    private void SetActiveLayout(Layout layout)
    {
        lock (updateLock)
        {
            currentLayout = layout.Clone() as Layout;
            UpdateInherit();
            BuildPlans();
            LayoutChanged?.Invoke(currentLayout);
        }
    }

    //  File I/O
    // Called from a non-UI thread by FileSystemWatcher — marshal to UI thread
    private void LayoutWatcher_Template(object sender, FileSystemEventArgs e) => UIHelper.TryInvoke(() => ProcessLayoutTemplate(e.FullPath));

    private Layout? ProcessLayout(string fileName)
    {
        try
        {
            string json = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<Layout>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse Layout {0}. {1}", fileName, ex.Message);
            return null;
        }
    }

    private void ProcessLayoutTemplate(string fileName)
    {
        LayoutTemplate? layoutTemplate = null;

        try
        {
            string json = File.ReadAllText(fileName);
            layoutTemplate = JsonConvert.DeserializeObject<LayoutTemplate>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse LayoutTemplate {0}. {1}", fileName, ex.Message);
        }

        if (layoutTemplate?.Layout is null)
        {
            LogManager.LogError("Could not parse LayoutTemplate {0}", fileName);
            return;
        }

        // TODO: implement deduplication
        Templates.Add(layoutTemplate);
        Updated?.Invoke(layoutTemplate);
    }

    public void SerializeLayout(Layout layout, string fileName)
    {
        string json = JsonConvert.SerializeObject(layout, Formatting.Indented, TypeNameSettings);
        string outPath = Path.Combine(ManagerPath, $"{fileName}.json");

        if (FileUtils.IsFileWritable(outPath))
            File.WriteAllText(outPath, json);
    }

    public void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        string fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}_{layoutTemplate.Author}.json");

        if (File.Exists(fileName))
        {
            // Preserve the Guid of an existing template with the same name+author
            LayoutTemplate? existing = Templates.FirstOrDefault(
                t => t.Name == layoutTemplate.Name && t.Author == layoutTemplate.Author);
            if (existing is not null)
                layoutTemplate.Guid = existing.Guid;
        }

        string json = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, TypeNameSettings);
        if (FileUtils.IsFileWritable(fileName))
            File.WriteAllText(fileName, json);
    }

    private static readonly JsonSerializerSettings TypeNameSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All
    };

    //  Inheritance resolution
    private void UpdateInherit()
    {
        lock (updateLock)
        {
            // Buttons: append default-layout actions where the current layout inherits
            foreach (ButtonFlags flag in ButtonState.AllButtons.Union(IDevice.GetCurrent().OEMButtons))
            {
                if (!currentLayout.ButtonLayout.TryGetValue(flag, out var actions)) continue;
                if (!actions.Any(a => a is InheritActions)) continue;

                if (defaultLayout!.ButtonLayout.TryGetValue(flag, out var defaults))
                    actions.AddRange(defaults);
            }

            // Axes: replace the entire list with the default when inheriting
            foreach (AxisLayoutFlags flag in AxisState.AllAxisLayoutFlags)
            {
                if (!currentLayout.AxisLayout.TryGetValue(flag, out var actions)) continue;
                if (!actions.Any(a => a is InheritActions)) continue;

                if (defaultLayout!.AxisLayout.TryGetValue(flag, out var defaults))
                    currentLayout.AxisLayout[flag] = defaults;
            }
        }
    }

    //  Plan building
    private void BuildPlans()
    {
        _buttonPlan.Clear();
        _axisPlan.Clear();
        _gyroPlan.Clear();

        foreach (var kv in currentLayout.ButtonLayout)
            _buttonPlan[kv.Key] = kv.Value.ToArray();

        foreach (var kv in currentLayout.AxisLayout)
            _axisPlan[kv.Key] = kv.Value.ToArray();

        foreach (var kv in currentLayout.GyroLayout)
            _gyroPlan[kv.Key] = kv.Value;

        _plannedButtons = _buttonPlan.Keys.ToArray();
        _plannedAxes = _axisPlan.Keys.ToArray();
        _plannedGyroAxes = _gyroPlan.Keys.ToArray();

        // Cache X/Y AxisFlags for all flags we will touch during mapping
        _axisXY.Clear();

        foreach (var flag in _plannedAxes) EnsureAxisXY(flag);
        foreach (var flag in _plannedGyroAxes) EnsureAxisXY(flag);

        foreach (var actions in _axisPlan.Values)
            foreach (var action in actions)
            {
                if (action is AxisActions aa) EnsureAxisXY(aa.Axis);
                if (action is TriggerActions ta) EnsureAxisXY(ta.Axis);
            }

        foreach (var actions in _buttonPlan.Values)
            foreach (var action in actions)
                if (action is TriggerActions ta) EnsureAxisXY(ta.Axis);
    }

    private void EnsureAxisXY(AxisLayoutFlags flag)
    {
        if (_axisXY.ContainsKey(flag)) return;
        var layout = AxisLayout.Layouts[flag];
        _axisXY[flag] = (layout.GetAxisFlags('X'), layout.GetAxisFlags('Y'));
    }

    //  Controller mapping (hot path — called at 125–1000 Hz)
    public ControllerState MapController(ControllerState controllerState, float delta)
    {
        if (currentLayout is null)
            return controllerState; // passthrough when no layout is active

        lock (updateLock)
        {
            outputState.ButtonState.Clear();
            outputState.AxisState.Clear();
            outputState.GyroState.CopyFrom(controllerState.GyroState);

            // delta arrives in seconds; all timer logic expects milliseconds
            float deltaMs = delta * 1000f;

            ShiftSlot shiftSlot = ComputeShiftSlot(controllerState, deltaMs);

            ProcessButtonActions(controllerState, shiftSlot, deltaMs);
            ProcessAxisActions(controllerState, shiftSlot, deltaMs);
            ProcessGyroActions(controllerState, shiftSlot, deltaMs);
        }

        return outputState;
    }

    /// <summary>
    /// First pass: evaluate all ShiftActions to determine the active shift slot(s).
    /// Both button-based and axis-based (trigger) shifts are considered.
    /// </summary>
    private ShiftSlot ComputeShiftSlot(ControllerState state, float deltaMs)
    {
        ShiftSlot shiftSlot = ShiftSlot.None;

        // Button shifts
        foreach (ButtonFlags button in ButtonState.AllButtons)
        {
            if (!_buttonPlan.TryGetValue(button, out var actions)) continue;

            bool pressed = state.ButtonState[button];
            foreach (var action in actions)
            {
                if (action is not ShiftActions shift) continue;
                shift.Execute(button, pressed, shiftSlot, deltaMs);
                if (shift.GetValue()) shiftSlot |= shift.ShiftSlot;
            }
        }

        // Axis shifts (e.g. trigger-based shift)
        for (int i = 0; i < _plannedAxes.Length; i++)
        {
            AxisLayoutFlags flag = _plannedAxes[i];
            var actions = _axisPlan[flag];
            var xyIn = _axisXY[flag];
            var layout = AxisLayout.Layouts[flag];

            layout.vector.X = state.AxisState[xyIn.X];
            layout.vector.Y = state.AxisState[xyIn.Y];

            foreach (var action in actions)
            {
                if (action is not ShiftActions shift) continue;
                shift.Execute(layout, shiftSlot, deltaMs);
                if (shift.GetValue()) shiftSlot |= shift.ShiftSlot;
            }
        }

        return shiftSlot;
    }

    /// <summary>
    /// Second pass: process all non-Shift button actions and write to outputState.
    /// Unmapped buttons are passed through transparently.
    /// </summary>
    private void ProcessButtonActions(ControllerState state, ShiftSlot shiftSlot, float deltaMs)
    {
        foreach (ButtonFlags button in ButtonState.AllButtons)
        {
            bool pressed = state.ButtonState[button];

            if (!_buttonPlan.TryGetValue(button, out var actions))
            {
                outputState.ButtonState[button] = pressed; // passthrough
                continue;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];

                switch (action.actionType)
                {
                    case ActionType.Button:
                        if (action is ButtonActions bA)
                        {
                            bA.Execute(button, pressed, shiftSlot, deltaMs);
                            outputState.ButtonState[bA.Button] |= bA.GetValue();
                        }
                        break;

                    case ActionType.Keyboard:
                        if (action is KeyboardActions kA)
                            kA.Execute(button, pressed, shiftSlot, deltaMs);
                        break;

                    case ActionType.Mouse:
                        if (action is MouseActions mA)
                            mA.Execute(button, pressed, shiftSlot, deltaMs);
                        break;

                    case ActionType.Trigger:
                        if (action is TriggerActions tA)
                        {
                            tA.Execute(button, pressed, shiftSlot, deltaMs);
                            var xyOut = _axisXY[tA.Axis];
                            outputState.AxisState[xyOut.Y] = ClampByte(outputState.AxisState[xyOut.Y] + tA.GetValue());
                        }
                        break;
                }

                ApplyActionStateSideEffects(actions, i);
            }
        }
    }

    /// <summary>
    /// Third pass: process axis (stick / pad / trigger) actions and write to outputState.
    /// </summary>
    private void ProcessAxisActions(ControllerState state, ShiftSlot shiftSlot, float deltaMs)
    {
        for (int a = 0; a < _plannedAxes.Length; a++)
        {
            AxisLayoutFlags flag = _plannedAxes[a];
            var actions = _axisPlan[flag];
            var xyIn = _axisXY[flag];
            var layout = AxisLayout.Layouts[flag];

            layout.vector.X = state.AxisState[xyIn.X];
            layout.vector.Y = state.AxisState[xyIn.Y];

            bool touched = false;
            if (ControllerState.AxisTouchButtons.TryGetValue(layout.flags, out var touchButton))
                touched = state.ButtonState[touchButton];

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];

                switch (action.actionType)
                {
                    case ActionType.Button:
                        if (action is ButtonActions bA)
                        {
                            bA.Execute(layout, shiftSlot, deltaMs);
                            outputState.ButtonState[bA.Button] |= bA.GetValue();
                        }
                        break;

                    case ActionType.Keyboard:
                        if (action is KeyboardActions kA)
                            kA.Execute(layout, shiftSlot, deltaMs);
                        break;

                    case ActionType.Joystick:
                        if (action is AxisActions aX)
                        {
                            aX.Execute(layout, shiftSlot, deltaMs);
                            var xyOut = _axisXY[aX.Axis];
                            outputState.AxisState[xyOut.X] = ClampShort(outputState.AxisState[xyOut.X] + (short)Math.Clamp(aX.XOuput, short.MinValue, short.MaxValue));
                            outputState.AxisState[xyOut.Y] = ClampShort(outputState.AxisState[xyOut.Y] + (short)Math.Clamp(aX.YOuput, short.MinValue, short.MaxValue));
                        }
                        break;

                    case ActionType.Trigger:
                        if (action is TriggerActions tA)
                        {
                            tA.Execute(xyIn.Y, layout.vector.Y, shiftSlot, deltaMs); // Y axis drives the trigger
                            var xyOut = _axisXY[tA.Axis];
                            outputState.AxisState[xyOut.Y] = ClampByte(outputState.AxisState[xyOut.Y] + tA.GetValue());
                        }
                        break;

                    case ActionType.Mouse:
                        if (action is MouseActions mA)
                            mA.Execute(layout, touched, shiftSlot, deltaMs);
                        break;
                }

                ApplyActionStateSideEffects(actions, i);
            }
        }
    }

    /// <summary>
    /// Fourth pass: process gyroscope actions, blending gyro output into the target stick.
    /// </summary>
    private void ProcessGyroActions(ControllerState state, ShiftSlot shiftSlot, float deltaMs)
    {
        for (int g = 0; g < _plannedGyroAxes.Length; g++)
        {
            AxisLayoutFlags flag = _plannedGyroAxes[g];
            IActions? action = _gyroPlan[flag];
            if (action is null) continue;

            var xyIn = _axisXY[flag];
            var layout = AxisLayout.Layouts[flag];
            layout.vector.X = state.AxisState[xyIn.X];
            layout.vector.Y = state.AxisState[xyIn.Y];

            switch (action.actionType)
            {
                case ActionType.Joystick:
                    if (action is AxisActions aA)
                    {
                        aA.Execute(layout, shiftSlot, deltaMs);

                        // Blend gyro output with the current stick value using gyroWeight
                        var xyOut = _axisXY[aA.Axis];
                        var current = new Vector2(outputState.AxisState[xyOut.X], outputState.AxisState[xyOut.Y]);

                        float stickNorm = Math.Clamp(current.Length() / short.MaxValue, 0f, 1f);
                        float weightFactor = aA.gyroWeight - stickNorm;
                        var blended = current + aA.GetValue() * weightFactor;

                        outputState.AxisState[xyOut.X] = ClampShort(blended.X);
                        outputState.AxisState[xyOut.Y] = ClampShort(blended.Y);
                    }
                    break;

                case ActionType.Mouse:
                    if (action is MouseActions mA)
                    {
                        bool touched = false;
                        if (ControllerState.AxisTouchButtons.TryGetValue(layout.flags, out var touchButton))
                            touched = state.ButtonState[touchButton];

                        mA.Execute(layout, touched, shiftSlot, deltaMs);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Propagates action state changes to sibling actions sharing the same <see cref="ShiftSlot"/>:
    /// <list type="bullet">
    ///   <item><b>Running</b>  → suspends all interruptable siblings.</item>
    ///   <item><b>Aborted</b>  → stops all interruptable siblings; forces the next one.</item>
    ///   <item><b>Stopped</b>  → stops all interruptable siblings.</item>
    /// </list>
    /// </summary>
    private static void ApplyActionStateSideEffects(IActions[] actions, int currentIndex)
    {
        var current = actions[currentIndex];
        ShiftSlot slot = current.ShiftSlot;
        ActionState state = current.actionState;

        if (state == ActionState.Running)
        {
            // Suspend every interruptable sibling in the same slot
            for (int j = 0; j < actions.Length; j++)
            {
                if (j == currentIndex) continue;
                var sibling = actions[j];
                if (sibling.ShiftSlot != slot) continue;
                if (!sibling.HasInterruptable) continue;
                if (sibling.actionState == ActionState.Succeed) continue;

                sibling.actionState = ActionState.Suspended;
            }
        }
        else if (state == ActionState.Aborted || state == ActionState.Stopped)
        {
            // Stop all interruptable siblings
            for (int j = 0; j < actions.Length; j++)
            {
                if (j == currentIndex) continue;
                var sibling = actions[j];
                if (sibling.ShiftSlot != slot) continue;
                if (!sibling.HasInterruptable) continue;
                if (sibling.actionState == ActionState.Succeed) continue;

                if (sibling.actionState != ActionState.Stopped &&
                    sibling.actionState != ActionState.Aborted)
                    sibling.actionState = ActionState.Stopped;
            }

            // On Abort: force the next eligible sibling (short-press fallback)
            if (state == ActionState.Aborted)
            {
                for (int j = currentIndex + 1; j < actions.Length; j++)
                {
                    var next = actions[j];
                    if (next.ShiftSlot == slot && next.HasInterruptable)
                    {
                        next.actionState = ActionState.Forced;
                        break;
                    }
                }
            }
        }
    }

    //  Arithmetic helpers (avoid repeated Math.Clamp boxing)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short ClampShort(float value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(int value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

    //  Public accessors
    public Layout? GetDefault() => defaultLayout;
    public Layout? GetCurrent() => currentLayout;
    public Layout? GetDesktop() => desktopLayout;

    //  Events
    public event LayoutChangedEventHandler? LayoutChanged;
    public delegate void LayoutChangedEventHandler(Layout layout);

    public event UpdatedEventHandler? Updated;
    public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);
}