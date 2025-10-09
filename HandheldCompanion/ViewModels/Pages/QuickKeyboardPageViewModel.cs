using HandheldCompanion.Devices;
using HandheldCompanion.Views.QuickPages;
using System;
using System.Windows.Input;

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
                if (SetProperty(ref shiftToggleChecked, value, null, nameof(ShiftToggleChecked)))
                    quickKeyboardPage.RelabelAll();
            }
        }

        private bool shiftToggleLocked = false;
        public bool ShiftToggleLocked
        {
            get => shiftToggleLocked;
            set
            {
                SetProperty(ref shiftToggleLocked, value, null, nameof(ShiftToggleLocked));
            }
        }

        public IDevice CurrentDevice { get; } = IDevice.GetCurrent();
        public bool IsFlipDS => CurrentDevice is AYANEOFlipDS;

        public ICommand ShiftToggleClicked { get; private set; }
        private DateTime lastShiftToggleClick = DateTime.MinValue;

        public QuickKeyboardPageViewModel(QuickKeyboardPage quickKeyboardPage)
        {
            this.quickKeyboardPage = quickKeyboardPage;

            ShiftToggleClicked = new DelegateCommand(async () =>
            {
                DateTime now = DateTime.Now;

                // detect double click
                if (now - lastShiftToggleClick < TimeSpan.FromMilliseconds(200))
                {
                    // set lock on double click
                    ShiftToggleChecked = true;
                    ShiftToggleLocked = true;
                }
                else
                {
                    // disable lock on single click
                    ShiftToggleLocked = false;
                }

                // update vars
                lastShiftToggleClick = now;
            });
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
