using System;
using System.Numerics;

namespace HandheldCompanion;

// MadgwickAHRS class. Implementation of Madgwick's IMU and AHRS algorithms.
// From: http://www.x-io.co.uk/node/8#open_source_ahrs_and_imu_algorithms
//
// Optimised for minimal arithmetic.
// Total ±: 45
// Total *: 85
// Total /: 3
// Total sqrt: 3
//
// Requires only gyroscope and accelerometer data.
// Gyro in rad/s
// Accelerometer measurement in any calibrated units.

public class MadgwickAHRS
{
    public MadgwickAHRS(float samplePeriod) : this(samplePeriod, 1f) { }

    // Sample period
    // Beta, algorithm gain beta.
    public MadgwickAHRS(float samplePeriod, float beta = 0f)
    {
        SamplePeriod = samplePeriod;
        Beta = beta;
        Quaternion = new[] { 0f, 0f, -1f, 0f };
    }

    public void Reset()
    {
        Quaternion = new[] { 0f, 0f, -1f, 0f };
    }

    public float SamplePeriod { get; set; }
    public float Beta { get; set; }
    public float[] Quaternion { get; set; }

    public void UpdateReport(float gx, float gy, float gz, float ax, float ay, float az, double DeltaSeconds)
    {
        SamplePeriod = (float)DeltaSeconds;

        float q1 = Quaternion[0],
              q2 = Quaternion[1],
              q3 = Quaternion[2],
              q4 = Quaternion[3]; // short name local variable for readability
        float norm;
        float s1, s2, s3, s4;
        float qDot1, qDot2, qDot3, qDot4;

        // Auxiliary variables to avoid repeated arithmetic
        var _2q1 = 2f * q1;
        var _2q2 = 2f * q2;
        var _2q3 = 2f * q3;
        var _2q4 = 2f * q4;
        var _4q1 = 4f * q1;
        var _4q2 = 4f * q2;
        var _4q3 = 4f * q3;
        var _8q2 = 8f * q2;
        var _8q3 = 8f * q3;
        var q1q1 = q1 * q1;
        var q2q2 = q2 * q2;
        var q3q3 = q3 * q3;
        var q4q4 = q4 * q4;

        // Normalise accelerometer measurement
        norm = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
        if (norm == 0f) return; // handle NaN
        norm = 1 / norm; // use reciprocal for division
        ax *= norm;
        ay *= norm;
        az *= norm;

        // Gradient decent algorithm corrective step
        s1 = _4q1 * q3q3 + _2q3 * ax + _4q1 * q2q2 - _2q2 * ay;
        s2 = _4q2 * q4q4 - _2q4 * ax + 4f * q1q1 * q2 - _2q1 * ay - _4q2 + _8q2 * q2q2 + _8q2 * q3q3 + _4q2 * az;
        s3 = 4f * q1q1 * q3 + _2q1 * ax + _4q3 * q4q4 - _2q4 * ay - _4q3 + _8q3 * q2q2 + _8q3 * q3q3 + _4q3 * az;
        s4 = 4f * q2q2 * q4 - _2q2 * ax + 4f * q3q3 * q4 - _2q3 * ay;
        norm = 1f / (float)Math.Sqrt(s1 * s1 + s2 * s2 + s3 * s3 + s4 * s4); // normalise step magnitude
        s1 *= norm;
        s2 *= norm;
        s3 *= norm;
        s4 *= norm;

        // Compute rate of change of quaternion
        qDot1 = 0.5f * (-q2 * gx - q3 * gy - q4 * gz) - Beta * s1;
        qDot2 = 0.5f * (q1 * gx + q3 * gz - q4 * gy) - Beta * s2;
        qDot3 = 0.5f * (q1 * gy - q2 * gz + q4 * gx) - Beta * s3;
        qDot4 = 0.5f * (q1 * gz + q2 * gy - q3 * gx) - Beta * s4;

        // Integrate to yield quaternion
        q1 += qDot1 * SamplePeriod;
        q2 += qDot2 * SamplePeriod;
        q3 += qDot3 * SamplePeriod;
        q4 += qDot4 * SamplePeriod;
        norm = 1f / (float)Math.Sqrt(q1 * q1 + q2 * q2 + q3 * q3 + q4 * q4); // normalise quaternion
        Quaternion[0] = q1 * norm;
        Quaternion[1] = q2 * norm;
        Quaternion[2] = q3 * norm;
        Quaternion[3] = q4 * norm;
    }

    public Quaternion GetQuaternion()
    {
        return new Quaternion(Quaternion[0], Quaternion[1], Quaternion[2], Quaternion[3]);
    }

}