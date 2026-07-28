using System.Numerics;

namespace Balancer.Core.Domain;

/// <summary>Complex 1X vibration responses at sensor positions A and B.</summary>
public readonly record struct VibrationVector(Complex AtSensorA, Complex AtSensorB)
{
    public Complex this[int sensorIndex] => sensorIndex switch
    {
        0 => AtSensorA,
        1 => AtSensorB,
        _ => throw new ArgumentOutOfRangeException(nameof(sensorIndex))
    };

    public VibrationVector Add(VibrationVector other) => new(AtSensorA + other.AtSensorA, AtSensorB + other.AtSensorB);
    public static VibrationVector operator -(VibrationVector value) => new(-value.AtSensorA, -value.AtSensorB);
    public double Magnitude => Math.Sqrt(Complex.Abs(AtSensorA) * Complex.Abs(AtSensorA) + Complex.Abs(AtSensorB) * Complex.Abs(AtSensorB));
}
