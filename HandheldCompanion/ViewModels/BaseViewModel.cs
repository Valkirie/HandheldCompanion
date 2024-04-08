using System;
using System.ComponentModel;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    class DelegateCommand : ICommand
    {
        private Action _action;
        public DelegateCommand(Action action)
        {
            this._action = action;
        }
        public bool CanExecute(object parameter)
        {
            return true;
        }
        public void Execute(object parameter)
        {
            _action();
        }
        public event EventHandler CanExecuteChanged;
        public void OnCanExecuteChanged()
        {
            CanExecuteChanged(this, EventArgs.Empty);
        }
    }
}
