using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace HandheldCompanion.Views
{
    /// <summary>
    /// Interaction logic for SplashScreen.xaml
    /// </summary>
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
        }
    }

    public sealed class SplashScreenHost : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly ManualResetEventSlim _windowReady = new(false);

        private Thread _thread;
        private Dispatcher _dispatcher;
        private SplashScreen _window;

        public void Show()
        {
            lock (_syncRoot)
            {
                if (_dispatcher is not null)
                    return;

                _windowReady.Reset();

                _thread = new Thread(ThreadStart)
                {
                    IsBackground = true,
                    Name = nameof(SplashScreen)
                };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            _windowReady.Wait();
        }

        public void Close()
        {
            SplashScreen window;
            Dispatcher dispatcher;

            lock (_syncRoot)
            {
                window = _window;
                dispatcher = _dispatcher;
                _window = null;
                _dispatcher = null;
                _thread = null;
            }

            if (window is null || dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return;

            dispatcher.BeginInvoke(new Action(window.Close), DispatcherPriority.Normal);
        }

        public void Dispose()
        {
            Close();
            _windowReady.Dispose();
        }

        private void ThreadStart()
        {
            SplashScreen window = new();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            window.Closed += Window_Closed;

            lock (_syncRoot)
            {
                _window = window;
                _dispatcher = dispatcher;
            }

            _windowReady.Set();
            window.Show();

            Dispatcher.Run();
        }

        private static void Window_Closed(object sender, EventArgs e)
        {
            Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }
}
