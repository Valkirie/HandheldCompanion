using HandheldCompanion.Controllers;
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
    public class TriggerStackViewModel : StackViewModel
    {
        public ObservableCollection<TriggerMappingViewModel> TriggerMappings { get; private set; } = [];

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
        public new int ActionNumber => TriggerMappings.Count();

        public TriggerStackViewModel(AxisLayoutFlags flag) : base(flag)
        {
            _flag = flag;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(TriggerMappings, new object());

            TriggerMappings.Add(new TriggerMappingViewModel(this, flag));
            TriggerMappings.CollectionChanged += TriggerMappings_CollectionChanged;

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
        }

        private void TriggerMappings_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Notify that ActionNumber has changed
            OnPropertyChanged(nameof(ActionNumber));
        }

        protected override void UpdateController(IController controller)
        {
            IsSupported = controller.HasSourceAxis(_flag);
            UpdateIcon(controller.GetGlyphIconInfo(_flag, 28));
        }

        public override void Dispose()
        {
            TriggerMappings.CollectionChanged -= TriggerMappings_CollectionChanged;
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
            ControllerManager.ControllerSelected -= UpdateController;

            foreach (var buttonMapping in TriggerMappings)
            {
                buttonMapping.Dispose();
            }

            TriggerMappings.Clear();

            base.Dispose();
        }

        public override void AddMapping()
        {
            TriggerMappings.SafeAdd(new TriggerMappingViewModel(this, _flag));
        }

        public override void RemoveMapping(MappingViewModel mapping)
        {
            TriggerMappings.SafeRemove((TriggerMappingViewModel)mapping);
            mapping.Dispose();
        }

        public void UpdateFromMapping()
        {
            var actions = TriggerMappings.Where(b => b.Action is not null)
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
                foreach (var mapping in TriggerMappings)
                    mapping.Dispose();

                var newMappings = new List<TriggerMappingViewModel>();
                foreach (var action in actions.OrderBy(a => a.ShiftSlot))
                {
                    var newMapping = new TriggerMappingViewModel(this, _flag);
                    newMappings.Add(newMapping);

                    // Model update should not go through as on update the entire stack is being recreated
                    // If updateToModel is true, ButtonMappings will end up empty 
                    //      => UpdateFromMapping() => UpdateMapping(layout) => UpdateFromMapping()
                    newMapping.SetAction(action, false);
                }

                TriggerMappings.ReplaceWith(newMappings);
            }
            else if (TriggerMappings.Count != 0)
            {
                foreach (var mapping in TriggerMappings)
                    mapping.Dispose();
                TriggerMappings.Clear();
            }
        }
    }
}
