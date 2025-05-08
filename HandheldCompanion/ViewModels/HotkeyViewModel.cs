using HandheldCompanion.Commands;
using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
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

        private List<Type> _functionTypes;

        private ObservableCollection<ComboBoxItemViewModel> _functionItems = [];
        public ListCollectionView FunctionCollectionView { get; set; }

        private Hotkey _Hotkey;
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
        }

        public override void Dispose()
        {
            _Hotkey.command.Executed -= Command_Executed;
            _Hotkey.command.Updated -= Command_Updated;
            base.Dispose();
        }

        public string Glyph => Hotkey.command.Glyph;
        public string LiveGlyph => Hotkey.command.LiveGlyph;
        public string LiveName => CanCustom ? CustomName : Hotkey.command.LiveName;
        public string FontFamily => Hotkey.command.FontFamily;

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
                    Hotkey.command = Activator.CreateInstance(typeToCreate) as ICommands;

                    // reset custom name
                    CustomName = Hotkey.command.Name;

                    OnPropertyChanged(nameof(FunctionIndex));
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        private string _Chord = "Press to define hotkey input";
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
            }
        }

        public bool ExecutableRunAs
        {
            get
            {
                if (Hotkey.command is ExecutableCommands executableCommand)
                    return executableCommand.RunAs;
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

        public HotkeyViewModel(Hotkey hotkey)
        {
            Hotkey = hotkey;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ButtonGlyphs, new object());

            _functionTypes = FunctionCommands.Functions.Where(item => item is Type type && type.IsAssignableTo(typeof(FunctionCommands))).Cast<Type>().ToList();
            UIHelper.TryInvoke(() =>
            {
                FunctionCollectionView = new ListCollectionView(_functionItems);
                FunctionCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
                // Fill initial data
                string currentCategory = "Ungrouped";
                foreach (object value in FunctionCommands.Functions)
                {
                    if (value is string)
                    {
                        currentCategory = Convert.ToString(value);
                    }
                    else
                    {
                        Type function = value as Type;
                        if (function == typeof(Separator))
                        {
                            _functionItems.Add(new ComboBoxItemViewModel(string.Empty, false, string.Empty));
                        }
                        else
                        {
                            var instance = Activator.CreateInstance(function);
                            ICommands command = (ICommands)instance!;
                            IDisposable? disposable = instance as IDisposable;

                            bool canUnpin = command.CanUnpin;
                            bool isSupported = command.deviceType is null || (command.deviceType == IDevice.GetCurrent().GetType());
                            bool isEnabled = canUnpin && isSupported;

                            _functionItems.Add(new ComboBoxItemViewModel(command.Name, isEnabled, currentCategory));

                            disposable?.Dispose();
                        }
                    }
                }
            });

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
                ManagerFactory.hotkeysManager.DeleteHotkey(Hotkey);
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
        }

        public void DrawChords()
        {
            foreach (FontIconViewModel FontIconViewModel in ButtonGlyphs.ToList())
                ButtonGlyphs.SafeRemove(FontIconViewModel);

            IController controller = ControllerManager.GetTargetOrDefault();

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                foreach (ButtonFlags buttonFlags in Hotkey.inputsChord.ButtonState.Buttons)
                {
                    string glyphString = string.Empty;

                    var color = controller.GetGlyphColor(buttonFlags);
                    Brush? glyphColor = color.HasValue ? new SolidColorBrush(color.Value) : null;

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
                            break;
                        default:
                            glyphString = controller.GetGlyph(buttonFlags);
                            break;
                    }

                    ButtonGlyphs.SafeAdd(new(Hotkey, this, glyphString, glyphColor));
                }
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
        }

        private void DrawNameAndDescription()
        {
            if (Hotkey.command.commandType == CommandType.Function)
            {
                // do something
            }
            else if (Hotkey.command.commandType == CommandType.Executable)
            {
                if (Hotkey.command is ExecutableCommands executableCommands)
                {
                    if (File.Exists(executableCommands.Path))
                    {
                        Dictionary<string, string> AppProperties = ProcessUtils.GetAppProperties(executableCommands.Path);
                        string ProductName = AppProperties.TryGetValue("FileDescription", out var property) ? property : AppProperties["ItemFolderNameDisplay"];
                        string Executable = System.IO.Path.GetFileName(executableCommands.Path);
                        Name = string.IsNullOrEmpty(ProductName) ? Executable : ProductName;
                    }
                    else
                    {
                        Name = Hotkey.command.Name;
                    }

                    Description = Hotkey.command.Description;

                    goto Success;
                }
            }
            else if (Hotkey.command.commandType == CommandType.Keyboard)
            {
                // do something
            }

            Name = string.IsNullOrEmpty(CustomName) ? Hotkey.command.Name : CustomName;
            Description = Hotkey.command.Description;

        Success:
            OnPropertyChanged(nameof(FontFamily));
            OnPropertyChanged(nameof(Glyph));
        }
    }
}
