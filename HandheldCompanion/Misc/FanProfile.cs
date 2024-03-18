using HandheldCompanion.Devices;
using HandheldCompanion.Views;
using System;
using System.Linq;

namespace HandheldCompanion.Misc
{
    public enum FanMode
    {
        Hardware = 0,
        Software = 1,
    }

    [Serializable]
    public class FanProfile
    {
        public FanMode fanMode = FanMode.Hardware;

        //                            00, 10, 20, 30, 40, 50, 60, 70, 80, 90,  100°C
        public double[] fanSpeeds = { 20, 20, 20, 30, 40, 50, 70, 80, 90, 100, 100 };

        // A private variable to store the average temperature
        private ConcurrentList<double> avgTemp;

        // A private variable to store the aggressivity level
        private int aggressivity;

        // A public constructor
        public FanProfile()
        {
            // Initialize the average temperature to zero
            this.avgTemp = new();
        }

        // A public constructor that takes an array of fan speed values as a parameter
        public FanProfile(double[] fanSpeeds, int aggressivity) : this()
        {
            SetFanSpeed(fanSpeeds);

            // Assign the aggressivity level to the private fields
            this.aggressivity = aggressivity;
        }

        // A public method that takes an aggressivity level as a parameter and updates the aggressivity variable
        public void SetAggressivity(int aggressivity)
        {
            // Check if the aggressivity level is within the range of 1 to 4
            if (aggressivity < 1 || aggressivity > 4)
            {
                throw new ArgumentException("Aggressivity level must be within the range of 1 to 4");
            }

            // Update the private field with the input value
            this.aggressivity = aggressivity;
        }

        // A public method that returns the fan speed based on average temperature by linear interpolation
        public double GetFanSpeed()
        {
            double average = GetAverageTemperature();
            return GetFanSpeed(average);
        }

        // A public method that takes a temperature as a parameter and returns the corresponding fan speed by linear interpolation
        private bool TjmaxReached = false;
        public double GetFanSpeed(double temp)
        {
            // Check if the temperature is within the °C range of Tjmax
            if (temp >= IDevice.GetCurrent().Tjmax)
                TjmaxReached = true;

            if (TjmaxReached)
            {
                if (temp <= 80)
                    TjmaxReached = false;
                else
                    return 100.0d;
            }

            // Find the two closest points that bracket the temperature
            int low = (int)Math.Floor(temp / fanSpeeds.Length);

            // Add a condition to prevent high from being out of bounds
            int high = low < (fanSpeeds.Length - 1) ? low + 1 : low;

            // Linearly interpolate the fan speed value between the two closest points
            double slope = (fanSpeeds[high] - fanSpeeds[low]) / fanSpeeds.Length;
            double intercept = fanSpeeds[low] - slope * low * fanSpeeds.Length;
            return slope * temp + intercept;
        }

        // A public method that takes an array of fan speed values as a parameter and updates the fanSpeeds variable
        public void SetFanSpeed(double[] fanSpeeds)
        {
            // Check if the array has the length of fanSpeeds and is not empty
            if (fanSpeeds.Length != this.fanSpeeds.Length || fanSpeeds.Length == 0)
            {
                throw new ArgumentException("Invalid input array");
            }

            // Check if the array is sorted in ascending order
            for (int i = 0; i < fanSpeeds.Length - 1; i++)
            {
                if (fanSpeeds[i] > fanSpeeds[i + 1])
                {
                    throw new ArgumentException("Input array must be sorted in ascending order");
                }
            }

            // Check if the fan speed values are within the range of 0 to 100
            if (fanSpeeds[0] < 0 || fanSpeeds[fanSpeeds.Length - 1] > 100)
            {
                throw new ArgumentException("Fan speed values must be within the range of 0 to 100");
            }

            // Update the private field with the input array
            this.fanSpeeds = fanSpeeds;
        }

        // A public method that takes a temperature as a parameter and updates the avgTemp variable
        public void SetTemperature(double temp)
        {
            // Check if the temperature is within the range of 0°C to 100°C
            if (temp < 0 || temp > 100)
                return;

            // Update the average temperature using a weighted average formula based on the aggressivity level
            if (this.avgTemp.Count > aggressivity)
                this.avgTemp.RemoveAt(aggressivity);

            this.avgTemp.Add(temp);
        }

        // A public method that returns the average temperature
        public double GetAverageTemperature()
        {
            if (this.avgTemp.Count != 0)
                return this.avgTemp.Average();

            return 50.0d;
        }
    }
}
