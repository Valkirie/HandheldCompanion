using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    // ViewModel used to fill Target ComboBox on Mappings
    public class MappingTargetViewModel : BaseViewModel
    {
        public object Tag { get; set; }
        public string Content { get; set; }
    }

    public abstract class MappingViewModel : BaseViewModel
    {
        public static readonly HashSet<ButtonFlags> OEM = [ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5, ButtonFlags.OEM6, ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10];

        protected object Value { get; set; }
        public IActions? Action { get; protected set; }

        public int ActionTypeIndex
        {
            get => Action is not null ? (int) Action.actionType : 0;
            set
            {
                if (value != ActionTypeIndex)
                {
                    if (Action is not null)
                        Action.actionType = (ActionType) value;

                    ActionTypeChanged((ActionType) value);
                }
            }
        }

        public ObservableCollection<MappingTargetViewModel> Targets { get; set; } = [];

        private MappingTargetViewModel? _selectedTarget;
        public MappingTargetViewModel? SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (value != SelectedTarget)
                {
                    _selectedTarget = value;
                    TargetTypeChanged();
                    OnPropertyChanged(nameof(SelectedTarget));
                }
            }
        }


        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (value != Name)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string _glyph;
        public string Glyph
        {
            get => _glyph;
            set
            {
                if (value != Glyph)
                {
                    _glyph = value;
                    OnPropertyChanged(nameof(Glyph));
                }
            }
        }

        private FontFamily? _glyphFontFamily;
        public FontFamily? GlyphFontFamily
        {
            get => _glyphFontFamily;
            set
            {
                if (value != GlyphFontFamily)
                {
                    _glyphFontFamily = value;
                    OnPropertyChanged(nameof(GlyphFontFamily));
                }
            }
        }

        private double _glyphFontSize = 14;
        public double GlyphFontSize
        {
            get => _glyphFontSize;
            set
            {
                if (value != GlyphFontSize)
                {
                    _glyphFontSize = value;
                    OnPropertyChanged(nameof(GlyphFontSize));
                }
            }
        }

        private Brush? _glyphForeground;
        public Brush? GlyphForeground
        {
            get => _glyphForeground;
            set
            {
                if (value != GlyphForeground)
                {
                    _glyphForeground = value;
                    OnPropertyChanged(nameof(GlyphForeground));
                }
            }
        }

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

        // Purely UI related properties, they should NOT update the Layout
        // Avoid unnecessary save/update calls
        protected HashSet<string> ExcludedUpdateProperties = 
        [
            nameof(Name),
            nameof(Glyph),
            nameof(GlyphFontFamily),
            nameof(GlyphFontSize),
            nameof(GlyphForeground),
            nameof(IsSupported),
        ];

        // Property to block off updating to model in certain cases
        protected bool _updateToModel = true;

        public MappingViewModel(object value)
        {
            Value = value;

            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;

            ControllerManager.ControllerSelected += UpdateController;
            DeviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceUpdated;
            DeviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceUpdated;
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

            // Send update event to Model
            PropertyChanged +=
                (s, e) =>
                {
                    if (_updateToModel && e.PropertyName is not null && !ExcludedUpdateProperties.Contains(e.PropertyName))
                        Update();
                };
        }

        public override void Dispose()
        {
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;

            ControllerManager.ControllerSelected -= UpdateController;
            DeviceManager.UsbDeviceArrived -= DeviceManager_UsbDeviceUpdated;
            DeviceManager.UsbDeviceRemoved -= DeviceManager_UsbDeviceUpdated;
            VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;

            base.Dispose();
        }

        private void VirtualManager_ControllerSelected(HIDmode hid) => ActionTypeChanged();

        private void DeviceManager_UsbDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
        {
            IController controller = ControllerManager.GetTargetController();
            if (controller is not null) UpdateController(controller);
        }

        protected void UpdateIcon(GlyphIconInfo glyphIconInfo)
        {
            Name = glyphIconInfo.Name!;
            Glyph = glyphIconInfo.Glyph!;
            GlyphFontFamily = glyphIconInfo.FontFamily;
            GlyphFontSize = glyphIconInfo.FontSize;
            GlyphForeground = glyphIconInfo.Foreground;
        }

        protected abstract void UpdateController(IController controller);
        protected abstract void ActionTypeChanged(ActionType? newActionType = null);
        protected abstract void TargetTypeChanged();
        protected abstract void Update();
        protected abstract void Delete();
        protected abstract void UpdateMapping(Layout layout);


        public virtual void SetAction(IActions newAction, bool updateToModel = true)
        {
            _selectedTarget = null;
            Action = newAction;

            _updateToModel = updateToModel;
            
            ActionTypeChanged(); // Includes full UI update

            // Reset update to model
            _updateToModel = true;
        }

        public virtual void Reset()
        {
            ActionTypeIndex = 0;
            SelectedTarget = null;
        }
    }
}
