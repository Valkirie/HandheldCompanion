using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

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
        private double GyroFactorX = 1.0; // Todo should get from profile
        private double GyroFactorY = 1.0; // Todo should get from profile
        private double AdditionalFactor = 20.0; // Sensitivity?

        // Time
        // Stopwatch, used for delta 
        protected readonly Stopwatch stopwatch = new();
        double UpdateTimePreviousMilliSeconds;

        // Timer used for sensor data reading 
        private Timer UpdateTimer;

        int CalculationIntervalMilliseconds = 15;

        private readonly ILogger logger;

        public SensorFusion(ILogger logger)
        {
            this.logger = logger;
        }

        public void UpdateReport(Vector3 AngularVelocity, Vector3 Acceleration)
        {
            if (AngularVelocity == new Vector3() || Acceleration == new Vector3())
                return;

            // TODO Do swapping and inversion based on profile

            // Determine time
            // Note Elapsed.TotalMilliseconds returns milliseconds including x.xxx precision i.e. microseconds.
            double DeltaSeconds = (double)(stopwatch.Elapsed.TotalMilliseconds - UpdateTimePreviousMilliSeconds) / 1000L;
            UpdateTimePreviousMilliSeconds = stopwatch.Elapsed.TotalMilliseconds;

            // Do calculations 
            CalculateGravitySimple(DeltaSeconds, AngularVelocity, Acceleration);
            //CalculateGravityFancy(DeltaSeconds, AngularVelocity, Acceleration);
            DeviceAngles(GravityVectorSimple);
            //PlayerSpace(DeltaSeconds, AngularVelocity, GravityVectorSimple);
        }
        public void CalculateGravitySimple(double DeltaTimeSec, Vector3 AngularVelocity, Vector3 Acceleration)
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

            GravityVectorSimple += Vector3.Normalize(gravityDelta) * (float)0.02;

        }

        public void CalculateGravityFancy(double DeltaTimeSec, Vector3 AngularVelocity, Vector3 accel)
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
            logger.LogInformation("Plot vigemtarget_reverseRotationy.X {0} {1}", stopwatch.Elapsed.TotalMilliseconds, reverseRotation.X);
            logger.LogInformation("Plot vigemtarget_reverseRotationy.Y {0} {1}", stopwatch.Elapsed.TotalMilliseconds, reverseRotation.Y);
            logger.LogInformation("Plot vigemtarget_reverseRotationy.Z {0} {1}", stopwatch.Elapsed.TotalMilliseconds, reverseRotation.Z);
            logger.LogInformation("Plot vigemtarget_reverseRotationy.W {0} {1}", stopwatch.Elapsed.TotalMilliseconds, reverseRotation.W);

            // rotate gravity vector
            GravityVectorFancy = Vector3.Transform(GravityVectorFancy, reverseRotation);

            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_after_rotate_x {0} {1}", stopwatch.Elapsed.TotalMilliseconds, GravityVectorFancy.X);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_after_rotate_y {0} {1}", stopwatch.Elapsed.TotalMilliseconds, GravityVectorFancy.Y);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_after_rotate_z {0} {1}", stopwatch.Elapsed.TotalMilliseconds, GravityVectorFancy.Z);

            // Correction factor variables
            SmoothAccel = Vector3.Transform(SmoothAccel, reverseRotation);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel_x {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.X);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel_y {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.Y);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel_z {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.Z);
            // Note to self, SmoothAccel seems OK.
            float smoothInterpolator = (float)Math.Pow(2, (-(float)DeltaTimeSec / SmoothingHalfTime));
            // Note to self, SmoothInterpolator seems OK, still no sure about the Pow from C++ to C#, also, is it suppose to be a negative value?
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_smoothInterpolator {0} {1}", stopwatch.Elapsed.TotalMilliseconds, smoothInterpolator);

            Shakiness *= smoothInterpolator;
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_ShakinessTimesInterpolator {0} {1}", stopwatch.Elapsed.TotalMilliseconds, Shakiness);
            Shakiness = Math.Max(Shakiness, Vector3.Subtract(accel, SmoothAccel).Length()); // Does this apply vector subtract and length correctly?
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_Shakiness {0} {1}", stopwatch.Elapsed.TotalMilliseconds, Shakiness);
            SmoothAccel = Vector3.Lerp(accel, SmoothAccel, smoothInterpolator); // smoothInterpolator is a negative value, correct?
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_x {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.X);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_y {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.Y);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_SmoothAccel2_z {0} {1}", stopwatch.Elapsed.TotalMilliseconds, SmoothAccel.Z);

            Vector3 gravityDelta = Vector3.Subtract(-accel, GravityVectorFancy);
            Vector3 gravityDirection = Vector3.Normalize(gravityDelta);
            float correctionRate;

            // Shakiness correction rate impact
            if (ShakinessMaxThreshold > ShakinessMinThreshold)
            {
                float stillOrShaky = Math.Clamp((Shakiness - ShakinessMinThreshold) / (ShakinessMaxThreshold - ShakinessMaxThreshold), 0, 1);
                // 

                logger.LogInformation("Plot vigemtarget_GravityVectorFancy_stillOrShaky {0} {1}", stopwatch.Elapsed.TotalMilliseconds, stillOrShaky);

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

            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_correctionRate {0} {1}", stopwatch.Elapsed.TotalMilliseconds, correctionRate);

            // limit in proportion to rotation rate
            // my input library has the gyro report degrees per second, so convert to radians per second here
            float angleRate = AngularVelocity.Length() * (float)Math.PI / 180;
            float correctionLimit = angleRate * GravityVectorFancy.Length() * CorrectionGyroFactor;

            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_angleRate {0} {1}", stopwatch.Elapsed.TotalMilliseconds, angleRate);
            logger.LogInformation("Plot vigemtarget_GravityVectorFancy_correctionLimit {0} {1}", stopwatch.Elapsed.TotalMilliseconds, correctionLimit);

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

        private void PlayerSpace(double DeltaTimeSec, Vector3 GravityVector, Vector3 AngularVelocity)
        {
            // PlayerSpace
            Vector3 GravityNorm = Vector3.Normalize(GravityVector);

            // use world yaw for yaw direction, local combined yaw for magnitude
            double worldYaw = AngularVelocity.Y * GravityNorm.Y + AngularVelocity.Z * GravityNorm.Z; // dot product but just yaw and roll
            double yawRelaxFactor = 1.41f;
            Vector2 AngularVelocityXZ = new(AngularVelocity.Y, AngularVelocity.Z);

            double CameraYawDelta = Math.Sign(worldYaw)
                                    * Math.Min(Math.Abs(worldYaw) * yawRelaxFactor, AngularVelocityXZ.Length())
                                    * GyroFactorY * AdditionalFactor * DeltaTimeSec;

            CameraYaw -= CameraYawDelta;

            // local pitch:
            double CameraPitchDelta = AngularVelocity.X * GyroFactorX * AdditionalFactor * DeltaTimeSec;

            CameraPitch += CameraPitchDelta;
        }

        private void DeviceAngles(Vector3 GravityVector)
        {

            // Calculate angles around Y and X axis (Theta and Psi) using all 3 directions of accelerometer
            // Based on: https://www.digikey.com/en/articles/using-an-accelerometer-for-inclination-sensing               
            DeviceAngle.X = (float)(-1 * (Math.Atan(GravityVector.Y / (Math.Sqrt(Math.Pow(GravityVector.X, 2) + Math.Pow(GravityVector.Z, 2))))) * 180 / Math.PI);
            DeviceAngle.Y = (float)(-1 * (Math.Atan(GravityVector.X / (Math.Sqrt(Math.Pow(GravityVector.Y, 2) + Math.Pow(GravityVector.Z, 2))))) * 180 / Math.PI);
        }

        public Vector3 GetCurrentReading()
        {
            return GravityVectorSimple;
        }
    }
}
