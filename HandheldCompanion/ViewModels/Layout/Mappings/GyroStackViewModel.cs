using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
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
            BindingOperations.EnableCollectionSynchronization(GyroMappings, _collectionLock);

            GyroMappings.Add(new GyroMappingViewModel(flag));
        }

        protected override void UpdateController(IController controller)
        {
            IsSupported = controller.HasSourceAxis(_flag) || IDevice.GetCurrent().HasMotionSensor();
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
                foreach (var buttonMapping in GyroMappings)
                    buttonMapping.Dispose();

                GyroMappings.Clear();
            }

            base.Dispose(disposing);
        }

        public override void AddMapping()
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                GyroMappings.Add(new GyroMappingViewModel(_flag));
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        public override void RemoveMapping(MappingViewModel mapping)
        {
            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                GyroMappings.Remove((GyroMappingViewModel)mapping);
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
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

        protected override void UpdateMapping(Layout layout)
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

                if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                    return;

                try
                {
                    GyroMappings.Clear();
                    foreach (var m in newMappings)
                        GyroMappings.Add(m);
                }
                finally
                {
                    Monitor.Exit(_collectionLock);
                }
            }
        }
    }
}
