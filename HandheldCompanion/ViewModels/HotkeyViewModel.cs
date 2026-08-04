using HandheldCompanion.Commands;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels.Controls;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Commands.ICommands;

namespace HandheldCompanion.ViewModels
{
    public class HotkeyViewModel : BaseViewModel
    {
        public ObservableCollection<FontIconViewModel> ButtonGlyphs { get; set; } = [];

        private List<Type> _functionTypes = [];

        // Expose shared function items for XAML CollectionViewSource binding
        public ObservableCollection<ComboBoxItemViewModel> FunctionItems
        {
            get
            {
                EnsureSharedFunctionData();
                return _sharedFunctionItems!;
            }
        }

        // Shared across all instances – built once to avoid repeated Activator.CreateInstance
        private static readonly object _sharedDataLock = new();
        private static List<Type>? _sharedFunctionTypes;
        private static ObservableCollection<ComboBoxItemViewModel>? _sharedFunctionItems;

        private Hotkey _Hotkey = null!;
        public Hotkey Hotkey
        {
            get => _Hotkey;
            set
            {
                // todo: we need to check if _hotkey != value but this will return false because this is a pointer
                // I've implemented all required Clone() functions but not sure where to call them

                _Hotkey = value;
                _Hotkey.command.Executed += Command_Executed;
                _Hotkey.command.Updated += Command_Updated;

                // refresh all properties
                OnPropertyChanged(string.Empty);
                OnPropertyChanged(nameof(Hotkey));
                OnPropertyChanged(nameof(IsPinned));
                OnPropertyChanged(nameof(CommandTypeIndex));
                OnPropertyChanged(nameof(Command));

                if (Hotkey.command is FunctionCommands functionCommands)
                    OnPropertyChanged(nameof(FunctionIndex));

                if (Hotkey.command is ExecutableCommands executableCommands)
                    OnPropertyChanged(nameof(ExecutablePath));

                DrawChords();
                DrawNameAndDescription();
            }
        }

        public ICommands Command => Hotkey.command;

        private bool _IsExecuted;
        public bool IsExecuted
        {
            get => _IsExecuted;
            set
            {
                if (_IsExecuted != value)
                {
                    _IsExecuted = value;
                    OnPropertyChanged(nameof(IsExecuted));
                }
            }
        }

        // CycleSubProfile
        public int CyclingDirection
        {
            get
            {
                return Hotkey.command is CycleSubProfileCommands cycleSubProfileCommands ? cycleSubProfileCommands.CycleIndex : 0;
            }
            set
            {
                if (value != CyclingDirection)
                {
                    if (Hotkey.command is CycleSubProfileCommands cycleSubProfileCommands)
                        cycleSubProfileCommands.CycleIndex = value;

                    OnPropertyChanged(nameof(CyclingDirection));
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        private void Command_Executed(ICommands command)
        {
            OnPropertyChanged(nameof(IsToggled));
            IsExecuted = true;

            // Optionally reset IsBlinking after a delay
            Task.Delay(125).ContinueWith(_ =>
            {
                IsExecuted = false;
            });
        }

        private void Command_Updated(ICommands command)
        {
            OnPropertyChanged(nameof(LiveGlyph));
            OnPropertyChanged(nameof(LiveName));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsToggled));
            OnPropertyChanged(nameof(HasDoubleExecute));
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _Hotkey.command.Executed -= Command_Executed;
                _Hotkey.command.Updated -= Command_Updated;
                ControllerManager.Initialized -= ControllerManager_Initialized;
                ControllerManager.ControllerSelected -= UpdateController;
            }

            base.Dispose(disposing);
        }

        public string Glyph => Hotkey.command.Glyph;
        public string LiveGlyph => Hotkey.command.LiveGlyph;
        public string LiveName => CanCustom ? CustomName : Hotkey.command.LiveName;
        public string FontFamily => Hotkey.command.FontFamily;
        public int FontSize => Hotkey.command.FontSize;
        public bool HasDoubleExecute => Hotkey.command.HasDoubleExecute;

        public ObservableCollection<GlyphIconInfo> TriggerChordParts { get; set; } = [];

        public bool HasTriggerChord => TriggerChordParts.Count > 0;

