using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace HandheldCompanion.Sensors
{
    public class IMUCalibration
    {
        private static Dictionary<string, IMUCalibration> Calibrations = new();
        public static string CalibrationPath = Path.Combine(MainWindow.SettingsPath, "calibration.json");

        public float xOffset;
        public float yOffset;
        public float zOffset;
        public int weight;
        public float thresholdG = 2000.0f;    // default °/s, based on DualShock4
        public float thresholdA = 4.0f;       // default g, based on DualShock4

        public IMUCalibration()
        { }

        static IMUCalibration()
        {
            // initialiaze path
            CalibrationPath = Path.Combine(MainWindow.SettingsPath, "calibration.json");
            Calibrations = DeserializeCollection();
        }

        public IMUCalibration(float xOffset, float yOffset, float zOffset, int weight)
        {
            this.xOffset = xOffset;
            this.yOffset = yOffset;
            this.zOffset = zOffset;
            this.weight = weight;
        }

        public void SetGyroThreshold(float thresholdG)
        {
            this.thresholdG = thresholdG;
        }

        public void SetAcceleroThreshold(float thresholdA)
        {
            this.thresholdA = thresholdA;
        }

        public float GetGyroThreshold()
        {
            return thresholdG;
        }

        public float GetAcceleroThreshold()
        {
            return thresholdA;
        }

        public static void SerializeCollection(Dictionary<string, IMUCalibration> collection)
        {
            string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            File.WriteAllText(CalibrationPath, json);
        }

        public static Dictionary<string, IMUCalibration> DeserializeCollection()
        {
            if (!File.Exists(CalibrationPath))
                return new();

            string json = File.ReadAllText(CalibrationPath);
            return JsonConvert.DeserializeObject<Dictionary<string, IMUCalibration>>(json);
        }

        public static IMUCalibration GetCalibration(string path)
        {
            if (Calibrations.TryGetValue(path, out IMUCalibration calibration))
            {
                LogManager.LogDebug("Restored calibration offsets for device: {0}", path);
                return calibration;
            }

            LogManager.LogDebug("No calibration offsets available for device: {0}", path);
            return new();
        }

        public static void StoreCalibration(string path, IMUCalibration calibration)
        {
            // upcase
            path = path.ToUpper();

            // update array
            Calibrations[path] = calibration;
            LogManager.LogDebug("Updated calibration offsets for device: {0}", path);

            // serialize to calibration.json
            SerializeCollection(Calibrations);
        }
    }
}
