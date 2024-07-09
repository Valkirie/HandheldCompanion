using ABI.System.Collections.Generic;
using HandheldCompanion.Commands;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Xml;
using WindowsInput.Events;
using static HandheldCompanion.Commands.ICommands;
using static PInvoke.Kernel32;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HandheldCompanion.ViewModels
{
    public class HotkeyViewModel : BaseViewModel
    {
        public ObservableCollection<GlyphViewModel> ButtonGlyphs { get; set; } = [];
        public ObservableCollection<ComboBoxItemViewModel> FunctionItems { get; set; } = [];

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

                OnPropertyChanged(nameof(Hotkey));
                OnPropertyChanged(nameof(IsPinned));
                OnPropertyChanged(nameof(CommandTypeIndex));

                if (Hotkey.command is FunctionCommands functionCommands)
                    OnPropertyChanged(nameof(FunctionIndex));

                if (Hotkey.command is ExecutableCommands executableCommands)
                    OnPropertyChanged(nameof(ExecutablePath));

                DrawChords();
                DrawNameAndDescription();
            }
        }

        private void Command_Executed(ICommands command)
        {
            OnPropertyChanged(nameof(IsToggled));
        }

        public override void Dispose()
        {
            _Hotkey.command.Executed -= Command_Executed;
            base.Dispose();
        }

        public string Glyph => Hotkey.command.Glyph;

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
                    HotkeysManager.UpdateOrCreateHotkey(Hotkey);
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

        private bool _IsListening = false;
        public bool IsListening => _IsListening;

        public void SetListening(bool listening)
        {
            _IsListening = listening;
            OnPropertyChanged(nameof(IsListening));
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
                return _KeyboardOutputChord;
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

        private void DrawChords()
        {
            foreach (GlyphViewModel glyphViewModel in ButtonGlyphs.ToList())
                ButtonGlyphs.SafeRemove(glyphViewModel);

            IController? controller = ControllerManager.GetTargetController();
            if (controller is null)
                controller = ControllerManager.GetEmulatedController();

            foreach (ButtonFlags buttonFlags in Hotkey.inputsChord.ButtonState.Buttons)
            {
                string glyphString = string.Empty;

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

                ButtonGlyphs.SafeAdd(new(Hotkey, this, glyphString));
            }

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
            switch (Hotkey.command.commandType)
            {
                case CommandType.Executable:
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
                        }
                    }
                    break;

                default:
                    Name = string.IsNullOrEmpty(CustomName) ? Hotkey.command.Name : CustomName;
                    Description = Hotkey.command.Description;
                    break;
            }

            OnPropertyChanged(nameof(Glyph));
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
                    // clear custom name
                    CustomName = string.Empty;

                    switch ((CommandType)value)
                    {
                        case CommandType.None:
                            Hotkey.command = new EmptyCommands();
                            break;
                        case CommandType.Function:
                            // pick first available function command
                            int index = FunctionCommands.Functions.FindIndex(item => item is Type);
                            FunctionIndex = index;
                            break;
                        case CommandType.Keyboard:
                            Hotkey.command = new KeyboardCommands();
                            break;
                        case CommandType.Executable:
                            Hotkey.command = new ExecutableCommands();
                            break;
                    }

                    OnPropertyChanged(nameof(CommandTypeIndex));
                    HotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        public int FunctionIndex
        {
            get
            {
                Type typeToSearch = Hotkey.command.GetType();
                if (FunctionCommands.Functions.Contains(typeToSearch))
                    return FunctionCommands.Functions.IndexOf(typeToSearch);
                else
                    return 0;
            }
            set
            {
                if (value != FunctionIndex)
                {
                    // clear custom name
                    CustomName = string.Empty;

                    Type typeToCreate = (Type)FunctionCommands.Functions[value];
                    Hotkey.command = Activator.CreateInstance(typeToCreate) as ICommands;

                    OnPropertyChanged(nameof(FunctionIndex));
                    HotkeysManager.UpdateOrCreateHotkey(Hotkey);
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
                    executableCommand.Arguments = value;
                    HotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            }
        }

        public bool IsToggled => Hotkey.command.IsToggled;

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

            // Fill initial data
            foreach (object value in FunctionCommands.Functions)
            {
                if (value is string)
                {
                    string category = Convert.ToString(value);
                    FunctionItems.SafeAdd(new ComboBoxItemViewModel(category, false));
                }
                else
                {
                    Type function = value as Type;
                    if (function == typeof(Separator))
                    {
                        FunctionItems.SafeAdd(new ComboBoxItemViewModel(string.Empty, false));
                    }
                    else
                    {
                        ICommands command = Activator.CreateInstance(function) as ICommands;
                        FunctionItems.SafeAdd(new ComboBoxItemViewModel(command.Name, true));
                    }
                }
            }

            DefineButtonCommand = new DelegateCommand(async () =>
            {
                InputsManager.StartListening(hotkey.ButtonFlags, InputsChordTarget.Input);
            });

            PinButtonCommand = new DelegateCommand(async () =>
            {
                Hotkey.IsPinned = !Hotkey.IsPinned;
                HotkeysManager.UpdateOrCreateHotkey(Hotkey);
            });

            DeleteHotkeyCommand = new DelegateCommand(async () =>
            {
                HotkeysManager.DeleteHotkey(Hotkey);
            });

            DefineOutputCommand = new DelegateCommand(async () =>
            {
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
                        HotkeysManager.UpdateOrCreateHotkey(Hotkey);
                    }
                }
            });

            ExecuteCommand = new DelegateCommand(async () =>
            {
                if (Hotkey.command is not null)
                    Hotkey.command.Execute(Hotkey.command.OnKeyDown, Hotkey.command.OnKeyUp);
            });

            EraseButtonCommand = new DelegateCommand(async () =>
            {
                Hotkey.inputsChord = new();
                HotkeysManager.UpdateOrCreateHotkey(Hotkey);
            });

            EraseOutputButtonCommand = new DelegateCommand(async () =>
            {
                if (Hotkey.command is KeyboardCommands keyboardCommands)
                {
                    keyboardCommands.outputChord = new();
                    HotkeysManager.UpdateOrCreateHotkey(Hotkey);
                }
            });
        }
    }
}
