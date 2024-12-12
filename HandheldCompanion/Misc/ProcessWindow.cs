using HandheldCompanion.Utils;
using System;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class ProcessWindow : IDisposable
    {
        private AutomationPropertyChangedEventHandler handler;
        public EventHandler Refreshed;

        public AutomationElement Element;
        public readonly int Hwnd;

        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }

            set
            {
                if (!value.Equals(_Name))
                {
                    _Name = value;

                    // raise event
                    Refreshed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ProcessWindow(AutomationElement element, bool isPrimary)
        {
            Hwnd = element.Current.NativeWindowHandle;
            Element = element;

            // Create the event handler
            handler = new AutomationPropertyChangedEventHandler(OnPropertyChanged);

            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out object patternObj))
            {
                Automation.AddAutomationPropertyChangedEventHandler(
                Element,
                TreeScope.Element,
                handler,
                AutomationElement.NameProperty,
                AutomationElement.BoundingRectangleProperty);
            }

            RefreshName(false);
        }

        ~ProcessWindow()
        {
            Dispose();
        }

        private void OnPropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            // Handle the property change event
            if (Element != null)
            {
                // Check if the Name property changed
                if (e.Property == AutomationElement.NameProperty)
                {
                    RefreshName(false);
                }
                // Check if the BoundingRectangle property changed
                else if (e.Property == AutomationElement.BoundingRectangleProperty)
                {
                    // raise event
                    Refreshed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void RefreshName(bool queryCache)
        {
            try
            {
                if (queryCache)
                {
                    CacheRequest cacheRequest = new CacheRequest();
                    cacheRequest.Add(ValuePattern.ValueProperty);
                    Element = Element.GetUpdatedCache(cacheRequest);
                }

                if (Element.TryGetCurrentPattern(InvokePattern.Pattern, out _))
                {
                    string ElementName = Element.Current.Name;
                    if (!string.IsNullOrEmpty(ElementName))
                    {
                        // preferred method
                        Name = ElementName;
                        return;
                    }
                }

                // backup method
                string title = ProcessUtils.GetWindowTitle(Hwnd);
                if (!string.IsNullOrEmpty(title))
                    Name = title;
            }
            catch { }
        }

        public void Dispose()
        {
            if (Element != null)
            {
                try
                {
                    // Remove the event handler safely
                    if (handler != null)
                        Automation.RemoveAutomationPropertyChangedEventHandler(Element, handler);
                }
                catch { }

                // Clear the reference to the element
                Element = null;
            }

            // Suppress finalization to optimize garbage collection
            GC.SuppressFinalize(this);
        }
    }
}
