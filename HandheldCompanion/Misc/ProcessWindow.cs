using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class ProcessWindow : IDisposable
    {
        private AutomationPropertyChangedEventHandler? propertyHandle;
        private AutomationEventHandler? eventHandler;

        public event EventHandler? Refreshed;
        public event EventHandler? Closed;
        public event EventHandler? Disposed;

        public AutomationElement? Element { get; private set; }
        public readonly int Hwnd;
        private bool _disposed = false;

        public ProcessEx processEx;
        public ProcessWindowSettings windowSettings = new();

        private string _Name = string.Empty;
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
                    AutomationElement.NameProperty);

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
                        // Run off the UIAutomation callback thread: GetWindowText sends WM_GETTEXT
                        // cross-process, which blocks the calling thread until the target processes
                        // the message. Blocking the UIA STA thread prevents it from pumping incoming
                        // COM messages and can deadlock the target application on close.
                        Task.Run(RefreshName);
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
                string? title = ProcessUtils.GetWindowTitle(Hwnd);
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

            // Mark as disposed first to stop any concurrent callbacks.
            _disposed = true;

            if (disposing)
            {
                // Capture references before clearing them.
                var localElement = Element;
                var localPropertyHandle = propertyHandle;
                var localEventHandler = eventHandler;

                // Release the COM reference to the target window's accessibility provider
                // as early as possible. Holding AutomationElement after the target window
                // is destroyed can block the target process from completing COM cleanup.
                Element = null;
                propertyHandle = null;
                eventHandler = null;

                // Remove event handlers asynchronously (fire-and-forget).
                //
                // IMPORTANT: Do NOT use TaskWithTimeout(.Wait) here. This Dispose may be
                // called from an UIAutomation event callback (OnClosed → Closed → Window_Closed
                // → Dispose). The UIAutomation callback thread is a COM STA thread.
                // Blocking it with a non-message-pumping wait (Task.Wait) prevents the STA
                // from processing incoming COM SendMessage calls. If the target application
                // (e.g. Dolphin) raises a UIAutomation event at the same moment, UIAutomation
                // delivers it via COM SendMessage to this STA thread, which is now blocked.
                // The target's UI thread then stalls waiting for HC to acknowledge — resulting
                // in the target application appearing hung / unable to close.
                if (localElement is not null)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (localPropertyHandle is not null)
                                Automation.RemoveAutomationPropertyChangedEventHandler(localElement, localPropertyHandle);
                        }
                        catch { }

                        try
                        {
                            if (localEventHandler is not null)
                                Automation.RemoveAutomationEventHandler(
                                    WindowPattern.WindowClosedEvent,
                                    localElement,
                                    localEventHandler);
                        }
                        catch { }
                    });
                }
            }

            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
