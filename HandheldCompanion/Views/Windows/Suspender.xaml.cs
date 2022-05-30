using ControllerCommon;
using Microsoft.Extensions.Logging;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Suspender.xaml
    /// </summary>
    public partial class Suspender : Window
    {
        #region imports
        [DllImport("ntdll.dll", EntryPoint = "NtSuspendProcess", SetLastError = true, ExactSpelling = false)]
        private static extern UIntPtr NtSuspendProcess(IntPtr processHandle);
        [DllImport("ntdll.dll", EntryPoint = "NtResumeProcess", SetLastError = true, ExactSpelling = false)]
        private static extern UIntPtr NtResumeProcess(IntPtr processHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        #endregion

        private ILogger logger;
        private PipeClient pipeClient;

        // Gamepad vars
        private MultimediaTimer UpdateTimer;
        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private State GamepadState;

        // Gamepad triggers
        private bool TriggerListening = false;
        private bool Triggered = false;
        public GamepadButtonFlags TriggerButtons = GamepadButtonFlags.Back | GamepadButtonFlags.RightThumb;

        public event TriggerUpdatedEventHandler TriggerUpdated;
        public delegate void TriggerUpdatedEventHandler(GamepadButtonFlags button);

        public Suspender()
        {
            InitializeComponent();

            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
            UpdateTimer.Tick += UpdateReport;
            UpdateTimer.Start();
        }

        public Suspender(ILogger logger, PipeClient pipeClient) : this()
        {
            this.logger = logger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
        }

        public void UpdateController(ControllerEx controllerEx)
        {
            this.controllerEx = controllerEx;
        }

        private void UpdateReport(object? sender, EventArgs e)
        {
            // get current gamepad state
            if (controllerEx != null && controllerEx.IsConnected())
            {
                GamepadState = controllerEx.GetState();
                Gamepad = GamepadState.Gamepad;
            }

            // Handle controller trigger(s)
            if (!TriggerListening && Gamepad.Buttons.HasFlag(TriggerButtons))
            {
                if (!Triggered)
                {
                    UpdateVisibility();
                    Triggered = true;
                }
            }
            else if (Triggered)
            {
                Triggered = false;
            }

            // handle controller trigger(s) update
            if (TriggerListening)
            {
                if (Gamepad.Buttons != 0)
                    TriggerButtons |= Gamepad.Buttons;
                else if (Gamepad.Buttons == 0 && TriggerButtons != 0)
                {
                    TriggerUpdated?.Invoke(TriggerButtons);
                    TriggerListening = false;
                }
            }
        }

        private void UpdateVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Visibility = this.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
            });
        }

        private void OnServerMessage(object sender, PipeMessage e)
        {
            // do something
        }
    }
}
