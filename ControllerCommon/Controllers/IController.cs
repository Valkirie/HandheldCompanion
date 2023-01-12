﻿using ControllerCommon.Managers;
using ModernWpf.Controls;
using PrecisionTiming;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ControllerCommon.Controllers
{
    [Flags]
    public enum ControllerCapacities : ushort
    {
        None = 0,
        Gyroscope = 1,
        Accelerometer = 2,
    }

    public abstract class IController
    {
        #region events
        public event InputsUpdatedEventHandler InputsUpdated;
        public delegate void InputsUpdatedEventHandler(ControllerInputs Inputs);

        public event MovementsUpdatedEventHandler MovementsUpdated;
        public delegate void MovementsUpdatedEventHandler(ControllerMovements Movements);
        #endregion

        public ControllerInputs Inputs = new();
        public ControllerMovements Movements = new();

        protected const short UPDATE_INTERVAL = 5;

        public ControllerButtonFlags InjectedButtons;
        public ControllerButtonFlags prevInjectedButtons;

        public ControllerCapacities Capacities = ControllerCapacities.None;
        public bool HideOnHook = true;

        protected int UserIndex;
        protected double VibrationStrength = 1.0d;

        protected PnPDetails Details;

        protected PrecisionTimer MovementsTimer;
        protected PrecisionTimer InputsTimer;

        protected Border ui_border = new Border() { CornerRadius = new CornerRadius(4, 4, 4, 4), Padding = new Thickness(15, 12, 12, 12) };
        protected Grid ui_grid = new Grid();
        protected FontIcon ui_icon = new FontIcon() { Glyph = "\uE7FC", Height = 40, HorizontalAlignment = HorizontalAlignment.Center };
        protected TextBlock ui_name = new TextBlock() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        protected Button ui_button_hide = new Button() { Width = 100, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        protected Button ui_button_hook = new Button() { Width = 100, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Style = Application.Current.FindResource("AccentButtonStyle") as Style };
        protected DockPanel ui_dock_content = new DockPanel() { HorizontalAlignment = HorizontalAlignment.Left };
        protected SimpleStackPanel ui_dock_buttons = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };


        protected IController()
        {
            InputsTimer = new PrecisionTimer();
            InputsTimer.SetInterval(UPDATE_INTERVAL);
            InputsTimer.SetAutoResetMode(true);

            MovementsTimer = new PrecisionTimer();
            MovementsTimer.SetInterval(UPDATE_INTERVAL);
            MovementsTimer.SetAutoResetMode(true);

            // attribute controller to tag
            ui_border.Tag = this;
        }

        public virtual void UpdateInputs()
        {
            // update states
            Inputs.Timestamp = Environment.TickCount;
            prevInjectedButtons = InjectedButtons;

            InputsUpdated?.Invoke(Inputs);
        }

        public virtual void UpdateMovements()
        {
            // update states
            Movements.Timestamp = Environment.TickCount;

            MovementsUpdated?.Invoke(Movements);
        }

        public bool HasGyro()
        {
            return Capacities.HasFlag(ControllerCapacities.Gyroscope);
        }

        public bool HasAccelerometer()
        {
            return Capacities.HasFlag(ControllerCapacities.Accelerometer);
        }

        public bool IsVirtual()
        {
            return Details.isVirtual;
        }

        public bool IsGaming()
        {
            return Details.isGaming;
        }

        public int GetUserIndex()
        {
            return UserIndex;
        }

        public string GetInstancePath()
        {
            return Details.deviceInstanceId;
        }

        public string GetContainerInstancePath()
        {
            return Details.baseContainerDeviceInstanceId;
        }

        public override string ToString()
        {
            return Details.Name;
        }

        protected void DrawControls()
        {
            // update name
            ui_name.Text = this.ToString();

            // Define columns
            ColumnDefinition colDef0 = new ColumnDefinition() { Width = new GridLength(9, GridUnitType.Star), MinWidth = 200 };
            ColumnDefinition colDef1 = new ColumnDefinition() { MinWidth = 240 };

            ui_grid.ColumnDefinitions.Add(colDef0);
            ui_grid.ColumnDefinitions.Add(colDef1);

            // SetResourceReference
            /*
            ui_icon.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_name.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_button_hide.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_button_hook.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            */
            ui_border.SetResourceReference(Control.BackgroundProperty, "SystemControlPageBackgroundAltHighBrush");

            ui_dock_content.Children.Add(ui_icon);
            ui_dock_content.Children.Add(ui_name);
            ui_grid.Children.Add(ui_dock_content);
            Grid.SetColumn(ui_dock_content, 0);

            ui_dock_buttons.Children.Add(ui_button_hook);
            ui_dock_buttons.Children.Add(ui_button_hide);
            ui_grid.Children.Add(ui_dock_buttons);
            Grid.SetColumn(ui_dock_buttons, 1);

            ui_border.Child = ui_grid;
        }

        protected void RefreshControls()
        {
            ui_button_hook.IsEnabled = !IsPlugged();
            ui_button_hook.Content = IsPlugged() ? "Connected" : "Connect";
            ui_button_hide.Content = IsHidden() ? "Unhide" : "Hide";
        }

        public FrameworkElement GetControl()
        {
            return ui_border;
        }

        public Button GetButtonHook()
        {
            return ui_button_hook;
        }

        public Button GetButtonHide()
        {
            return ui_button_hide;
        }

        public void InjectButton(ControllerButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            if (button == ControllerButtonFlags.None)
                return;

            if (IsKeyDown)
                InjectedButtons |= button;
            else if (IsKeyUp)
                InjectedButtons &= ~button;

            LogManager.LogDebug("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", button, IsKeyDown, IsKeyUp, ToString());
        }

        public virtual void SetVibrationStrength(double value)
        {
            VibrationStrength = value / 100;
        }

        public virtual void SetVibration(byte LargeMotor, byte SmallMotor)
        { }

        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual void Rumble(int loop)
        { }

        public virtual bool IsPlugged()
        {
            return InputsTimer.IsRunning();
        }

        public virtual void Plug()
        {
            InjectedButtons = ControllerButtonFlags.None;
            InputsTimer.Start();

            RefreshControls();
        }

        public virtual void Unplug()
        {
            InputsTimer.Stop();

            RefreshControls();
        }

        public bool IsHidden()
        {
            bool hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            bool hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
            return (hide_device || hide_base);
        }

        public void Hide()
        {
            HidHide.HidePath(Details.deviceInstanceId);
            HidHide.HidePath(Details.baseContainerDeviceInstanceId);

            RefreshControls();
        }

        public void Unhide()
        {
            HidHide.UnhidePath(Details.deviceInstanceId);
            HidHide.UnhidePath(Details.baseContainerDeviceInstanceId);

            RefreshControls();
        }
    }
}
