using HandheldCompanion.Misc;
using System;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public class LayoutTemplateViewModel : BaseViewModel
    {
        private LayoutTemplate _layoutTemplate;
        public LayoutTemplate LayoutTemplate
        {
            get => _layoutTemplate;
            set
            {
                _layoutTemplate = value;
                OnPropertyChanged(nameof(LayoutTemplate));
            }
        }

        public bool IsHeader { get; set; } = false;

        private Guid _guid = Guid.Empty;
        public Guid Guid
        {
            get => _layoutTemplate is not null ? _layoutTemplate.Guid : _guid;
            set
            {
                _guid = value;
                OnPropertyChanged(nameof(Guid));
            }
        }

        private Type _controllerType;
        public Type ControllerType
        {
            get => _layoutTemplate is not null ? _layoutTemplate.ControllerType : _controllerType;
            set
            {
                _controllerType = value;
                OnPropertyChanged(nameof(ControllerType));
            }
        }

        private string _name;
        public string Name
        {
            get => _layoutTemplate is not null ? _layoutTemplate.Name : _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
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
                _visibility = value;
                OnPropertyChanged(nameof(Visibility));
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
