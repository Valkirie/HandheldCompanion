using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class ButtonMappingViewModel : MappingViewModel
    {
        private static List<MappingTargetViewModel> _keyboardKeysTargets = [];

        private static HashSet<MouseActionsType> _unsupportedMouseActionTypes =
        [
            MouseActionsType.Move,
            MouseActionsType.Scroll,
        ];

        #region Mapping Properties

        private int _pressTypeFallbackIndex = 0;
        public int PressTypeIndex
        {
            get => Action is not null ? (int)Action.pressType : 0;
            set
            {
                // In case press type is changed when there's no action yet
                // Keep track of value to set PressTypeIndex when action is created
                _pressTypeFallbackIndex = value;

                if (Action is not null && value != PressTypeIndex)
                {
                    Action.pressType = (PressType)value;
                    OnPropertyChanged(nameof(PressTypeIndex));
                }
            }
        }

        public int LongPressDelay
        {
            get => Action is not null ? Action.ActionTimer : 0;
            set
            {
                if (Action is not null && value != LongPressDelay)
                {
                    Action.ActionTimer = value;
                    OnPropertyChanged(nameof(LongPressDelay));
                }
            }
        }

        public int ModifierIndex
        {
            get
            {
                if (Action is KeyboardActions keyboardAction)
                    return (int)keyboardAction.Modifiers;

                if (Action is MouseActions mouseAction)
                    return (int)mouseAction.Modifiers;

                return 0;
            }

            set
            {
                if (Action is not null && value != ModifierIndex)
                {
                    if (Action is KeyboardActions keyboardAction)
                        keyboardAction.Modifiers = (ModifierSet)value;

                    else if (Action is MouseActions mouseAction)
                        mouseAction.Modifiers = (ModifierSet)value;

                    OnPropertyChanged(nameof(ModifierIndex));
                }
            }
        }

        public bool Turbo
        {
            get => Action is not null ? Action.Turbo : false;
            set
            {
                if (Action is not null && value != Turbo)
                {
                    Action.Turbo = value;
                    OnPropertyChanged(nameof(Turbo));
                }
            }
        }

        public int TurboDelay
        {
            get => Action is not null ? Action.TurboDelay : 0;
            set
            {
                if (Action is not null && value != TurboDelay)
                {
                    Action.TurboDelay = value;
                    OnPropertyChanged(nameof(TurboDelay));
                }
            }
        }

        public bool Toggle
        {
            get => Action is not null ? Action.Toggle : false;
            set
            {
                if (Action is not null && value != Toggle)
                {
                    Action.Toggle = value;
                    OnPropertyChanged(nameof(Toggle));
                }
            }
        }

        public int HapticModeIndex
        {
            get => Action is not null ? (int)Action.HapticMode : 0;
            set
            {
                if (Action is not null && value != HapticModeIndex)
                {
                    Action.HapticMode = (HapticMode)value;
                    OnPropertyChanged(nameof(HapticModeIndex));
                }
            }
        }

        public int HapticStrengthIndex
        {
            get => Action is not null ? (int)Action.HapticStrength : 0;
            set
            {
                if (Action is not null && value != HapticStrengthIndex)
                {
                    Action.HapticStrength = (HapticStrength)value;
                    OnPropertyChanged(nameof(HapticStrengthIndex));
                }
            }
        }

        public bool Interruptable
        {
            get => Action is not null ? Action.Interruptable : false;
            set
            {
                if (Action is not null && value != Interruptable)
                {
                    Action.Interruptable = value;
                    OnPropertyChanged(nameof(Interruptable));
                }
            }
        }

        #endregion

        private ButtonStackViewModel _parentStack;

        public bool IsInitialMapping { get; private set; } = false;

        public ICommand ButtonCommand { get; private set; }

        public ButtonMappingViewModel(ButtonStackViewModel parentStack, ButtonFlags button, bool isInitialMapping = false) : base(button)
        {
            _parentStack = parentStack;
            IsInitialMapping = isInitialMapping;

            ButtonCommand = new DelegateCommand(() =>
            {
                if (IsInitialMapping)
                    _parentStack.AddMapping();
                else
                {
                    if (Action is not null) Delete();
                    _parentStack.RemoveMapping(this);
                }
            });

            // Lazy initialize to avoid re-creating target for Keyboard targets
            if (_keyboardKeysTargets.Count == 0)
            {
                foreach (KeyFlags key in KeyFlagsOrder.arr)
                {
                    _keyboardKeysTargets.Add(new MappingTargetViewModel
                    {
                        Tag = (VirtualKeyCode)key,
                        Content = EnumUtils.GetDescriptionFromEnumValue(key)
                    });
                }
            }

            if (isInitialMapping)
            {
                var controller = ControllerManager.GetTargetController();
                if (controller is not null) UpdateController(controller);
            }

            if (OEM.Contains(button))
            {
                UpdateIcon(IDevice.GetCurrent().GetGlyphIconInfo(button));
            }
        }

        protected override void UpdateController(IController controller)
        {
            var flag = (ButtonFlags)Value;
            if (OEM.Contains(flag))
                return;

            UpdateIcon(controller.GetGlyphIconInfo(flag));
        }

        protected override void ActionTypeChanged(ActionType? newActionType = null)
        {
            var actionType = newActionType ?? (ActionType)ActionTypeIndex;
            if (actionType == ActionType.Disabled)
            {
                if (Action is not null) Delete();
                SelectedTarget = null;
                OnPropertyChanged(string.Empty);
                return;
            }

            var fallbackPressType = (PressType)_pressTypeFallbackIndex;

            if (actionType == ActionType.Button)
            {
                if (Action is null || Action is not ButtonActions)
                    Action = new ButtonActions() { pressType = fallbackPressType };

                // get current controller
                var controller = ControllerManager.GetEmulatedController();

                // Build Targets
                var targets = new List<MappingTargetViewModel>();

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var button in controller.GetTargetButtons())
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = button,
                        Content = controller.GetButtonName(button)
                    };
                    targets.Add(mappingTargetVm);

                    if (button == ((ButtonActions)Action).Button)
                    {
                        matchingTargetVm = mappingTargetVm;
                    }
                }

                Targets.ReplaceWith(targets);
                if (matchingTargetVm != null) SelectedTarget = matchingTargetVm;
            }
            else if (actionType == ActionType.Keyboard)
            {
                if (Action is null || Action is not KeyboardActions)
                    Action = new KeyboardActions() { pressType = fallbackPressType };

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                    Action = new MouseActions() { pressType = fallbackPressType };

                // Build Targets
                var targets = new List<MappingTargetViewModel>();

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var mouseType in Enum.GetValues<MouseActionsType>().Except(_unsupportedMouseActionTypes))
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = mouseType,
                        Content = EnumUtils.GetDescriptionFromEnumValue(mouseType)
                    };
                    targets.Add(mappingTargetVm);

                    if (mouseType == ((MouseActions)Action).MouseType)
                    {
                        matchingTargetVm = mappingTargetVm;
                    }
                }

                // Update list and selected target
                Targets.ReplaceWith(targets);
                if (matchingTargetVm != null) SelectedTarget = matchingTargetVm;
            }

            // Refresh mapping
            OnPropertyChanged(string.Empty);
        }

        protected override void TargetTypeChanged()
        {
            if (Action is null || SelectedTarget is null)
                return;

            switch (Action.actionType)
            {
                case ActionType.Button:
                    ((ButtonActions)Action).Button = (ButtonFlags)SelectedTarget.Tag;
                    break;

                case ActionType.Keyboard:
                    ((KeyboardActions)Action).Key = (VirtualKeyCode)SelectedTarget.Tag;
                    break;

                case ActionType.Mouse:
                    ((MouseActions)Action).MouseType = (MouseActionsType)SelectedTarget.Tag;
                    break;
            }
        }


        protected override void Update()
        {
            _parentStack.UpdateFromMapping();
        }

        protected override void Delete()
        {
            Action = null;
            _parentStack.UpdateFromMapping();
        }


        // Done from ButtonStack
        protected override void UpdateMapping(Layout layout)
        {
        }
    }
}
