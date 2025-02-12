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
    public class AxisStackViewModel : BaseViewModel
    {
        public ObservableCollection<AxisMappingViewModel> AxisMappings { get; private set; } = [];

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

        private AxisLayoutFlags _flag;

        public AxisStackViewModel(AxisLayoutFlags flag)
        {
            _flag = flag;
            AxisMappings.Add(new AxisMappingViewModel(this, flag, isInitialMapping: true));

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

            foreach (var buttonMapping in AxisMappings)
            {
                buttonMapping.Dispose();
            }

            AxisMappings.Clear();

            base.Dispose();
        }



        public void AddMapping()
        {
            AxisMappings.SafeAdd(new AxisMappingViewModel(this, _flag));
        }

        public void RemoveMapping(AxisMappingViewModel mapping)
        {
            AxisMappings.SafeRemove(mapping);
            mapping.Dispose();
        }

        public void UpdateFromMapping()
        {
            var actions = AxisMappings.Where(b => b.Action is not null)
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
            if (layout.AxisLayout.TryGetValue(_flag, out var actions))
            {
                foreach (var mapping in AxisMappings)
                    mapping.Dispose();

                var newMappings = new List<AxisMappingViewModel>();
                foreach (var action in actions.OrderBy(a => a.ShiftSlot))
                {
                    var newMapping = new AxisMappingViewModel(this, _flag, isInitialMapping: newMappings.Count == 0);
                    newMappings.Add(newMapping);

                    // Model update should not go through as on update the entire stack is being recreated
                    // If updateToModel is true, ButtonMappings will end up empty 
                    //      => UpdateFromMapping() => UpdateMapping(layout) => UpdateFromMapping()
                    newMapping.SetAction(action, false);
                }

                AxisMappings.ReplaceWith(newMappings);
            }
            else
            {
                foreach (var mapping in AxisMappings)
                    mapping.Dispose();

                AxisMappings.ReplaceWith([new AxisMappingViewModel(this, _flag, isInitialMapping: true)]);
            }
        }

        private void UpdateController(IController controller)
        {
            IsSupported = controller.HasSourceAxis(_flag);
        }
    }
}
