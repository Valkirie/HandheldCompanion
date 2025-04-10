using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class GyroStackViewModel : StackViewModel
    {
        public ObservableCollection<GyroMappingViewModel> GyroMappings { get; private set; } = [];

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

        public GyroStackViewModel(AxisLayoutFlags flag) : base(flag)
        {
            _flag = flag;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(GyroMappings, new object());

            GyroMappings.Add(new GyroMappingViewModel(flag));

            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            ControllerManager.ControllerSelected += UpdateController;

            // send events
            if (ControllerManager.HasTargetController)
                UpdateController(ControllerManager.GetTarget());
        }

        protected override void UpdateController(IController controller)
        {
            IsSupported = controller.HasSourceAxis(_flag) || IDevice.GetCurrent().HasMotionSensor();
            UpdateIcon(controller.GetGlyphIconInfo(_flag, 28));
        }

        public override void Dispose()
        {
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
            ControllerManager.ControllerSelected -= UpdateController;

            foreach (var buttonMapping in GyroMappings)
            {
                buttonMapping.Dispose();
            }

            GyroMappings.Clear();

            base.Dispose();
        }

        public override void AddMapping()
        {
            GyroMappings.SafeAdd(new GyroMappingViewModel(_flag));
        }

        public override void RemoveMapping(MappingViewModel mapping)
        {
            GyroMappings.SafeRemove((GyroMappingViewModel)mapping);
            mapping.Dispose();
        }

        public void UpdateFromMapping()
        {
            var actions = GyroMappings.Where(b => b.Action is not null)
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
                foreach (var mapping in GyroMappings)
                    mapping.Dispose();

                var newMappings = new List<GyroMappingViewModel>();
                foreach (var action in actions.OrderBy(a => a.ShiftSlot))
                {
                    var newMapping = new GyroMappingViewModel(_flag);
                    newMappings.Add(newMapping);

                    // Model update should not go through as on update the entire stack is being recreated
                    // If updateToModel is true, ButtonMappings will end up empty 
                    //      => UpdateFromMapping() => UpdateMapping(layout) => UpdateFromMapping()
                    newMapping.SetAction(action, false);
                }

                GyroMappings.ReplaceWith(newMappings);
            }
        }
    }
}
