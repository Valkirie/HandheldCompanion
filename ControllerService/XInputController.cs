using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Logging;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ControllerService
{
    public class XInputController
    {
        public Controller Controller;
        public ViGEmTarget Target;

        public Profile profile;
        public Profile defaultProfile;

        public DeviceInstance Instance;

        public XInputGirometer Gyrometer;
        public XInputAccelerometer Accelerometer;

        public UserIndex UserIndex;
        private readonly ILogger logger;

        public XInputController(Controller controller, UserIndex index, ILogger logger)
        {
            this.logger = logger;

            // initilize controller
            this.Controller = controller;
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

        public void SetGyroscope(XInputGirometer _gyrometer)
        {
            Gyrometer = _gyrometer;
        }

        public void SetAccelerometer(XInputAccelerometer _accelerometer)
        {
            Accelerometer = _accelerometer;
        }

        public void SetTarget(ViGEmTarget target)
        {
            this.Target = target;

            Gyrometer.ReadingChanged += Target.Girometer_ReadingChanged;
            Accelerometer.ReadingChanged += Target.Accelerometer_ReadingChanged;

            logger.LogInformation("Virtual {0} attached to {1} on slot {2}", target, Instance.InstanceName, UserIndex);
            logger.LogInformation("Virtual {0} report interval set to {1}ms", target, Target.UpdateTimer.Interval);
        }
    }
}
