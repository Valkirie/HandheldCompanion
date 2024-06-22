using HandheldCompanion.Controls;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class QuickApplicationsPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProcessExViewModel> Processes { get; set; } = [];

        public QuickApplicationsPageViewModel()
        {
            ProcessManager.ProcessStarted += ProcessStarted;
            ProcessManager.ProcessStopped += ProcessStopped;

            // get processes
            foreach (ProcessEx processEx in ProcessManager.GetProcesses())
                ProcessStarted(processEx, true);
        }

        public override void Dispose()
        {
            ProcessManager.ProcessStarted -= ProcessStarted;
            ProcessManager.ProcessStopped -= ProcessStopped;
            base.Dispose();
        }

        private void ProcessStopped(ProcessEx processEx)
        {
            ProcessExViewModel? foundProcess = Processes.ToList().FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
            if (foundProcess is not null)
            {
                Processes.SafeRemove(foundProcess);
                foundProcess.Dispose();
            }
        }

        private void ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            ProcessExViewModel? foundProcess = Processes.ToList().FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
            if (foundProcess is null)
            {
                Processes.SafeAdd(new ProcessExViewModel(processEx));
            }
            else
            {
                // Some apps might have the process come in twice, update the process on the viewmodel
                foundProcess.Process = processEx;
            }
        }
    }
}
