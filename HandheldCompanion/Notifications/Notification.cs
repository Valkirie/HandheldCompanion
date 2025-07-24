using iNKORE.UI.WPF.Modern.Controls;
using System;

namespace HandheldCompanion.Notifications
{
    public class Notification
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Message { get; set; }
        public string Action { get; private set; }
        public InfoBarSeverity Severity { get; private set; }
        public bool IsClosable { get; private set; }
        public bool IsClickable { get; private set; }

        public bool IsInternal { get; set; }
        public bool IsIndeterminate { get; set; }

        public Notification(string title, string message, string action = "", InfoBarSeverity severity = InfoBarSeverity.Informational)
        {
            Title = title;
            Message = message;
            Action = action;
            Severity = severity;

            IsClosable = severity == InfoBarSeverity.Informational ? true : false;
            IsClickable = !string.IsNullOrEmpty(action);
        }

        public virtual void Execute()
        { }
    }
}
