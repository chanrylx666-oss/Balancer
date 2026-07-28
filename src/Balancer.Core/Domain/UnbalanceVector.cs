using System.Numerics;

namespace Balancer.Core.Domain;

/// <summary>Unbalance expressed in g·mm and referenced to the tachometer phase.</summary>
public readonly record struct UnbalanceVector
{
    public UnbalanceVector(double gramMillimeters, Angle angle)
    {
        if (!double.IsFinite(gramMillimeters) || gramMillimeters < 0)
            throw new ArgumentOutOfRangeException(nameof(gramMillimeters));

        GramMillimeters = gramMillimeters;
        Angle = angle;
    }

    public double GramMillimeters { get; }
    public Angle Angle { get; }

    public Complex ToComplex() => Complex.FromPolarCoordinates(GramMillimeters, Angle.Radians);

    public static UnbalanceVector FromComplex(Complex value)
    {
        if (!double.IsFinite(value.Real) || !double.IsFinite(value.Imaginary))
            throw new ArgumentOutOfRangeException(nameof(value));

        return new UnbalanceVector(Complex.Abs(value), Angle.FromRadians(value.Phase));
    }
}
