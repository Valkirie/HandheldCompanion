using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Commands;
using HandheldCompanion.Views.QuickPages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.ViewModels
{
    public class QuickKeyboardPageViewModel : BaseViewModel
    {
        private QuickKeyboardPage quickKeyboardPage;

        private bool shiftToggleChecked;
        public bool ShiftToggleChecked
        {
            get => shiftToggleChecked;
            set
            {
                if (SetProperty(ref shiftToggleChecked, value))
                    quickKeyboardPage.RelabelAll();
            }
        }

        public QuickKeyboardPageViewModel(QuickKeyboardPage quickKeyboardPage)
        {
            this.quickKeyboardPage = quickKeyboardPage;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
