using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class ButtonStackViewModel : BaseViewModel
    {
        public ObservableCollection<ButtonMappingViewModel> ButtonMappings { get; private set; } = [];

        private bool _isSupported;
        public bool IsSupported
        {
            get => _isSupported;
            set
            {
                if (value != _isSupported)
                {
                    _isSupported = value;
                    OnPropertyChanged(nameof(IsSupported));
                }
            }
        }


        private ButtonFlags _flag;

        public ButtonStackViewModel(ButtonFlags flag)
        {
            _flag = flag;
            ButtonMappings.Add(new ButtonMappingViewModel(this, flag, isInitialMapping: true));

            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            ControllerManager.ControllerSelected += UpdateController;

            // send events
            if (ControllerManager.HasTargetController)
            {
                UpdateController(ControllerManager.GetTarget());
            }
        }

        public override void Dispose()
        {
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
            ControllerManager.ControllerSelected -= UpdateController;

            foreach (var buttonMapping in ButtonMappings)
            {
                buttonMapping.Dispose();
            }

            ButtonMappings.Clear();

            base.Dispose();
        }



        public void AddMapping()
        {
            ButtonMappings.SafeAdd(new ButtonMappingViewModel(this, _flag));
        }

        public void RemoveMapping(ButtonMappingViewModel mapping)
        {
            ButtonMappings.SafeRemove(mapping);
            mapping.Dispose();
        }

        public void UpdateFromMapping()
        {
            var actions = ButtonMappings.Where(b => b.Action is not null)
                                        .Select(b => b.Action!).ToList();

            if (actions.Count > 0)
            {
                MainWindow.layoutPage.CurrentLayout.UpdateLayout(_flag, actions);
            }
            else
            {
                MainWindow.layoutPage.CurrentLayout.RemoveLayout(_flag);
            }
        }

        private void UpdateMapping(Layout layout)
        {
            if (layout.ButtonLayout.TryGetValue(_flag, out var actions))
            {
                foreach (var mapping in ButtonMappings)
                    mapping.Dispose();

                var newMappings = new List<ButtonMappingViewModel>();
                foreach (var action in actions.OrderBy(a => a.ShiftSlot))
                {
                    var newMapping = new ButtonMappingViewModel(this, _flag, isInitialMapping: newMappings.Count == 0);
                    newMappings.Add(newMapping);

                    // Model update should not go through as on update the entire stack is being recreated
                    // If updateToModel is true, ButtonMappings will end up empty 
                    //      => UpdateFromMapping() => UpdateMapping(layout) => UpdateFromMapping()
                    newMapping.SetAction(action, false);
                }

                ButtonMappings.ReplaceWith(newMappings);
            }
            else
            {
                foreach (var mapping in ButtonMappings)
                    mapping.Dispose();

                ButtonMappings.ReplaceWith([new ButtonMappingViewModel(this, _flag, isInitialMapping: true)]);
            }
        }

        private void UpdateController(IController controller)
        {
            IsSupported = controller.HasSourceButton(_flag) || MappingViewModel.OEM.Contains(_flag);
        }
    }
}
