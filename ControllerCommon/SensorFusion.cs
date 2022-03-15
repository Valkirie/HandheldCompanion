using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace ControllerCommon
{
    public class SensorFusion
    {
        // Gravity Simple
        private Vector3 GravityVectorSimple;

        // Device Angle
        public Vector2 DeviceAngle;

        // Gravity Fancy
        private float Shakiness;
        private Vector3 SmoothAccel;
        private Vector3 GravityVectorFancy;

        // Player Space
        private double CameraYaw;
        private double CameraPitch;
        public double CameraYawDelta;
        public double CameraPitchDelta;
        // Bring Player Space more on par with gyro only
        private double AdditionalFactor = 30.0; 

        // Time
        double UpdateTimePreviousMilliSeconds;

        private readonly ILogger logger;

        public SensorFusion(ILogger logger)
        {
            this.logger = logger;
        }

        public void UpdateReport(double TotalMilliseconds, Vector3 AngularVelocity, Vector3 Acceleration)
        {
            // Determine time
            // Note Elapsed.TotalMilliseconds returns milliseconds including x.xxx precision i.e. microseconds.
            double DeltaSeconds = (double)(TotalMilliseconds - UpdateTimePreviousMilliSeconds) / 1000L;
            UpdateTimePreviousMilliSeconds = TotalMilliseconds;

            Task.Run(() => logger.LogDebug("Plot XInputSensorFusion_DeltaSeconds {0} {1}", TotalMilliseconds, DeltaSeconds));

            Task.Run(() =>
            {
                logger.LogDebug("Plot XInputSensorFusion_AngularVelocityX {0} {1}", TotalMilliseconds, AngularVelocity.X);
                logger.LogDebug("Plot XInputSensorFusion_AngularVelocityY {0} {1}", TotalMilliseconds, AngularVelocity.Y);
                logger.LogDebug("Plot XInputSensorFusion_AngularVelocityZ {0} {1}", TotalMilliseconds, AngularVelocity.Z);

                logger.LogDebug("Plot XInputSensorFusion_AccelerationX {0} {1}", TotalMilliseconds, Acceleration.X);
                logger.LogDebug("Plot XInputSensorFusion_AccelerationY {0} {1}", TotalMilliseconds, Acceleration.Y);
                logger.LogDebug("Plot XInputSensorFusion_AccelerationZ {0} {1}", TotalMilliseconds, Acceleration.Z);
            });

            // Check for empty inputs, prevent NaN computes
            Vector3 EmptyVector = new(0f, 0f, 0f);
            if (AngularVelocity.Equals(EmptyVector) || Acceleration.Equals(EmptyVector))
            {
                logger.LogDebug("Sensorfusion prevented from calculating with empty vectors.");
                return;
            }

            // Perform calculations 
            // Todo, kickstart gravity vector with = acceleration when calculation is either
            // run for the first time or is selcted to be run based on user profile?
            // Todo, gravity is inverted acceleration but everything still works fine, figure out
            CalculateGravitySimple(TotalMilliseconds, DeltaSeconds, AngularVelocity, Acceleration);
            //CalculateGravityFancy(TotalMilliseconds, DeltaSeconds, AngularVelocity, Acceleration);
            
            DeviceAngles(TotalMilliseconds, GravityVectorSimple);
            PlayerSpace(TotalMilliseconds, DeltaSeconds, AngularVelocity, GravityVectorSimple);

        }

        public void CalculateGravitySimple(double TotalMilliseconds, double DeltaTimeSec, Vector3 AngularVelocity, Vector3 Acceleration)
        {
            // Gravity determination using sensor fusion, "Something Simple" example from:
            // http://gyrowiki.jibbsmart.com/blog:finding-gravity-with-sensor-fusion

            // Convert to radian as per library spec
            Vector3 AngularVelocityRad = new Vector3(Utils.deg2rad(AngularVelocity.X), Utils.deg2rad(AngularVelocity.Y), Utils.deg2rad(AngularVelocity.Z));
            // Normalize before creating quat from axis angle as per library spec
            AngularVelocityRad = Vector3.Normalize(AngularVelocityRad);

            // convert gyro input to reverse rotation  
            Quaternion reverseRotation = Quaternion.CreateFromAxisAngle(-AngularVelocityRad, AngularVelocityRad.Length() * (float)DeltaTimeSec);

            // rotate gravity vector
            GravityVectorSimple = Vector3.Transform(GravityVectorSimple, reverseRotation);

            // nudge towards gravity according to current acceleration
            Vector3 newGravity = -Acceleration;
            Vector3 gravityDelta = Vector3.Subtract(newGravity, GravityVectorSimple);

            GravityVectorSimple += Vector3.Multiply(0.02f, Vector3.Normalize(gravityDelta));

            Task.Run(() =>
            {
                logger.LogDebug("Plot XInputSensorFusion_GravityVectorSimpleEndX {0} {1}", TotalMilliseconds, GravityVectorSimple.X);
                logger.LogDebug("Plot XInputSensorFusion_GravityVectorSimpleEndY {0} {1}", TotalMilliseconds, GravityVectorSimple.Y);
                logger.LogDebug("Plot XInputSensorFusion_GravityVectorSimpleEndZ {0} {1}", TotalMilliseconds, GravityVectorSimple.Z);
            });
        }

        public void CalculateGravityFancy(double TotalMilliseconds, double DeltaTimeSec, Vector3 AngularVelocity, Vector3 Acceleration)
        {
            // TODO Does not work yet!!!

            // Gravity determination using sensor fusion, "Something Fancy" example from:
            // http://gyrowiki.jibbsmart.com/blog:finding-gravity-with-sensor-fusion

            // SETTINGS
            // the time it takes in our acceleration smoothing for 'A' to get halfway to 'B'
            float SmoothingHalfTime = 0.25f;

            // thresholds of trust for accel shakiness. less shakiness = more trust
            float ShakinessMaxThreshold = 0.4f;
            float ShakinessMinThreshold = 0.27f;//0.01f;

            // when we trust the accel a lot (the controller is "still"), how quickly do we correct our gravity vector?
            float CorrectionStillRate = 1f;
            // when we don't trust the accel (the controller is "shaky"), how quickly do we correct our gravity vector?
            float CorrectionShakyRate = 0.1f;

            // if our old gravity vector is close enough to our new one, limit further corrections to this proportion of the rotation speed
            float CorrectionGyroFactor = 0.1f;
            // thresholds for what's considered "close enough"
            float CorrectionGyroMinThreshold = 0.05f;
            float CorrectionGyroMaxThreshold = 0.25f;

            // no matter what, always apply a minimum of this much correction to our gravity vector
            float CorrectionMinimumSpeed = 0.01f;

            // Question
            // Isn't this always true with the default settings? if (ShakinessMaxThreshold > ShakinessMinThreshold)

            // Convert to radian as per library spec
            Vector3 AngularVelocityRad = new Vector3(Utils.deg2rad(AngularVelocity.X), Utils.deg2rad(AngularVelocity.Y), Utils.deg2rad(AngularVelocity.Z));
            // Normalize before creating quat from axis angle as per library spec
            AngularVelocityRad = Vector3.Normalize(AngularVelocityRad);

            // convert gyro input to reverse rotation  
            Quaternion reverseRotation = Quaternion.CreateFromAxisAngle(-AngularVelocityRad, AngularVelocityRad.Length() * (float)DeltaTimeSec);

            Task.Run(() =>
            {

                logger.LogDebug("Plot vigemtarget_reverseRotationy.X {0} {1}", TotalMilliseconds, reverseRotation.X);
                logger.LogDebug("Plot vigemtarget_reverseRotationy.Y {0} {1}", TotalMilliseconds, reverseRotation.Y);
                logger.LogDebug("Plot vigemtarget_reverseRotationy.Z {0} {1}", TotalMilliseconds, reverseRotation.Z);
                logger.LogDebug("Plot vigemtarget_reverseRotationy.W {0} {1}", TotalMilliseconds, reverseRotation.W);
            });

            // rotate gravity vector
            GravityVectorFancy = Vector3.Transform(GravityVectorFancy, reverseRotation);

            // Correction factor variables
            SmoothAccel = Vector3.Transform(SmoothAccel, reverseRotation);

            Task.Run(() =>
            {
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_after_rotate_x {0} {1}", TotalMilliseconds, GravityVectorFancy.X);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_after_rotate_y {0} {1}", TotalMilliseconds, GravityVectorFancy.Y);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_after_rotate_z {0} {1}", TotalMilliseconds, GravityVectorFancy.Z);

                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel_x {0} {1}", TotalMilliseconds, SmoothAccel.X);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel_y {0} {1}", TotalMilliseconds, SmoothAccel.Y);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel_z {0} {1}", TotalMilliseconds, SmoothAccel.Z);
            });

            // Note to self, SmoothAccel seems OK.
            float smoothInterpolator = (float)Math.Pow(2, (-(float)DeltaTimeSec / SmoothingHalfTime));
            // Note to self, SmoothInterpolator seems OK, still no sure about the Pow from C++ to C#, also, is it suppose to be a negative value?

            Shakiness *= smoothInterpolator;
            Shakiness = Math.Max(Shakiness, Vector3.Subtract(Acceleration, SmoothAccel).Length()); // Does this apply vector subtract and length correctly?
            SmoothAccel = Vector3.Lerp(Acceleration, SmoothAccel, smoothInterpolator); // smoothInterpolator is a negative value, correct?

            Task.Run(() =>
            {
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_smoothInterpolator {0} {1}", TotalMilliseconds, smoothInterpolator);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_ShakinessTimesInterpolator {0} {1}", TotalMilliseconds, Shakiness);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_Shakiness {0} {1}", TotalMilliseconds, Shakiness);

                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_x {0} {1}", TotalMilliseconds, SmoothAccel.X);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_y {0} {1}", TotalMilliseconds, SmoothAccel.Y);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_z {0} {1}", TotalMilliseconds, SmoothAccel.Z);
            });

            Vector3 gravityDelta = Vector3.Subtract(-Acceleration, GravityVectorFancy);
            Vector3 gravityDirection = Vector3.Normalize(gravityDelta);
            float correctionRate;

            // Shakiness correction rate impact
            if (ShakinessMaxThreshold > ShakinessMinThreshold)
            {
                float stillOrShaky = Math.Clamp((Shakiness - ShakinessMinThreshold) / (ShakinessMaxThreshold - ShakinessMaxThreshold), 0, 1);

                Task.Run(() =>
                {
                    logger.LogDebug("Plot vigemtarget_GravityVectorFancy_stillOrShaky {0} {1}", TotalMilliseconds, stillOrShaky);
                });

                correctionRate = CorrectionStillRate + (CorrectionShakyRate - CorrectionStillRate) * stillOrShaky;
                // 1 + (0.1 - 1) * 1 = 0.1
                // Note, found still or shaky to be a constant 1, correction rate to be a constant 0.1

            }
            else if (Shakiness > ShakinessMaxThreshold)
            {
                correctionRate = CorrectionShakyRate;
            }
            else
            {
                correctionRate = CorrectionStillRate;
            }

            // limit in proportion to rotation rate
            // my input library has the gyro report degrees per second, so convert to radians per second here
            float angleRate = AngularVelocity.Length() * (float)Math.PI / 180;
            float correctionLimit = angleRate * GravityVectorFancy.Length() * CorrectionGyroFactor;

            Task.Run(() =>
            {
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_correctionRate {0} {1}", TotalMilliseconds, correctionRate);

                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_angleRate {0} {1}", TotalMilliseconds, angleRate);
                logger.LogDebug("Plot vigemtarget_GravityVectorFancy_correctionLimit {0} {1}", TotalMilliseconds, correctionLimit);
            });

            if (correctionRate > correctionLimit)
            {
                float closeEnoughFactor;

                if (CorrectionGyroMaxThreshold > CorrectionGyroMinThreshold)
                {
                    closeEnoughFactor = Math.Clamp((gravityDelta.Length() - CorrectionGyroMinThreshold) / (CorrectionGyroMaxThreshold - CorrectionGyroMinThreshold), 0, 1);
                }
                else if (gravityDelta.Length() > CorrectionGyroMaxThreshold)
                {
                    closeEnoughFactor = 1;
                }
                else
                {
                    closeEnoughFactor = 0;
                }
                correctionRate += (correctionLimit - correctionRate) * closeEnoughFactor;
            }

            // finally, let's always allow a little bit of correction
            correctionRate = Math.Max(correctionRate, CorrectionMinimumSpeed);

            // apply correction
            Vector3 correction = gravityDirection * (float)(correctionRate * DeltaTimeSec);

            if (correction.LengthSquared() < gravityDelta.LengthSquared())
            {
                GravityVectorFancy += correction;
            }
            else
            {
                GravityVectorFancy += gravityDelta;
            }
        }

        private void PlayerSpace(double TotalMilliseconds, double DeltaTimeSec, Vector3 AngularVelocity, Vector3 GravityVector)
        {
            // PlayerSpace
            Vector3 GravityNorm = Vector3.Normalize(GravityVector);

            // use world yaw for yaw direction, local combined yaw for magnitude
            double worldYaw = AngularVelocity.Y * GravityNorm.Y + AngularVelocity.Z * GravityNorm.Z; // dot product but just yaw and roll
            
            if (worldYaw == 0f) return; // handle NaN

            double yawRelaxFactor = 1.41f;
            Vector2 AngularVelocityYZ = new(AngularVelocity.Y, AngularVelocity.Z);

            CameraYawDelta = Math.Sign(worldYaw)
                                    * Math.Min(Math.Abs(worldYaw) * yawRelaxFactor, AngularVelocityYZ.Length())
                                    * AdditionalFactor * DeltaTimeSec;

            CameraYaw -= CameraYawDelta;

            // local pitch:
            CameraPitchDelta = AngularVelocity.X * AdditionalFactor * DeltaTimeSec;

            CameraPitch += CameraPitchDelta;

            logger?.LogDebug("CameraYawDelta {0}, CameraPitchDelta {1})", CameraYawDelta, CameraPitchDelta);
        }

        private void DeviceAngles(double TotalMilliseconds, Vector3 GravityVector)
        {

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            DeviceAngle.X = (float)(-1 * (Math.Atan(GravityVector.Y / (Math.Sqrt(Math.Pow(GravityVector.X, 2) + Math.Pow(GravityVector.Z, 2))))) * 180 / Math.PI);
            DeviceAngle.Y = (float)(-1 * (Math.Atan(GravityVector.X / (Math.Sqrt(Math.Pow(GravityVector.Y, 2) + Math.Pow(GravityVector.Z, 2))))) * 180 / Math.PI);

            Task.Run(() =>
            {
                logger.LogDebug("Plot XInputSensorFusion_DeviceAngle.Y {0} {1}", TotalMilliseconds, DeviceAngle.Y);
            });
        }
    }
}
