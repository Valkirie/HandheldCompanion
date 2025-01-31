using System;
using System.ComponentModel;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool _disposed = false; // Track whether Dispose has been called

        ~BaseViewModel()
        {
            Dispose(false);
        }

        public void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Free any managed resources here
                PropertyChanged = null; // Unsubscribe all event handlers to avoid memory leaks
            }

            // Free unmanaged resources here (if any)
            _disposed = true;
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Suppress finalization
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
