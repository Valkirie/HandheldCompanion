using System;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class WindowElement : IDisposable
    {
        public AutomationElement _automationElement;
        public readonly IntPtr _hwnd;
        public readonly int _processId;

        public event OnWindowClosedEventHandler Closed;
        public delegate void OnWindowClosedEventHandler(WindowElement windowElement);

        public WindowElement(int processId, AutomationElement element)
        {
            _automationElement = element;
            _hwnd = element.Current.NativeWindowHandle;
            _processId = processId;

            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out object patternObj))
            {
                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowClosedEvent,
                    element,
                    TreeScope.Subtree,
                    OnWindowClosed);
            }
        }

        ~WindowElement()
        {
            Dispose();
        }

        private void OnWindowClosed(object sender, AutomationEventArgs e)
        {
            Closed?.Invoke(this);
        }

        public void Dispose()
        {
            // Remove the event handler
            if (_automationElement != null)
            {
                try
                {
                    Automation.RemoveAutomationEventHandler(
                        WindowPattern.WindowClosedEvent,
                        _automationElement,
                        OnWindowClosed);
                }
                catch { }

                // Clear the reference to the element
                _automationElement = null;
            }

            // Suppress finalization to optimize garbage collection
            GC.SuppressFinalize(this);
        }
    }
}
