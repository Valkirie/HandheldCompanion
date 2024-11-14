using System;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class WindowElement
    {
        public readonly IntPtr _hwnd;
        public readonly int _processId;

        public event OnWindowClosedEventHandler Closed;
        public delegate void OnWindowClosedEventHandler(WindowElement windowElement);

        public WindowElement(int processId, AutomationElement element)
        {
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

        private void OnWindowClosed(object sender, AutomationEventArgs e)
        {
            Closed?.Invoke(this);
        }
    }
}
