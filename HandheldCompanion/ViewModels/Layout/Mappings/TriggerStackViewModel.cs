using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
            BindingOperations.EnableCollectionSynchronization(TriggerMappings, _collectionLock);

            TriggerMappings.Add(new TriggerMappingViewModel(this, flag));
            TriggerMappings.CollectionChanged += TriggerMappings_CollectionChanged;

            ButtonCommand = new DelegateCommand(() =>
            {
                var newMapping = AddMappingAndReturn();
                // Navigate to LayoutItemPage with the new mapping
                if (newMapping is not null && MainWindow.layoutItemPage is not null)
                {
                    MainWindow.layoutItemPage.SetMapping(newMapping);
                    MainWindow.NavView_Navigate(MainWindow.layoutItemPage);
                }
            });
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
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TriggerMappings.CollectionChanged -= TriggerMappings_CollectionChanged;

                foreach (var buttonMapping in TriggerMappings)
                    buttonMapping.Dispose();

                TriggerMappings.Clear();
            }

            base.Dispose(disposing);
        }

        public override void AddMapping()
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                TriggerMappings.Add(new TriggerMappingViewModel(this, _flag));
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        public TriggerMappingViewModel? AddMappingAndReturn()
        {
            var newMapping = new TriggerMappingViewModel(this, _flag);
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return null;

            try
            {
                TriggerMappings.Add(newMapping);
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
            return newMapping;
        }

        public override void RemoveMapping(MappingViewModel mapping)
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                TriggerMappings.Remove((TriggerMappingViewModel)mapping);
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
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

        protected override void UpdateMapping(Layout layout)
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

                if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                    return;

                try
                {
                    TriggerMappings.Clear();
                    foreach (var m in newMappings)
                        TriggerMappings.Add(m);
                }
                finally
                {
                    Monitor.Exit(_collectionLock);
                }
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
