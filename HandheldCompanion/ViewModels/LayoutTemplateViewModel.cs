using HandheldCompanion.Misc;
using HandheldCompanion.Properties;

using System;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public class LayoutTemplateViewModel : BaseViewModel
    {
        public string Header => LayoutTemplate.IsInternal ? Resources.LayoutPage_Templates : Resources.LayoutPage_Community;

        private LayoutTemplate _layoutTemplate = null!;
        public LayoutTemplate LayoutTemplate
        {
            get => _layoutTemplate;
            set
            {
                _layoutTemplate = value;
                OnPropertyChanged(nameof(LayoutTemplate));
                OnPropertyChanged(nameof(Header));
            }
        }

        private Guid _guid = Guid.Empty;
        public Guid Guid
        {
            get => _layoutTemplate is not null ? _layoutTemplate.Guid : _guid;
            set
            {
                if (_guid != value)
                {
                    _guid = value;
                    OnPropertyChanged(nameof(Guid));
                }
            }
        }

        private string? _deviceName = null;
        public string? DeviceName
        {
            get => _layoutTemplate is not null ? _layoutTemplate.DeviceName : _deviceName;
            set
            {
                if (_deviceName != value)
                {
                    _deviceName = value;
                    OnPropertyChanged(nameof(DeviceName));
                }
            }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _layoutTemplate is not null ? _layoutTemplate.Name : _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Description => _layoutTemplate is not null ? _layoutTemplate.Description : string.Empty;
        public string Author => _layoutTemplate is not null ? _layoutTemplate.Author : string.Empty;
        public string Product => _layoutTemplate is not null ? _layoutTemplate.Product : string.Empty;

        private Visibility _visibility;
        public Visibility Visibility
        {
            get => _visibility;
            set
            {
                if (_visibility != value)
                {
                    _visibility = value;
                    OnPropertyChanged(nameof(Visibility));
                }
            }
        }

        public LayoutTemplateViewModel()
        {
        }

        public LayoutTemplateViewModel(LayoutTemplate layoutTemplate)
        {
            LayoutTemplate = layoutTemplate;
        }
    }
}
