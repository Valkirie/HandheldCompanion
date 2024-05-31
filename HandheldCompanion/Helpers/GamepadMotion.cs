using HandheldCompanion.Sensors;
using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Helpers
{
    public enum CalibrationMode
    {
        /// <summary>
        /// No auto-calibration. This is the default.
        /// </summary>
        Manual = 0,
        /// <summary>
        /// Automatically try to detect when the controller is being held still and update the calibration offset accordingly.
        /// </summary>
        Stillness = 1,
        /// <summary>
        /// Calculate an angular velocity from changes in the gravity direction as detected by the accelerometer. If these are steady enough, use them to make corrections to the calibration offset. This will only apply to relevant axes.
        /// </summary>
        SensorFusion = 2,
    }

    public class GamepadMotion : IDisposable
    {
        private IntPtr handle;
        private IMUCalibration calibration;
        private bool thresholdCalibration;

        public float gyroX;
        public float gyroY;
        public float gyroZ;
        public float accelX;
        public float accelY;
        public float accelZ;
        public float deltaTime;

        public const float minGyro = 124.0f;  // known minimum
        public const float minAccel = 2.0f;   // known minimum

        public float maxGyro = minGyro;
        public float maxAccel = minAccel;

        public string deviceInstanceId;

        private const string DllName = "GamepadMotion.dll";

        public GamepadMotion(string deviceInstanceId, CalibrationMode calibrationMode)
        {
            handle = CreateGamepadMotion();

            // store device path
            this.deviceInstanceId = deviceInstanceId;

            // get previous calibration
            calibration = IMUCalibration.GetCalibration(deviceInstanceId.ToUpper());
            SetCalibrationOffset(calibration.xOffset, calibration.yOffset, calibration.zOffset, calibration.weight);
            SetCalibrationMode(calibrationMode);
        }

        ~GamepadMotion()
        {
            Dispose(false);
        }

        public void Reset()
        {
            ResetGamepadMotion(handle);
        }

        public IMUCalibration GetCalibration()
        {
            return calibration;
        }

        // Implement the ProcessMotion function
        public void ProcessMotion(float gyroX, float gyroY, float gyroZ, float accelX, float accelY, float accelZ, float deltaTimeSeconds)
        {
            this.gyroX = gyroX;
            this.gyroY = gyroY;
            this.gyroZ = gyroZ;
            this.accelX = accelX;
            this.accelY = accelY;
            this.accelZ = accelZ;
            this.deltaTime = deltaTimeSeconds;

            if (thresholdCalibration)
            {
                if (gyroX > maxGyro)
                    maxGyro = gyroX;
                if (gyroY > maxGyro)
                    maxGyro = gyroY;
                if (gyroZ > maxGyro)
                    maxGyro = gyroZ;

                if (accelX > maxAccel)
                    maxAccel = accelX;
                if (accelY > maxAccel)
                    maxAccel = accelY;
                if (accelZ > maxAccel)
                    maxAccel = accelZ;
            }

            ProcessMotion(handle, gyroX, gyroY, gyroZ, accelX, accelY, accelZ, deltaTimeSeconds);
        }

        public void GetRawGyro(out float x, out float y, out float z)
        {
            x = gyroX;
            y = gyroY;
            z = gyroZ;
        }

        public void GetRawAcceleration(out float x, out float y, out float z)
        {
            x = accelX;
            y = accelY;
            z = accelZ;
        }

        // Implement the GetCalibratedGyro function
        public void GetCalibratedGyro(out float x, out float y, out float z)
        {
            GetCalibratedGyro(handle, out x, out y, out z);
        }

        // Implement the GetGravity function
        public void GetGravity(out float x, out float y, out float z)
        {
            GetGravity(handle, out x, out y, out z);
        }

        // Implement the GetProcessedAcceleration function
        public void GetProcessedAcceleration(out float x, out float y, out float z)
        {
            GetProcessedAcceleration(handle, out x, out y, out z);
        }

        // Implement the GetOrientation function
        public void GetOrientation(out float w, out float x, out float y, out float z)
        {
            GetOrientation(handle, out w, out x, out y, out z);
        }

        // Implement the GetPlayerSpaceGyro function
        public void GetPlayerSpaceGyro(out float x, out float y, float yawRelaxFactor)
        {
            GetPlayerSpaceGyro(handle, out x, out y, yawRelaxFactor);
        }

        // Implement the GetWorldSpaceGyro function
        public void GetWorldSpaceGyro(out float x, out float y, float sideReductionThreshold)
        {
            GetWorldSpaceGyro(handle, out x, out y, sideReductionThreshold);
        }

        // Implement the StartContinuousCalibration function
        public void StartContinuousCalibration()
        {
            StartContinuousCalibration(handle);
        }

        // Implement the PauseContinuousCalibration function
        public void PauseContinuousCalibration()
        {
            PauseContinuousCalibration(handle);
        }

        // Implement the ResetContinuousCalibration function
        public void ResetContinuousCalibration()
        {
            ResetContinuousCalibration(handle);
        }

        // Implement the GetCalibrationOffset function
        public void GetCalibrationOffset(out float xOffset, out float yOffset, out float zOffset)
        {
            GetCalibrationOffset(handle, out xOffset, out yOffset, out zOffset);
        }

        // Implement the SetCalibrationOffset function
        public void SetCalibrationOffset(float xOffset, float yOffset, float zOffset, int weight)
        {
            calibration.xOffset = xOffset;
            calibration.yOffset = yOffset;
            calibration.zOffset = zOffset;
            calibration.weight = weight;

            SetCalibrationOffset(handle, xOffset, yOffset, zOffset, weight);
        }

        // Implement the GetAutoCalibrationConfidence function
        public float GetAutoCalibrationConfidence()
        {
            return GetAutoCalibrationConfidence(handle);
        }

        // Implement the SetAutoCalibrationConfidence function
        public void SetAutoCalibrationConfidence(float newConfidence)
        {
            SetAutoCalibrationConfidence(handle, newConfidence);
        }

        // Implement the GetAutoCalibrationIsSteady function
        public bool GetAutoCalibrationIsSteady()
        {
            return GetAutoCalibrationIsSteady(handle);
        }

        // Implement the GetCalibrationMode function
        public CalibrationMode GetCalibrationMode()
        {
            return GetCalibrationMode(handle);
        }

        // Implement the SetCalibrationMode function
        public void SetCalibrationMode(CalibrationMode calibrationMode)
        {
            SetCalibrationMode(handle, calibrationMode);
        }

        public void StartThresholdCalibration()
        {
            thresholdCalibration = true;
        }

        public void PauseThresholdCalibration()
        {
            thresholdCalibration = false;
        }

        public void ResetThresholdCalibration()
        {
            maxGyro = minGyro;
            maxAccel = minAccel;
        }

        public void SetCalibrationThreshold(float gyroTreshold, float accelThreshold)
        {
            calibration.SetGyroThreshold(Math.Max(minGyro, gyroTreshold));
            calibration.SetAcceleroThreshold(Math.Max(minAccel, accelThreshold));
        }

        // Implement the ResetMotion function
        public void ResetMotion()
        {
            ResetMotion(handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (handle != IntPtr.Zero)
            {
                DeleteGamepadMotion(handle);
                handle = IntPtr.Zero;
            }
        }

        [DllImport(DllName)]
        private static extern IntPtr CreateGamepadMotion();

        [DllImport(DllName)]
        private static extern void DeleteGamepadMotion(IntPtr motion);

        [DllImport(DllName)]
        private static extern void ResetGamepadMotion(IntPtr motion);

        [DllImport(DllName)]
        private static extern void ProcessMotion(IntPtr motion, float gyroX, float gyroY, float gyroZ, float accelX, float accelY, float accelZ, float deltaTime);

        [DllImport(DllName)]
        private static extern void GetCalibratedGyro(IntPtr motion, out float x, out float y, out float z);

        [DllImport(DllName)]
        private static extern void GetGravity(IntPtr motion, out float x, out float y, out float z);

        [DllImport(DllName)]
        private static extern void GetProcessedAcceleration(IntPtr motion, out float x, out float y, out float z);

        [DllImport(DllName)]
        private static extern void GetOrientation(IntPtr motion, out float w, out float x, out float y, out float z);

        [DllImport(DllName)]
        private static extern void GetPlayerSpaceGyro(IntPtr motion, out float x, out float y, float yawRelaxFactor);

        [DllImport(DllName)]
        private static extern void GetWorldSpaceGyro(IntPtr motion, out float x, out float y, float sideReductionThreshold);

        [DllImport(DllName)]
        private static extern void StartContinuousCalibration(IntPtr motion);

        [DllImport(DllName)]
        private static extern void PauseContinuousCalibration(IntPtr motion);

        [DllImport(DllName)]
        private static extern void ResetContinuousCalibration(IntPtr motion);

        [DllImport(DllName)]
        private static extern void GetCalibrationOffset(IntPtr motion, out float xOffset, out float yOffset, out float zOffset);

        [DllImport(DllName)]
        private static extern void SetCalibrationOffset(IntPtr motion, float xOffset, float yOffset, float zOffset, int weight);

        [DllImport(DllName)]
        private static extern float GetAutoCalibrationConfidence(IntPtr motion);

        [DllImport(DllName)]
        private static extern void SetAutoCalibrationConfidence(IntPtr motion, float newConfidence);

        [DllImport(DllName)]
        private static extern bool GetAutoCalibrationIsSteady(IntPtr motion);

        [DllImport(DllName)]
        private static extern CalibrationMode GetCalibrationMode(IntPtr motion);

        [DllImport(DllName)]
        private static extern void SetCalibrationMode(IntPtr motion, CalibrationMode calibrationMode);

        [DllImport(DllName)]
        private static extern void ResetMotion(IntPtr motion);
    }
}
