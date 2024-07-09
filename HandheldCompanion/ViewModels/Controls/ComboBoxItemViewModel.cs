using System;

namespace HandheldCompanion.ViewModels.Controls
{
    public class ComboBoxItemViewModel : BaseViewModel
    {
        private string _Text = string.Empty;
        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                _Text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        private bool _IsSeparator = false;
        public bool IsSeparator
        {
            get
            {
                return _IsSeparator;
            }
            set
            {
                _IsSeparator = value;
                OnPropertyChanged(nameof(IsSeparator));
            }
        }

        private bool _IsEnabled = true;
        public bool IsEnabled
        {
            get
            {
                return _IsEnabled;
            }
            set
            {
                _IsEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public ComboBoxItemViewModel(string Text, bool IsEnabled)
        {
            if (string.IsNullOrEmpty(Text))
                this.IsSeparator = true;
            else
                this.Text = Text;

            this.IsEnabled = IsEnabled;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
