using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static ControllerCommon.Utils.DeviceUtils;
using System.Timers;

namespace ControllerCommon
{
    public class ControllerEx
    {
        private ILogger logger;

        public Controller Controller;
        public string ControllerName;
        public UserIndex UserIndex;

        private XInputCapabilitiesEx CapabilitiesEx;
        public List<string> DeviceIDs = new();

        private Vibration IdentifyVibration = new Vibration() { LeftMotorSpeed = ushort.MaxValue, RightMotorSpeed = ushort.MaxValue };
        private Timer IdentifyTimer;

        public ControllerEx(UserIndex index, ILogger logger = null)
        {
            this.logger = logger;

            this.Controller = new Controller(index);
            this.UserIndex = index;

            // initialize timers
            IdentifyTimer = new Timer(200) { AutoReset= false };
            IdentifyTimer.Elapsed += IdentifyTimer_Tick;
        }

        public void PullCapabilitiesEx()
        {
            // pull data from xinput
            CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, (int)UserIndex, 0, ref CapabilitiesEx) != 0)
                logger?.LogWarning($"Failed to retrive XInputData on UserIndex:{0}", UserIndex);
            else
            {
                // initialize ID(s)
                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE \"%VID_0{CapabilitiesEx.VID.ToString("X2")}&PID_0{CapabilitiesEx.PID.ToString("X2")}%\"";
                var moSearch = new ManagementObjectSearcher(query);
                var moCollection = moSearch.Get();

                Dictionary<string, string> DeviceDetails = new();
                foreach (ManagementObject mo in moCollection)
                {
                    string DeviceID = (string)mo.Properties["DeviceID"].Value;
                    string DeviceName = (string)mo.Properties["Description"].Value;
                    DeviceDetails.Add(DeviceID, DeviceName);

                    if (DeviceID != null && !DeviceIDs.Contains(DeviceID))
                        DeviceIDs.Add(DeviceID);
                }

                // shorter key should contain true device name
                ControllerName = DeviceDetails.OrderBy(x => x.Key.Length).FirstOrDefault().Value;
            }
        }

        public override string ToString()
        {
            return ControllerName;
        }

        public State GetState()
        {
            return Controller.GetState();
        }

        public bool IsConnected()
        {
            return Controller.IsConnected;
        }

        public ushort GetPID()
        {
            return CapabilitiesEx.PID;
        }

        public ushort GetVID()
        {
            return CapabilitiesEx.VID;
        }

        public void Identify()
        {
            if (!Controller.IsConnected)
                return;

            Controller.SetVibration(IdentifyVibration);
            IdentifyTimer.Stop();
            IdentifyTimer.Start();
        }

        private void IdentifyTimer_Tick(object sender, EventArgs e)
        {
            Controller.SetVibration(new Vibration());
        }
    }
}