        public string CustomName
        {
            get
            {
                return Hotkey.Name;
            }
            set
            {
                if (value != Hotkey.Name)
                {
                    Hotkey.Name = value;
                    OnPropertyChanged(nameof(CustomName));
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        private string _Name = "Name of the actual hotkey";
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (value != _Name)
                {
                    _Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string _Description = "Description of the actual hotkey, generated based on command and arguments";
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                if (value != _Description)
                {
                    _Description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public bool IsPinned => Hotkey.IsPinned;
        public bool CanUnpin => Hotkey.command.CanUnpin;

        private bool _IsListening = false;
        public bool IsListening => _IsListening;

        private bool _IsListeningOutput = false;
        public bool IsListeningOutput => _IsListeningOutput;

        public void SetListening(bool listening, InputsChordTarget chordTarget)
        {
            switch (chordTarget)
            {
                case InputsChordTarget.Input:
                    _IsListening = listening;
                    OnPropertyChanged(nameof(IsListening));
                    break;
                case InputsChordTarget.Output:
                    _IsListeningOutput = listening;
                    OnPropertyChanged(nameof(IsListeningOutput));
                    break;
            }
        }

        private string _KeyboardChord = string.Empty;
        public string KeyboardChord
        {
            get
            {
                return _KeyboardChord;
            }
            set
            {
                if (value != _KeyboardChord)
                {
                    _KeyboardChord = value;
                    OnPropertyChanged(nameof(KeyboardChord));
                }
            }
        }

        private string _KeyboardOutputChord = string.Empty;
        public string KeyboardOutputChord
        {
            get
            {
                return string.IsNullOrEmpty(_KeyboardOutputChord) ? Resources.Hotkey_OutputDefineTip : _KeyboardOutputChord;
            }
            set
            {
                if (value != _KeyboardOutputChord)
                {
                    _KeyboardOutputChord = value;
                    OnPropertyChanged(nameof(KeyboardOutputChord));
                }
            }
        }

        private string _InputsChordType = string.Empty;
        public string InputsChordType
        {
            get
            {
                return _InputsChordType;
            }
            set
            {
                if (value != _InputsChordType)
                {
                    _InputsChordType = value;
                    OnPropertyChanged(nameof(InputsChordType));
                }
            }
        }

        public int CommandTypeIndex
        {
            get
            {
                return (int)Hotkey.command.commandType;
            }
            set
            {
                if (value != CommandTypeIndex)
                {
                    switch ((CommandType)value)
                    {
                        case CommandType.None:
                            Hotkey.command = new EmptyCommands();
                            break;
                        case CommandType.Function:
                            FunctionIndex = 1;
                            break;
                        case CommandType.Keyboard:
                            Hotkey.command = new KeyboardCommands();
                            break;
                        case CommandType.Executable:
                            Hotkey.command = new ExecutableCommands();
                            break;
                        case CommandType.PowerShell:
                            Hotkey.command = new PowerShellCommands();
                            break;
                    }

                    // reset custom name
                    CustomName = Hotkey.command.Name;

                    OnPropertyChanged(nameof(CommandTypeIndex));
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        public int FunctionIndex
        {
            get
            {
                Type typeToSearch = Hotkey.command.GetType();
                if (_functionTypes.Contains(typeToSearch))
                    return _functionTypes.IndexOf(typeToSearch);
                else
                    return 0;
            }
            set
            {
                if (value != FunctionIndex)
                {
                    Type typeToCreate = _functionTypes[value];
                    ICommands? commands = Activator.CreateInstance(typeToCreate) as ICommands;
                    if (commands is null)
                        return;

                    Hotkey.command = commands;

                    // reset custom name
                    CustomName = Hotkey.command.Name;

                    OnPropertyChanged(nameof(FunctionIndex));
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        private string _Chord = string.Empty;
        public string Chord
        {
            get
            {
                return _Chord;
            }
            set
            {
                if (value != _Chord)
                {
                    _Chord = value;
                    OnPropertyChanged(nameof(Chord));
                }
            }
        }

        public int WindowPageIndex
        {
            get
            {
                if (Hotkey.command is QuickToolsCommands quickToolsCommands)
                    return quickToolsCommands.PageIndex;
                else if (Hotkey.command is MainWindowCommands windowCommands)
                    return windowCommands.PageIndex;

                return 0;
            }
            set
            {
                if (Hotkey.command is QuickToolsCommands quickToolsCommands)
                    quickToolsCommands.PageIndex = value;
                else if (Hotkey.command is MainWindowCommands windowCommands)
                    windowCommands.PageIndex = value;

                OnPropertyChanged(nameof(WindowPageIndex));
                ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
            }
        }

        public string ExecutablePath
        {
            get
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                    return executableCommand.Path;
                return string.Empty;
            }
        }

        public string ExecutableArguments
        {
            get
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                    return executableCommand.Arguments;
                return string.Empty;
            }
            set
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                {
                    if (executableCommand.Arguments != value)
                    {
                        executableCommand.Arguments = value;
                        OnPropertyChanged(nameof(ExecutableArguments));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            }
        }

        public int ExecutableWindowStyle
        {
            get
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                    return (int)executableCommand.windowStyle;
                else if (Hotkey.command is PowerShellCommands powerShellCommands)
                    return (int)powerShellCommands.windowStyle;
                return 0;
            }
            set
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                {
                    if (executableCommand.windowStyle != (ProcessWindowStyle)value)
                    {
                        executableCommand.windowStyle = (ProcessWindowStyle)value;
                        OnPropertyChanged(nameof(ExecutableWindowStyle));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
                else if (Hotkey.command is PowerShellCommands powerShellCommands)
                {
                    if (powerShellCommands.windowStyle != (ProcessWindowStyle)value)
                    {
                        powerShellCommands.windowStyle = (ProcessWindowStyle)value;
                        OnPropertyChanged(nameof(ExecutableWindowStyle));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            }
        }

        public bool ExecutableRunAs
        {
            get
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                    return executableCommand.RunAs;
                else if (Hotkey.command is PowerShellCommands powerShellCommands)
                    return powerShellCommands.RunAs;
                return false;
            }
            set
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                {
                    if (executableCommand.RunAs != value)
                    {
                        executableCommand.RunAs = value;
                        OnPropertyChanged(nameof(ExecutableRunAs));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
                else if (Hotkey.command is PowerShellCommands powerShellCommands)
                {
                    if (powerShellCommands.RunAs != value)
                    {
                        powerShellCommands.RunAs = value;
                        OnPropertyChanged(nameof(ExecutableRunAs));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            }
        }

        public string ScriptContent
        {
            get
            {
                if (Hotkey.command is PowerShellCommands powerShellCommands)
                    return powerShellCommands.ScriptContent;
                return string.Empty;
            }
            set
            {
                if (Hotkey.command is PowerShellCommands powerShellCommands)
                {
                    if (powerShellCommands.ScriptContent != value)
                    {
                        powerShellCommands.ScriptContent = value;
                        OnPropertyChanged(nameof(ExecutableRunAs));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            }
        }

        public int OnScreenKeyboardLegacyPosition
        {
            get
            {
                if (Hotkey.command is OnScreenKeyboardLegacyCommands keyboardCommands)
                    return keyboardCommands.KeyboardPosition;
                return 0;
            }
            set
            {
                if (Hotkey.command is OnScreenKeyboardLegacyCommands keyboardCommands)
                {
                    if (keyboardCommands.KeyboardPosition != value)
                    {
                        keyboardCommands.KeyboardPosition = value;
                        OnPropertyChanged(nameof(OnScreenKeyboardLegacyPosition));

                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            }
        }

        public ObservableCollection<MappingTargetViewModel> ButtonCommandsValues { get; } = new();

        public MappingTargetViewModel? ButtonCommandsButton
        {
            get
            {
                if (Hotkey.command is ButtonCommands bc)
                    return ButtonCommandsValues.FirstOrDefault(vm => vm.Tag is ButtonFlags buttonFlags && buttonFlags == bc.ButtonFlags);
                return ButtonCommandsValues.FirstOrDefault();
            }
            set
            {
                if (Hotkey.command is ButtonCommands bc && value?.Tag is ButtonFlags buttonFlags && bc.ButtonFlags != buttonFlags)
                {
                    bc.ButtonFlags = buttonFlags;
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    OnPropertyChanged(nameof(ButtonCommandsButton));
                }
            }
        }

        public short ButtonCommandsDelay
        {
            get
            {
                if (Hotkey.command is ButtonCommands bc)
                    return bc.KeyPressDelay;
                return 250; // ms
            }
            set
            {
                if (Hotkey.command is ButtonCommands bc && bc.KeyPressDelay != value)
                {
                    bc.KeyPressDelay = value;
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    OnPropertyChanged(nameof(ButtonCommandsDelay));
                }
            }
        }

        public bool IsToggled => Hotkey.command.IsToggled;
        public bool IsEnabled => Hotkey.command.IsEnabled;
        public bool CanCustom => Hotkey.command.CanCustom;

        public ICommand DefineButtonCommand { get; private set; }
        public ICommand PinButtonCommand { get; private set; }
        public ICommand DeleteHotkeyCommand { get; private set; }
        public ICommand DefineOutputCommand { get; private set; }
        public ICommand TextBoxClickCommand { get; private set; }
        public ICommand ExecuteCommand { get; private set; }
        public ICommand EraseButtonCommand { get; private set; }
        public ICommand EraseOutputButtonCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        private static void EnsureSharedFunctionData()
        {
            if (_sharedFunctionTypes is not null)
                return;

            lock (_sharedDataLock)
            {
                if (_sharedFunctionTypes is not null)
                    return;

                var types = FunctionCommands.Functions
                    .Where(item => item is Type type && type.IsAssignableTo(typeof(FunctionCommands)))
                    .Cast<Type>().ToList();

                var items = new ObservableCollection<ComboBoxItemViewModel>();
                string currentCategory = "Ungrouped";
                foreach (object value in FunctionCommands.Functions)
                {
                    if (value is string strVal)
                    {
                        currentCategory = strVal;
                    }
                    else if (value is Type function)
                    {
                        if (function == typeof(Separator))
                        {
                            items.Add(new ComboBoxItemViewModel(string.Empty, false, string.Empty));
                        }
                        else
                        {
                            var instance = Activator.CreateInstance(function);
                            ICommands command = (ICommands)instance!;
                            IDisposable? disposable = instance as IDisposable;

                            bool canUnpin = command.CanUnpin;
                            bool isSupported = command.deviceType is null || (command.deviceType == IDevice.GetCurrent().GetType());
                            bool isEnabled = canUnpin && isSupported;

                            items.Add(new ComboBoxItemViewModel(command.Name, isEnabled, currentCategory));
                            disposable?.Dispose();
                        }
                    }
                }

                _sharedFunctionItems = items;
                _sharedFunctionTypes = types;
            }
        }

        private bool IsQuickTools;
        public HotkeyViewModel(Hotkey hotkey, bool isQuickTools = false)
        {
            Hotkey = hotkey;
            IsQuickTools = isQuickTools;

            EnsureSharedFunctionData();
            _functionTypes = _sharedFunctionTypes!;

            DefineButtonCommand = new DelegateCommand(async () =>
            {
                // todo: improve me
                // we need to make sure the key that was pressed to trigger the listening event isn't recorded
                await Task.Delay(100).ConfigureAwait(false); // Avoid blocking the synchronization context
                InputsManager.StartListening(hotkey.ButtonFlags, InputsChordTarget.Input);
            });

            PinButtonCommand = new DelegateCommand(async () =>
            {
                Hotkey.IsPinned = !Hotkey.IsPinned;
                ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
            });

            DeleteHotkeyCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(MainWindow.GetCurrent())
                {
                    Title = string.Format(Resources.ProfilesPage_AreYouSureDelete1, Name),
                    Content = Resources.ProfilesPage_AreYouSureDelete2,
                    CloseButtonText = Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Resources.ProfilesPage_Delete
                };

                ContentDialogResult result = await dialog.ShowAsync();
                switch (result)
                {
                    case ContentDialogResult.None:
                        dialog.Hide();
                        break;
                    case ContentDialogResult.Primary:
                        ManagerFactory.hotkeysManager.DeleteHotkey(Hotkey);
                        break;
                }
            });

            DefineOutputCommand = new DelegateCommand(async () =>
            {
                // todo: improve me
                // we need to make sure the key that was pressed to trigger the listening event isn't recorded
                await Task.Delay(100).ConfigureAwait(false); // Avoid blocking the synchronization context
                InputsManager.StartListening(hotkey.ButtonFlags, InputsChordTarget.Output);
            });

            TextBoxClickCommand = new DelegateCommand(async () =>
            {
                OpenFileDialog openFileDialog = new OpenFileDialog()
                {
                    Filter = "Executable|*.exe",
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (Hotkey.command is ExecutableCommands executableCommand)
                    {
                        executableCommand.Path = openFileDialog.FileName;
                        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            });

            ExecuteCommand = new DelegateCommand(async () =>
            {
                Hotkey.Execute(Hotkey.command.OnKeyDown, Hotkey.command.OnKeyUp, false);
            });

            EraseButtonCommand = new DelegateCommand(async () =>
            {
                Hotkey.inputsChord = new();
                ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
            });

            EraseOutputButtonCommand = new DelegateCommand(async () =>
            {
                if (Hotkey.command is KeyboardCommands keyboardCommands)
                {
                    keyboardCommands.outputChord = new();
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            });

            OpenSettingsCommand = new DelegateCommand(() =>
            {
                if (HotkeysPage.hotkeySettingsPage is not null)
                {
                    HotkeysPage.hotkeySettingsPage.SetHotkey(this);
                    MainWindow.NavView_Navigate(HotkeysPage.hotkeySettingsPage);
                }
            });

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();
        }

        private void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += UpdateController;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                UpdateController(controller);
        }

        protected void UpdateController(IController? controller)
        {
            var newValues = new List<MappingTargetViewModel>();

            if (controller is not null)
            {
                foreach (var button in controller.GetTargetButtons())
                {
                    newValues.Add(new MappingTargetViewModel
                    {
                        Tag = button,
                        Content = controller.GetButtonName(button)
                    });
                }
            }

            UIHelper.TryBeginInvoke(() =>
            {
                ButtonCommandsValues.Clear();
                foreach (var value in newValues)
                    ButtonCommandsValues.Add(value);
                OnPropertyChanged(nameof(ButtonCommandsValues));
            });
        }

        public void DrawChords()
        {
            IController controller = ControllerManager.GetTargetOrDefault();

            // Build new glyphs locally, then swap onto UI thread via UIHelper.
            var newGlyphs = new List<FontIconViewModel>();

            foreach (ButtonFlags buttonFlags in Hotkey.inputsChord.ButtonState.Buttons)
            {
                string glyphString = string.Empty;
                string glyphFont = string.Empty;

                Color? color = controller.GetGlyphColor(buttonFlags);
                Brush? glyphColor = null;
                if (color.HasValue)
                {
                    glyphColor = new SolidColorBrush(color.Value);
                    glyphColor.Freeze();
                }

                switch (buttonFlags)
                {
                    case ButtonFlags.OEM1:
                    case ButtonFlags.OEM2:
                    case ButtonFlags.OEM3:
                    case ButtonFlags.OEM4:
                    case ButtonFlags.OEM5:
                    case ButtonFlags.OEM6:
                    case ButtonFlags.OEM7:
                    case ButtonFlags.OEM8:
                    case ButtonFlags.OEM9:
                    case ButtonFlags.OEM10:
                        glyphString = IDevice.GetCurrent().GetGlyph(buttonFlags);
                        glyphFont = IDevice.GetCurrent().GetFontFamily(buttonFlags);
                        break;
                    default:
                        glyphString = controller.GetGlyph(buttonFlags);
                        glyphFont = controller.GetFontFamily(buttonFlags);
                        break;
                }

                newGlyphs.Add(new FontIconViewModel(Hotkey, this, glyphString, glyphColor, glyphFont));
            }

            UIHelper.TryBeginInvoke(() =>
            {
                ButtonGlyphs.Clear();
                foreach (var glyph in newGlyphs)
                    ButtonGlyphs.Add(glyph);
            });

            switch (Hotkey.command.commandType)
            {
                case CommandType.Keyboard:
                    if (Hotkey.command is KeyboardCommands keyboardCommands)
                        KeyboardOutputChord = string.Join(",", keyboardCommands.outputChord.KeyState.Where(key => key.IsKeyDown).Select(key => (KeyCode)key.KeyValue));
                    break;
            }
            KeyboardChord = string.Join(",", Hotkey.inputsChord.KeyState.Where(key => key.IsKeyDown).Select(key => (KeyCode)key.KeyValue));
            InputsChordType = EnumUtils.GetDescriptionFromEnumValue(Hotkey.inputsChord.chordType);
            var newParts = BuildTriggerChordParts(controller);

            UIHelper.TryBeginInvoke(() =>
            {
                TriggerChordParts.Clear();
                foreach (var part in newParts)
                    TriggerChordParts.Add(part);
                OnPropertyChanged(nameof(HasTriggerChord));
            });
        }

        private List<GlyphIconInfo> BuildTriggerChordParts(IController controller)
        {
            List<GlyphIconInfo> parts = [];

            foreach (var key in Hotkey.inputsChord.KeyState.Where(key => key.IsKeyDown).OrderBy(key => key.Timestamp))
                parts.Add(KeyboardToken((KeyCode)key.KeyValue));

            foreach (var button in Hotkey.inputsChord.ButtonState.Buttons)
                parts.Add(GetTriggerButtonToken(controller, button, IsQuickTools));

            return parts;
        }

        private static GlyphIconInfo GetTriggerButtonToken(IController controller, ButtonFlags button, bool IsQuickTools)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                case ButtonFlags.OEM2:
                case ButtonFlags.OEM3:
                case ButtonFlags.OEM4:
                case ButtonFlags.OEM5:
                case ButtonFlags.OEM6:
                case ButtonFlags.OEM7:
                case ButtonFlags.OEM8:
                case ButtonFlags.OEM9:
                case ButtonFlags.OEM10:
                    {
                        return IDevice.GetCurrent().GetGlyphIconInfo(button, IsQuickTools ? 15 : 22);
                    }
                default:
                    {
                        return controller.GetGlyphIconInfo(button, IsQuickTools ? 15 : 22);
                    }
            }
        }

        private static GlyphIconInfo KeyboardToken(KeyCode key)
        {
            return key switch
            {
                KeyCode.A => new GlyphIconInfo { Glyph = "A", FontSize = 12 },
                KeyCode.B => new GlyphIconInfo { Glyph = "B", FontSize = 12 },
                KeyCode.C => new GlyphIconInfo { Glyph = "C", FontSize = 12 },
                KeyCode.D => new GlyphIconInfo { Glyph = "D", FontSize = 12 },
                KeyCode.E => new GlyphIconInfo { Glyph = "E", FontSize = 12 },
                KeyCode.F => new GlyphIconInfo { Glyph = "F", FontSize = 12 },
                KeyCode.G => new GlyphIconInfo { Glyph = "G", FontSize = 12 },
                KeyCode.H => new GlyphIconInfo { Glyph = "H", FontSize = 12 },
                KeyCode.I => new GlyphIconInfo { Glyph = "I", FontSize = 12 },
                KeyCode.J => new GlyphIconInfo { Glyph = "J", FontSize = 12 },
                KeyCode.K => new GlyphIconInfo { Glyph = "K", FontSize = 12 },
                KeyCode.L => new GlyphIconInfo { Glyph = "L", FontSize = 12 },
                KeyCode.M => new GlyphIconInfo { Glyph = "M", FontSize = 12 },
                KeyCode.N => new GlyphIconInfo { Glyph = "N", FontSize = 12 },
                KeyCode.O => new GlyphIconInfo { Glyph = "O", FontSize = 12 },
                KeyCode.P => new GlyphIconInfo { Glyph = "P", FontSize = 12 },
                KeyCode.Q => new GlyphIconInfo { Glyph = "Q", FontSize = 12 },
                KeyCode.R => new GlyphIconInfo { Glyph = "R", FontSize = 12 },
                KeyCode.S => new GlyphIconInfo { Glyph = "S", FontSize = 12 },
                KeyCode.T => new GlyphIconInfo { Glyph = "T", FontSize = 12 },
                KeyCode.U => new GlyphIconInfo { Glyph = "U", FontSize = 12 },
                KeyCode.V => new GlyphIconInfo { Glyph = "V", FontSize = 12 },
                KeyCode.W => new GlyphIconInfo { Glyph = "W", FontSize = 12 },
                KeyCode.X => new GlyphIconInfo { Glyph = "X", FontSize = 12 },
                KeyCode.Y => new GlyphIconInfo { Glyph = "Y", FontSize = 12 },
                KeyCode.Z => new GlyphIconInfo { Glyph = "Z", FontSize = 12 },

                KeyCode.D0 => new GlyphIconInfo { Glyph = "0", FontSize = 12 },
                KeyCode.D1 => new GlyphIconInfo { Glyph = "1", FontSize = 12 },
                KeyCode.D2 => new GlyphIconInfo { Glyph = "2", FontSize = 12 },
                KeyCode.D3 => new GlyphIconInfo { Glyph = "3", FontSize = 12 },
                KeyCode.D4 => new GlyphIconInfo { Glyph = "4", FontSize = 12 },
                KeyCode.D5 => new GlyphIconInfo { Glyph = "5", FontSize = 12 },
                KeyCode.D6 => new GlyphIconInfo { Glyph = "6", FontSize = 12 },
                KeyCode.D7 => new GlyphIconInfo { Glyph = "7", FontSize = 12 },
                KeyCode.D8 => new GlyphIconInfo { Glyph = "8", FontSize = 12 },
                KeyCode.D9 => new GlyphIconInfo { Glyph = "9", FontSize = 12 },

                KeyCode.NumPad0 => new GlyphIconInfo { Glyph = "0", FontSize = 12 },
                KeyCode.NumPad1 => new GlyphIconInfo { Glyph = "1", FontSize = 12 },
                KeyCode.NumPad2 => new GlyphIconInfo { Glyph = "2", FontSize = 12 },
                KeyCode.NumPad3 => new GlyphIconInfo { Glyph = "3", FontSize = 12 },
                KeyCode.NumPad4 => new GlyphIconInfo { Glyph = "4", FontSize = 12 },
                KeyCode.NumPad5 => new GlyphIconInfo { Glyph = "5", FontSize = 12 },
                KeyCode.NumPad6 => new GlyphIconInfo { Glyph = "6", FontSize = 12 },
                KeyCode.NumPad7 => new GlyphIconInfo { Glyph = "7", FontSize = 12 },
                KeyCode.NumPad8 => new GlyphIconInfo { Glyph = "8", FontSize = 12 },
                KeyCode.NumPad9 => new GlyphIconInfo { Glyph = "9", FontSize = 12 },

                KeyCode.Control or KeyCode.LControlKey or KeyCode.RControlKey or KeyCode.LControl or KeyCode.RControl => new GlyphIconInfo { Glyph = "Ctrl", FontSize = 12 },
                KeyCode.Shift or KeyCode.LShiftKey or KeyCode.RShiftKey or KeyCode.LShift or KeyCode.RShift => new GlyphIconInfo { Glyph = "Shift", FontSize = 12 },
                KeyCode.Menu or KeyCode.LMenu or KeyCode.RMenu or KeyCode.LAlt or KeyCode.RAlt => new GlyphIconInfo { Glyph = "Alt", FontSize = 12 },
                KeyCode.LWin or KeyCode.RWin => new GlyphIconInfo { FontFamily = new("PromptFont"), Glyph = "\uE008", FontSize = 12 },
                KeyCode.Tab => new GlyphIconInfo { Glyph = "Tab", FontSize = 12 },
                KeyCode.CapsLock => new GlyphIconInfo { Glyph = "Caps", FontSize = 12 },
                KeyCode.Backspace => new GlyphIconInfo { Glyph = "Back", FontSize = 12 },
                KeyCode.Return => new GlyphIconInfo { Glyph = "Enter", FontSize = 12 },
                KeyCode.Escape => new GlyphIconInfo { Glyph = "Esc", FontSize = 12 },
                KeyCode.PageUp => new GlyphIconInfo { Glyph = "PgUp", FontSize = 12 },
                KeyCode.PageDown => new GlyphIconInfo { Glyph = "PgDn", FontSize = 12 },
                KeyCode.Insert => new GlyphIconInfo { Glyph = "Ins", FontSize = 12 },
                KeyCode.Delete => new GlyphIconInfo { Glyph = "Del", FontSize = 12 },
                KeyCode.Home => new GlyphIconInfo { Glyph = "Home", FontSize = 12 },
                KeyCode.End => new GlyphIconInfo { Glyph = "End", FontSize = 12 },
                KeyCode.Left => new GlyphIconInfo { Glyph = "←", FontSize = 12 },
                KeyCode.Up => new GlyphIconInfo { Glyph = "↑", FontSize = 12 },
                KeyCode.Right => new GlyphIconInfo { Glyph = "→", FontSize = 12 },
                KeyCode.Down => new GlyphIconInfo { Glyph = "↓", FontSize = 12 },
                KeyCode.PrintScreen => new GlyphIconInfo { Glyph = "PrtSc", FontSize = 12 },
                KeyCode.Scroll => new GlyphIconInfo { Glyph = "ScrLk", FontSize = 12 },
                KeyCode.Pause => new GlyphIconInfo { Glyph = "Pause", FontSize = 12 },
                KeyCode.NumLock => new GlyphIconInfo { Glyph = "Num", FontSize = 12 },
                KeyCode.Space => new GlyphIconInfo { Glyph = "Space", FontSize = 12 },

                KeyCode.F1 => new GlyphIconInfo { Glyph = "F1", FontSize = 12 },
                KeyCode.F2 => new GlyphIconInfo { Glyph = "F2", FontSize = 12 },
                KeyCode.F3 => new GlyphIconInfo { Glyph = "F3", FontSize = 12 },
                KeyCode.F4 => new GlyphIconInfo { Glyph = "F4", FontSize = 12 },
                KeyCode.F5 => new GlyphIconInfo { Glyph = "F5", FontSize = 12 },
                KeyCode.F6 => new GlyphIconInfo { Glyph = "F6", FontSize = 12 },
                KeyCode.F7 => new GlyphIconInfo { Glyph = "F7", FontSize = 12 },
                KeyCode.F8 => new GlyphIconInfo { Glyph = "F8", FontSize = 12 },
                KeyCode.F9 => new GlyphIconInfo { Glyph = "F9", FontSize = 12 },
                KeyCode.F10 => new GlyphIconInfo { Glyph = "F10", FontSize = 12 },
                KeyCode.F11 => new GlyphIconInfo { Glyph = "F11", FontSize = 12 },
                KeyCode.F12 => new GlyphIconInfo { Glyph = "F12", FontSize = 12 },

                KeyCode.Oem1 or KeyCode.OemSemicolon => new GlyphIconInfo { Glyph = ";", FontSize = 12 },
                KeyCode.Oem2 or KeyCode.OemQuestion => new GlyphIconInfo { Glyph = "/", FontSize = 12 },
                KeyCode.Oem3 or KeyCode.Oemtilde => new GlyphIconInfo { Glyph = "`", FontSize = 12 },
                KeyCode.Oem4 or KeyCode.OemOpenBrackets => new GlyphIconInfo { Glyph = "[", FontSize = 12 },
                KeyCode.Oem5 or KeyCode.OemPipe or KeyCode.OemBackslash => new GlyphIconInfo { Glyph = "\\", FontSize = 12 },
                KeyCode.Oem6 or KeyCode.OemCloseBrackets => new GlyphIconInfo { Glyph = "]", FontSize = 12 },
                KeyCode.Oem7 or KeyCode.OemQuotes => new GlyphIconInfo { Glyph = "'", FontSize = 12 },
                KeyCode.OemMinus => new GlyphIconInfo { Glyph = "-", FontSize = 12 },
                KeyCode.Oemplus => new GlyphIconInfo { Glyph = "+", FontSize = 12 },
                KeyCode.Oemcomma => new GlyphIconInfo { Glyph = ",", FontSize = 12 },
                KeyCode.OemPeriod => new GlyphIconInfo { Glyph = ".", FontSize = 12 },

                _ => new GlyphIconInfo { Glyph = key.ToString(), FontSize = 12 }
            };
        }

        private void DrawNameAndDescription()
        {
            if (Hotkey.command.commandType == CommandType.Executable)
            {
                if (Hotkey.command is ExecutableCommands executableCommands)
                {
                    // GetAppProperties is a blocking COM/Shell call; run it off the UI thread
                    string execPath = executableCommands.Path;
                    ICommands cmd = Hotkey.command;
                    Task.Run(() =>
                    {
                        string name;
                        if (File.Exists(execPath))
                        {
                            Dictionary<string, string> AppProperties = ProcessUtils.GetAppProperties(execPath);
                            string ProductName = AppProperties.TryGetValue("FileDescription", out var property) ? property : AppProperties.GetValueOrDefault("ItemFolderNameDisplay", string.Empty);
                            string Executable = System.IO.Path.GetFileName(execPath);
                            name = string.IsNullOrEmpty(ProductName) ? Executable : ProductName;
                        }
                        else
                        {
                            name = cmd.Name;
                        }

                        Name = name;
                        Description = cmd.Description;
                        OnPropertyChanged(nameof(FontFamily));
                        OnPropertyChanged(nameof(Glyph));
                    });
                    return;
                }
            }

            Name = string.IsNullOrEmpty(CustomName) ? Hotkey.command.Name : CustomName;
            Description = Hotkey.command.Description;
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(Glyph));
        }
    }
}
