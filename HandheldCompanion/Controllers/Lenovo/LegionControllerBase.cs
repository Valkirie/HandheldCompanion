using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using System;

namespace HandheldCompanion.Controllers.Lenovo
{
    public abstract class LegionControllerBase : XInputController
    {
        protected controller_hidapi.net.LegionController? Controller;
        protected byte[] data = new byte[64];

        protected bool IsPassthrough = false;
        protected bool IsControllerSwap = false;

        public LegionControllerBase() : base() { }

        public LegionControllerBase(PnPDetails details) : base(details) { }

        protected virtual void Open()
        {
            lock (hidLock)
            {
                try
                {
                    if (Controller is not null)
                    {
                        Controller.OnControllerInputReceived += Controller_OnControllerInputReceived;
                        Controller.Open();
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Couldn't initialize {0}. Exception: {1}", GetType(), ex.Message);
                    return;
                }
            }
        }

        protected virtual void Close()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.Close();
                }
            }
        }

        public override void Gone()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.EndRead();
                    Controller = null;
                }
            }
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            Buffer.BlockCopy(Data, 0, this.data, 0, Data.Length);
        }

        public override void Plug()
        {
            Open();

            base.Plug();
        }

        public override void Unplug()
        {
            Close();

            base.Unplug();
        }

        protected override void QuerySettings()
        {
            SettingsManager_SettingValueChanged("LegionControllerPassthrough", ManagerFactory.settingsManager.GetBoolean("LegionControllerPassthrough"), false, false);
            SettingsManager_SettingValueChanged("LegionControllerSwap", ManagerFactory.settingsManager.GetBoolean("LegionControllerSwap"), false, false);
            base.QuerySettings();
        }

        protected override void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "LegionControllerPassthrough":
                    IsPassthrough = Convert.ToBoolean(value);
                    break;
                case "LegionControllerSwap":
                    IsControllerSwap = Convert.ToBoolean(value);
                    break;
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary, initializing);
        }

        protected void ApplyControllerSwap()
        {
            (Inputs.ButtonState[ButtonFlags.OEM1], Inputs.ButtonState[ButtonFlags.Start]) = (Inputs.ButtonState[ButtonFlags.Start], Inputs.ButtonState[ButtonFlags.OEM1]);
            (Inputs.ButtonState[ButtonFlags.OEM2], Inputs.ButtonState[ButtonFlags.Back]) = (Inputs.ButtonState[ButtonFlags.Back], Inputs.ButtonState[ButtonFlags.OEM2]);
        }
    }
}
