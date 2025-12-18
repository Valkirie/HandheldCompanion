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

    private object updateLock = new();

    private Layout currentLayout = new();
    private Layout profileLayout = new();
    private Layout? defaultLayout = null;
    private Layout? desktopLayout = null;

    private ControllerState outputState = new();

    private const string desktopLayoutFile = "desktop";

    public string TemplatesPath;

    public FileSystemWatcher layoutWatcher { get; set; }
    private Timer layoutTimer;

    public LayoutManager()
    {
        // initialize path(s)
        ManagerPath = Path.Combine(App.SettingsPath, "layouts");
        TemplatesPath = Path.Combine(App.SettingsPath, "templates");

        // create path(s)
        if (!Directory.Exists(ManagerPath))
            Directory.CreateDirectory(ManagerPath);
        if (!Directory.Exists(TemplatesPath))
            Directory.CreateDirectory(TemplatesPath);

        // Ensure the path exists before setting FileSystemWatcher
        if (!Directory.Exists(TemplatesPath))
            return;

        // monitor layout files
        layoutWatcher = new FileSystemWatcher
        {
            Path = TemplatesPath,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };

        // prepare timer
        layoutTimer = new(100) { AutoReset = false };
        layoutTimer.Elapsed += LayoutTimer_Elapsed;
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // process community templates
        string[] fileEntries = Directory.GetFiles(TemplatesPath, "*.json", SearchOption.AllDirectories);
        foreach (string fileName in fileEntries)
            ProcessLayoutTemplate(fileName);

        // process default templates
        foreach (LayoutTemplate layoutTemplate in Templates)
            Updated?.Invoke(layoutTemplate);

        string desktopFile = Path.Combine(ManagerPath, $"{desktopLayoutFile}.json");
        desktopLayout = ProcessLayout(desktopFile);
        if (desktopLayout is null)
        {
            desktopLayout = LayoutTemplate.DesktopLayout.Layout.Clone() as Layout;
            DesktopLayout_Updated(desktopLayout);
        }

        // manage desktop layout events
        desktopLayout.Updated += DesktopLayout_Updated;

        // manage layout watcher events
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

    private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessEx.ProcessFilter filter)
    {
        CheckProfileLayout();
    }

    private void GamepadFocusManager_FocusChanged(string Name)
    {
        CheckProfileLayout();
    }

    private void QueryProfile()
    {
        // ref
        defaultLayout = ManagerFactory.profileManager.GetDefault().Layout;
        defaultLayout.Updated += DefaultLayout_Updated;

        // manage events
        ManagerFactory.profileManager.Applied += ProfileManager_Applied;

        ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
    }

    private void MultimediaManager_Initialized()
    {
        QueryMedia();
    }

    private void QueryMedia()
    {
        // do something
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        bool DesktopLayoutOnStart = ManagerFactory.settingsManager.GetBoolean("DesktopLayoutOnStart");
        if (DesktopLayoutOnStart)
            ManagerFactory.settingsManager.SetProperty("LayoutMode", (int)LayoutModes.Desktop);
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // stop timer(s)
        layoutTimer.Stop();

        // manage desktop layout events
        desktopLayout.Updated -= DesktopLayout_Updated;

        // manage layout watcher events
        layoutWatcher.Created -= LayoutWatcher_Template;
        layoutWatcher.Changed -= LayoutWatcher_Template;

        // manage events
        ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
        ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        UIGamepad.GotFocus -= GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus -= GamepadFocusManager_FocusChanged;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;

        base.Stop();
    }

    // this event is called from non main thread and it creates LayoutTemplate which is a WPF element
    private void LayoutWatcher_Template(object sender, FileSystemEventArgs e)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            ProcessLayoutTemplate(e.FullPath);
        });
    }

    private Layout? ProcessLayout(string fileName)
    {
        Layout layout = null;

        try
        {
            string outputraw = File.ReadAllText(fileName);
            layout = JsonConvert.DeserializeObject<Layout>(outputraw, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse Layout {0}. {1}", fileName, ex.Message);
        }

        // failed to parse
        if (layout is null)
            LogManager.LogError("Could not parse Layout {0}", fileName);

        return layout;
    }

    private void ProcessLayoutTemplate(string fileName)
    {
        LayoutTemplate layoutTemplate = null;

        try
        {
            string outputraw = File.ReadAllText(fileName);
            layoutTemplate = JsonConvert.DeserializeObject<LayoutTemplate>(outputraw, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
        }
        catch (Exception ex)
        {
            LogManager.LogError("Could not parse LayoutTemplate {0}. {1}", fileName, ex.Message);
        }

        // failed to parse
        if (layoutTemplate is null || layoutTemplate.Layout is null)
        {
            LogManager.LogError("Could not parse LayoutTemplate {0}", fileName);
            return;
        }

        // todo: implement deduplication
        Templates.Add(layoutTemplate);
        Updated?.Invoke(layoutTemplate);
    }

    private void DesktopLayout_Updated(Layout layout)
    {
        SerializeLayout(layout, desktopLayoutFile);

        // update desktop layout
        // ref
        desktopLayout = layout;

        CheckProfileLayout();
    }

    private void ProfileManager_Applied(Profile profile, UpdateSource source)
    {
        // use profile layout (will be cloned during SetActiveLayout)
        // ref
        profileLayout = profile.Layout;

        CheckProfileLayout();
    }

    private void ProfileManager_Initialized()
    {
        QueryProfile();
    }

    private void DefaultLayout_Updated(Layout layout)
    {
        UpdateInherit();
    }

    private void LayoutTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        LayoutModes layoutMode = (LayoutModes)ManagerFactory.settingsManager.GetInt("LayoutMode");

        if (layoutMode == LayoutModes.Gamepad)
        {
            if (!currentLayout.Equals(profileLayout))
                SetActiveLayout(profileLayout);
        }
        else if (layoutMode == LayoutModes.Desktop)
        {
            if (!currentLayout.Equals(desktopLayout))
                SetActiveLayout(desktopLayout);
        }
        else if (layoutMode == LayoutModes.Auto)
        {
            if (UIGamepad.HasFocus() && defaultLayout is not null)
            {
                if (!currentLayout.Equals(defaultLayout))
                    SetActiveLayout(defaultLayout);
            }
            else
            {
                ProcessEx processEx = ProcessManager.GetCurrent();
                Layout targetLayout = (processEx == null || processEx.IsGame() || processEx.Filter == ProcessEx.ProcessFilter.HandheldCompanion) ? profileLayout : desktopLayout;

                if (!currentLayout.Equals(targetLayout))
                    SetActiveLayout(targetLayout);
            }
        }
    }

    private void CheckProfileLayout()
    {
        layoutTimer.Stop();
        layoutTimer.Start();
    }

    public Layout? GetDefault()
    {
        return defaultLayout;
    }

    public Layout? GetCurrent()
    {
        return currentLayout;
    }

    public Layout? GetDesktop()
    {
        return desktopLayout;
    }

    public void SerializeLayout(Layout layout, string fileName)
    {
        var jsonString = JsonConvert.SerializeObject(layout, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        fileName = Path.Combine(ManagerPath, $"{fileName}.json");
        if (FileUtils.IsFileWritable(fileName))
            File.WriteAllText(fileName, jsonString);
    }

    public void SerializeLayoutTemplate(LayoutTemplate layoutTemplate)
    {
        string fileName = Path.Combine(TemplatesPath, $"{layoutTemplate.Name}_{layoutTemplate.Author}.json");
        if (File.Exists(fileName))
        {
            // get previous template with same name and author
            LayoutTemplate template = Templates.FirstOrDefault(t => t.Name == layoutTemplate.Name && t.Author == layoutTemplate.Author);
            if (template is not null)
                layoutTemplate.Guid = template.Guid;
        }

        var jsonString = JsonConvert.SerializeObject(layoutTemplate, Formatting.Indented, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        if (FileUtils.IsFileWritable(fileName))
            File.WriteAllText(fileName, jsonString);
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "LayoutMode":
                CheckProfileLayout();
                break;
        }
    }

    private void SetActiveLayout(Layout layout)
    {
        lock (updateLock)
        {
            // clone
            currentLayout = layout.Clone() as Layout;

            // (re)apply inheritance
            UpdateInherit();
            // build caches for the new layout
            BuildPlans();

            LayoutChanged?.Invoke(currentLayout);
        }
    }

    // --- Mapping plans & caches (rebuilt when currentLayout changes) ---
    private Dictionary<ButtonFlags, IActions[]> _buttonPlan = new();
    private Dictionary<AxisLayoutFlags, IActions[]> _axisPlan = new();
    private Dictionary<AxisLayoutFlags, IActions> _gyroPlan = new(); // single action per flag in GyroLayout

    // Cache AxisFlags (X,Y) for each AxisLayoutFlags to avoid per-tick GetAxisFlags & lookups
    private readonly Dictionary<AxisLayoutFlags, (AxisFlags X, AxisFlags Y)> _axisXY = new();

    // Reusable arrays to avoid multiple enumerations per tick
    private ButtonFlags[] _plannedButtons = Array.Empty<ButtonFlags>();
    private AxisLayoutFlags[] _plannedAxes = Array.Empty<AxisLayoutFlags>();
    private AxisLayoutFlags[] _plannedGyroAxes = Array.Empty<AxisLayoutFlags>();

    private void UpdateInherit()
    {
        lock (updateLock)
        {
            // Check for inherit(s) and replace actions with default layout actions where necessary
            foreach (ButtonFlags buttonFlags in ButtonState.AllButtons.Union(IDevice.GetCurrent().OEMButtons))
            {
                if (currentLayout.ButtonLayout.TryGetValue(buttonFlags, out var actions) && actions.Any(action => action is InheritActions))
                {
                    // Replace with default layout actions
                    if (defaultLayout.ButtonLayout.TryGetValue(buttonFlags, out var defaultActions))
                        currentLayout.ButtonLayout[buttonFlags].AddRange(defaultActions);
                }
            }

            // Check for inherit(s) and replace actions with default layout actions where necessary
            foreach (AxisLayoutFlags axisLayout in AxisState.AllAxisLayoutFlags)
            {
                if (currentLayout.AxisLayout.TryGetValue(axisLayout, out List<IActions>? actions))
                {
                    foreach (IActions action in actions.Where(act => act is InheritActions))
                    {
                        // Replace with default layout actions
                        if (defaultLayout.AxisLayout.TryGetValue(axisLayout, out var defaultActions))
                            currentLayout.AxisLayout[axisLayout] = defaultActions;
                    }
                }
            }
        }
    }

    private void BuildPlans()
    {
        // Clear old plans
        _buttonPlan.Clear();
        _axisPlan.Clear();
        _gyroPlan.Clear();

        // Buttons
        foreach (var kv in currentLayout.ButtonLayout)
        {
            // Store as array to avoid enumerator allocations & repeated Count checks
            var actions = kv.Value is List<IActions> list ? list.ToArray() : kv.Value.ToArray();
            _buttonPlan[kv.Key] = actions;
        }

        // Axes (sticks/pads/triggers routed as axes)
        foreach (var kv in currentLayout.AxisLayout)
        {
            var actions = kv.Value is List<IActions> list ? list.ToArray() : kv.Value.ToArray();
            _axisPlan[kv.Key] = actions;
        }

        // Gyro
        foreach (var kv in currentLayout.GyroLayout)
        {
            // GyroLayout maps one action per flag in current design
            _gyroPlan[kv.Key] = kv.Value;
        }

        // Frozen key arrays for faster iteration
        _plannedButtons = _buttonPlan.Keys.ToArray();
        _plannedAxes = _axisPlan.Keys.ToArray();
        _plannedGyroAxes = _gyroPlan.Keys.ToArray();

        // Cache AxisFlags(X,Y) for every AxisLayoutFlags we will touch
        _axisXY.Clear();
        void EnsureXY(AxisLayoutFlags f)
        {
            if (_axisXY.ContainsKey(f)) return;
            var layout = AxisLayout.Layouts[f];
            _axisXY[f] = (layout.GetAxisFlags('X'), layout.GetAxisFlags('Y'));
        }

        foreach (var f in _plannedAxes) EnsureXY(f);
        foreach (var f in _plannedGyroAxes) EnsureXY(f);

        // Also ensure all possible output targets referenced by actions are cached
        foreach (var actions in _axisPlan.Values)
            for (int i = 0; i < actions.Length; i++)
                if (actions[i] is AxisActions aa) EnsureXY(aa.Axis);
                else if (actions[i] is TriggerActions ta) EnsureXY(ta.Axis);

        foreach (var actions in _buttonPlan.Values)
            for (int i = 0; i < actions.Length; i++)
                if (actions[i] is TriggerActions ta) EnsureXY(ta.Axis);
    }

    public ControllerState MapController(ControllerState controllerState, float delta)
    {
        // 1:1 mapping when no active layout
        if (currentLayout is null)
            return controllerState;

        lock (updateLock)
        {
            // Reset output
            outputState.ButtonState.Clear();
            outputState.AxisState.Clear();
            outputState.GyroState.CopyFrom(controllerState.GyroState);

            // compute ShiftSlot
            ShiftSlot shiftSlot = ShiftSlot.None;

            // in ms
            delta *= 1000.0f;

            // Iterate only over buttons that actually have actions, but also consider real buttons present in state
            // to maintain shifter behavior even if the button is unmapped elsewhere.
            foreach (ButtonFlags button in ButtonState.AllButtons)
            {
                bool value = controllerState.ButtonState[button];

                if (!_buttonPlan.TryGetValue(button, out var actions))
                    continue;

                for (int i = 0; i < actions.Length; i++)
                {
                    var act = actions[i];
                    if (act.actionType != ActionType.Shift)
                        continue;

                    // cast once
                    var sAction = (ShiftActions)act;
                    sAction.Execute(button, value, shiftSlot, delta);
                    if (sAction.GetValue())
                        shiftSlot |= sAction.ShiftSlot;
                }
            }

            // Also check axes (triggers) for shift actions
            for (int a = 0; a < _plannedAxes.Length; a++)
            {
                var inFlag = _plannedAxes[a];
                var actions = _axisPlan[inFlag];

                // Read origin values
                var xyIn = _axisXY[inFlag];
                var inX = controllerState.AxisState[xyIn.X];
                var inY = controllerState.AxisState[xyIn.Y];

                // Prepare InLayout for shift detection
                var inLayout = AxisLayout.Layouts[inFlag];
                inLayout.vector.X = inX;
                inLayout.vector.Y = inY;

                for (int i = 0; i < actions.Length; i++)
                {
                    var act = actions[i];
                    if (act.actionType != ActionType.Shift)
                        continue;

                    // cast once
                    var sAction = (ShiftActions)act;
                    sAction.Execute(inLayout, shiftSlot, delta);
                    if (sAction.GetValue())
                        shiftSlot |= sAction.ShiftSlot;
                }
            }

            // process button-based map
            foreach (ButtonFlags button in ButtonState.AllButtons)
            {
                bool value = controllerState.ButtonState[button];

                if (!_buttonPlan.TryGetValue(button, out var actions))
                {
                    // passthrough if unmapped (keeps fake buttons behavior)
                    outputState.ButtonState[button] = value;
                    continue;
                }

                for (int i = 0; i < actions.Length; i++)
                {
                    var act = actions[i];

                    switch (act.actionType)
                    {
                        case ActionType.Button:
                            {
                                var b = (ButtonActions)act;
                                b.Execute(button, value, shiftSlot, delta);
                                bool outVal = b.GetValue() || outputState.ButtonState[b.Button];
                                outputState.ButtonState[b.Button] = outVal;
                                break;
                            }
                        case ActionType.Keyboard:
                            {
                                ((KeyboardActions)act).Execute(button, value, shiftSlot, delta);
                                break;
                            }
                        case ActionType.Mouse:
                            {
                                ((MouseActions)act).Execute(button, value, shiftSlot, delta);
                                break;
                            }
                        case ActionType.Trigger:
                            {
                                var t = (TriggerActions)act;
                                t.Execute(button, value, shiftSlot, delta);
                                // write Y only (triggers)
                                var xyOut = _axisXY[t.Axis];
                                byte add = t.GetValue();
                                outputState.AxisState[xyOut.Y] = (byte)Math.Clamp(outputState.AxisState[xyOut.Y] + add, byte.MinValue, byte.MaxValue);
                                break;
                            }
                    }

                    ApplyActionStateSideEffects(actions, i);
                }
            }

            // process axis-based map
            for (int a = 0; a < _plannedAxes.Length; a++)
            {
                var inFlag = _plannedAxes[a];
                var actions = _axisPlan[inFlag];

                // Read origin values
                var xyIn = _axisXY[inFlag];
                var inX = controllerState.AxisState[xyIn.X];
                var inY = controllerState.AxisState[xyIn.Y];

                // Prepare InLayout (mutate the shared static to avoid allocs)
                var inLayout = AxisLayout.Layouts[inFlag];
                inLayout.vector.X = inX;
                inLayout.vector.Y = inY;

                // "Touched" for pads (needed by MouseActions)
                bool touched = false;
                if (ControllerState.AxisTouchButtons.TryGetValue(inLayout.flags, out var touchButton))
                    touched = controllerState.ButtonState[touchButton];

                for (int i = 0; i < actions.Length; i++)
                {
                    var act = actions[i];

                    switch (act.actionType)
                    {
                        case ActionType.Button:
                            {
                                var b = (ButtonActions)act;
                                b.Execute(inLayout, shiftSlot, delta);
                                bool outVal = b.GetValue() || outputState.ButtonState[b.Button];
                                outputState.ButtonState[b.Button] = outVal;
                                break;
                            }
                        case ActionType.Keyboard:
                            {
                                ((KeyboardActions)act).Execute(inLayout, shiftSlot, delta);
                                break;
                            }
                        case ActionType.Joystick:
                            {
                                var ax = (AxisActions)act;
                                ax.Execute(inLayout, shiftSlot, delta);

                                var xyOut = _axisXY[ax.Axis];
                                short addX = (short)Math.Clamp(ax.XOuput, short.MinValue, short.MaxValue);
                                short addY = (short)Math.Clamp(ax.YOuput, short.MinValue, short.MaxValue);

                                outputState.AxisState[xyOut.X] = (short)Math.Clamp(outputState.AxisState[xyOut.X] + addX, short.MinValue, short.MaxValue);
                                outputState.AxisState[xyOut.Y] = (short)Math.Clamp(outputState.AxisState[xyOut.Y] + addY, short.MinValue, short.MaxValue);
                                break;
                            }
                        case ActionType.Trigger:
                            {
                                var t = (TriggerActions)act;
                                t.Execute(xyIn.Y, inLayout.vector.Y, shiftSlot, delta); // Y drives trigger

                                var xyOut = _axisXY[t.Axis];
                                byte add = t.GetValue();
                                outputState.AxisState[xyOut.Y] = (byte)Math.Clamp(outputState.AxisState[xyOut.Y] + add, byte.MinValue, byte.MaxValue);
                                break;
                            }
                        case ActionType.Mouse:
                            {
                                var m = (MouseActions)act;
                                m.Execute(inLayout, touched, shiftSlot, delta);
                                break;
                            }
                    }

                    ApplyActionStateSideEffects(actions, i);
                }
            }

            // process gyro-based map
            for (int g = 0; g < _plannedGyroAxes.Length; g++)
            {
                var inFlag = _plannedGyroAxes[g];
                var action = _gyroPlan[inFlag];
                if (action is null) continue;

                var xyIn = _axisXY[inFlag];
                var inLayout = AxisLayout.Layouts[inFlag];
                inLayout.vector.X = controllerState.AxisState[xyIn.X];
                inLayout.vector.Y = controllerState.AxisState[xyIn.Y];

                switch (action.actionType)
                {
                    case ActionType.Joystick:
                        {
                            var a = (AxisActions)action;
                            a.Execute(inLayout, shiftSlot, delta);

                            // blend with stick using gyro weight logic
                            var xyOut = _axisXY[a.Axis];
                            var current = new Vector2(outputState.AxisState[xyOut.X], outputState.AxisState[xyOut.Y]);

                            float len = Math.Clamp(current.Length() / short.MaxValue, 0f, 1f);
                            float weightFactor = a.gyroWeight - len;
                            var result = current + a.GetValue() * weightFactor;

                            outputState.AxisState[xyOut.X] = (short)Math.Clamp(result.X, short.MinValue, short.MaxValue);
                            outputState.AxisState[xyOut.Y] = (short)Math.Clamp(result.Y, short.MinValue, short.MaxValue);
                            break;
                        }
                    case ActionType.Mouse:
                        {
                            var m = (MouseActions)action;
                            bool touched = false;
                            if (ControllerState.AxisTouchButtons.TryGetValue(inLayout.flags, out var touchButton))
                                touched = controllerState.ButtonState[touchButton];

                            m.Execute(inLayout, touched, shiftSlot, delta);
                            break;
                        }
                }
            }

            return outputState;
        }
    }

    private static void ApplyActionStateSideEffects(IActions[] actions, int currentIndex)
    {
        var action = actions[currentIndex];
        var slot = action.ShiftSlot;

        if (action.actionState == ActionState.Aborted || action.actionState == ActionState.Stopped)
        {
            // Stop/sanitize siblings with same ShiftSlot
            for (int j = 0; j < actions.Length; j++)
            {
                if (j == currentIndex) continue;
                var a2 = actions[j];
                if (a2.ShiftSlot != slot) continue;
                if (!a2.HasInterruptable) continue;
                if (a2.actionState == ActionState.Succeed) continue;

                var st = a2.actionState;
                if (st != ActionState.Stopped && st != ActionState.Aborted)
                    a2.actionState = ActionState.Stopped;
            }

            // On Aborted, force the next interruptable action with same ShiftSlot
            if (action.actionState == ActionState.Aborted)
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
        else if (action.actionState == ActionState.Running)
        {
            // Suspend siblings (same ShiftSlot), except Succeed
            for (int j = 0; j < actions.Length; j++)
            {
                if (j == currentIndex) continue;
                var a2 = actions[j];
                if (a2.ShiftSlot != slot) continue;
                if (!a2.HasInterruptable) continue;
                if (a2.actionState == ActionState.Succeed) continue;

                a2.actionState = ActionState.Suspended;
            }
        }
    }

    #region events

    public event LayoutChangedEventHandler LayoutChanged;
    public delegate void LayoutChangedEventHandler(Layout layout);

    public event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(LayoutTemplate layoutTemplate);

    #endregion
}