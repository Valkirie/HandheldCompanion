using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System.Collections.Generic;

namespace ControllerService
{
    public class XInputController
    {
        public Controller physicalController;
        public ViGEmTarget virtualTarget;

        public Profile profile;
        private Profile defaultProfile;

        public float vibrationStrength = 100.0f;
        public int updateInterval = 10;

        public DeviceInstance Instance;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;
        public XInputInclinometer Inclinometer;

        public UserIndex UserIndex;
        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.physicalController = controller;
            this.UserIndex = index;

            // initialize profile(s)
            profile = new();
            defaultProfile = new();
        }

        public Dictionary<string, string> ToArgs()
        {
            return new Dictionary<string, string>() {
                { "ProductName", Instance.ProductName },
                { "InstanceGuid", $"{Instance.InstanceGuid}" },
                { "ProductGuid", $"{Instance.ProductGuid}" },
                { "ProductIndex", $"{(int)UserIndex}" }
            };
        }

        public void SetProfile(Profile profile)
        {
            // skip if current profile
            if (profile == this.profile)
                return;

            // restore default profile
            if (profile == null)
                profile = defaultProfile;

            this.profile = profile;

            // update default profile
            if (profile.IsDefault)
                defaultProfile = profile;
            else
                logger.LogInformation("Profile {0} applied.", profile.name);
        }

        public void SetGyroscope(XInputGirometer gyrometer)
        {
            Gyrometer = gyrometer;
        }

        public void SetAccelerometer(XInputAccelerometer accelerometer)
        {
            Accelerometer = accelerometer;
        }

        public void SetInclinometer(XInputInclinometer inclinometer)
        {
            Inclinometer = inclinometer;
        }

        public void SetPollRate(int HIDrate)
        {
            updateInterval = HIDrate;
            this.virtualTarget?.SetPollRate(updateInterval);
        }

        public void SetVibrationStrength(float strength)
        {
            vibrationStrength = strength;
            this.virtualTarget?.SetVibrationStrength(vibrationStrength);
        }

        public void SetViGEmTarget(ViGEmTarget target)
        {
            this.virtualTarget = target;
            Gyrometer.ReadingChanged += this.virtualTarget.Girometer_ReadingChanged;
            Accelerometer.ReadingHasChanged += this.virtualTarget.Accelerometer_ReadingChanged;

            SetPollRate(updateInterval);
            SetVibrationStrength(vibrationStrength);

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, Instance.InstanceName, UserIndex);
        }
    }
}
