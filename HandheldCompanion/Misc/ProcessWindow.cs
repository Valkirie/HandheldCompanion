using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class ProcessWindow : IDisposable
    {
        private AutomationPropertyChangedEventHandler propertyHandle;
        private AutomationEventHandler eventHandler;

        public event EventHandler Refreshed;
        public event EventHandler Closed;
        public event EventHandler Disposed;

        public AutomationElement Element { get; private set; }
        public readonly int Hwnd;
        private bool _disposed = false;

        public ProcessEx processEx;
        public ProcessWindowSettings windowSettings = new();

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                if (!value.Equals(_Name))
                {
                    _Name = value;
                    Refreshed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ProcessWindow(ProcessEx processEx, AutomationElement element, bool isPrimary)
        {
            this.processEx = processEx;
            this.Hwnd = element.Current.NativeWindowHandle;
            this.Element = element;
            this.Name = element.Current.Name;

            this.propertyHandle = new(OnPropertyChanged);
            this.eventHandler = new(OnClosed);

            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out object patternObj))
            {
                Automation.AddAutomationPropertyChangedEventHandler(
                    Element,
                    TreeScope.Element,
                    propertyHandle,
                    AutomationElement.NameProperty,
                    AutomationElement.BoundingRectangleProperty);

                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowClosedEvent,
                    Element,
                    TreeScope.Element,
                    eventHandler);
            }

            RefreshName();

            if (string.IsNullOrEmpty(this.Name))
                return;

            // store window settings
            windowSettings = WindowManager.GetWindowSettings(processEx.Path, this.Name, this.Hwnd);
            WindowManager.ApplySettings(this);
        }

        private void OnClosed(object sender, AutomationEventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        ~ProcessWindow()
        {
            Dispose(false);
        }

        private void OnPropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            try
            {
                if (Element != null)
                {
                    if (e.Property == AutomationElement.NameProperty)
                    {
                        RefreshName();
                    }
                    else if (e.Property == AutomationElement.BoundingRectangleProperty)
                    {
                        Refreshed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch { }
        }

        public void RefreshName()
        {
            if (_disposed) return;

            try
            {
                string title = ProcessUtils.GetWindowTitle(Hwnd);
                if (!string.IsNullOrEmpty(title))
                    Name = title;
            }
            catch (COMException)
            {
                Dispose();
            }
            catch
            { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    if (Element != null)
                    {
                        if (propertyHandle is not null)
                            ProcessUtils.TaskWithTimeout(() =>
                            Automation.RemoveAutomationPropertyChangedEventHandler(Element, propertyHandle),
                            TimeSpan.FromSeconds(3));

                        if (eventHandler is not null)
                            ProcessUtils.TaskWithTimeout(() =>
                            Automation.RemoveAutomationEventHandler(
                            WindowPattern.WindowClosedEvent,
                            Element,
                            eventHandler),
                            TimeSpan.FromSeconds(3));
                    }
                }
                catch { }
            }

            // Clear references and mark as disposed
            Element = null;
            propertyHandle = null;
            _disposed = true;

            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
