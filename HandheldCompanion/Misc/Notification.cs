using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HandheldCompanion.Misc
{
    public class Notification
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string Action { get; private set; }
        public InfoBarSeverity Severity { get; private set; }
        public bool IsClosable { get; private set; }
        public bool IsClickable { get; private set; }

        public Notification(string title, string message, string action, InfoBarSeverity severity)
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
