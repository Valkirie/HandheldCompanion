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
    public class ButtonStackViewModel : StackViewModel
    {
        public static readonly HashSet<ButtonFlags> OEM = [ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5, ButtonFlags.OEM6, ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10];

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
        public new int ActionNumber => ButtonMappings.Count();

        public ButtonStackViewModel(ButtonFlags flag) : base(flag)
        {
            _flag = flag;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ButtonMappings, new object());

            ButtonMappings.Add(new ButtonMappingViewModel(this, flag));
            ButtonMappings.CollectionChanged += ButtonMappings_CollectionChanged;

            ButtonCommand = new DelegateCommand(() =>
            {
                AddMapping();
            });

            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            ControllerManager.ControllerSelected += UpdateController;

            // send events
            if (ControllerManager.HasTargetController)
                UpdateController(ControllerManager.GetTarget());

            if (OEM.Contains(_flag))
            {
                IsSupported = true;
                UpdateIcon(IDevice.GetCurrent().GetGlyphIconInfo(_flag, 28));
            }
        }

        private void ButtonMappings_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Notify that ActionNumber has changed
            OnPropertyChanged(nameof(ActionNumber));
        }

        protected override void UpdateController(IController controller)
        {
            if (OEM.Contains(_flag))
                return;

            IsSupported = controller.HasSourceButton(_flag);
            UpdateIcon(controller.GetGlyphIconInfo(_flag, 28));
        }

        public override void Dispose()
        {
            ButtonMappings.CollectionChanged -= ButtonMappings_CollectionChanged;
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
            ControllerManager.ControllerSelected -= UpdateController;

            foreach (var buttonMapping in ButtonMappings)
                buttonMapping.Dispose();

            ButtonMappings.Clear();

            base.Dispose();
        }

        public override void AddMapping()
        {
            ButtonMappings.SafeAdd(new ButtonMappingViewModel(this, _flag));
        }

        public override void RemoveMapping(MappingViewModel mapping)
        {
            ButtonMappings.SafeRemove((ButtonMappingViewModel)mapping);
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
                    var newMapping = new ButtonMappingViewModel(this, _flag);
                    newMappings.Add(newMapping);

                    // Model update should not go through as on update the entire stack is being recreated
                    // If updateToModel is true, ButtonMappings will end up empty 
                    //      => UpdateFromMapping() => UpdateMapping(layout) => UpdateFromMapping()
                    newMapping.SetAction(action, false);
                }

                ButtonMappings.ReplaceWith(newMappings);
            }
            else if (ButtonMappings.Count != 0)
            {
                foreach (var mapping in ButtonMappings)
                    mapping.Dispose();
                ButtonMappings.Clear();
            }
        }
    }
}
