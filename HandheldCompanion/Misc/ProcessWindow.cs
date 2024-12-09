using HandheldCompanion.Utils;
using System;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class ProcessWindow : IDisposable
    {
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

        public EventHandler Refreshed;

        public ProcessWindow(AutomationElement element, bool isPrimary)
        {
            Hwnd = element.Current.NativeWindowHandle;
            Element = element;

            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out object patternObj))
            {
                Automation.AddAutomationPropertyChangedEventHandler(
                Element,
                TreeScope.Element,
                new AutomationPropertyChangedEventHandler(OnPropertyChanged),
                AutomationElement.NameProperty,
                AutomationElement.BoundingRectangleProperty);
            }

            RefreshName(false);
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
            try
            {
                // Remove the event handler when done
                Automation.RemoveAllEventHandlers();
            }
            catch { }
            GC.SuppressFinalize(this);
        }
    }
}
