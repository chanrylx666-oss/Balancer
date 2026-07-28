using System.Numerics;

namespace Balancer.Core.Domain;

/// <summary>Maps correction-plane unbalance vectors [A, B] to sensor vibration vectors [A, B].</summary>
public readonly record struct InfluenceCoefficientMatrix(Complex H11, Complex H12, Complex H21, Complex H22)
{
    public Complex Determinant => H11 * H22 - H12 * H21;

    public VibrationVector Multiply(UnbalanceVector planeA, UnbalanceVector planeB)
        => Multiply(new[] { planeA.ToComplex(), planeB.ToComplex() });

    public VibrationVector Multiply(IReadOnlyList<Complex> unbalance)
    {
        ArgumentNullException.ThrowIfNull(unbalance);
        if (unbalance.Count != 2)
            throw new ArgumentException("Exactly two plane vectors are required.", nameof(unbalance));

        return new VibrationVector(
            H11 * unbalance[0] + H12 * unbalance[1],
            H21 * unbalance[0] + H22 * unbalance[1]);
    }

    public double ConditionNumber
    {
        get
        {
            var determinantMagnitude = Complex.Abs(Determinant);
            if (determinantMagnitude == 0)
                return double.PositiveInfinity;

            var norm = FrobeniusNorm(H11, H12, H21, H22);
            var inverseNorm = FrobeniusNorm(H22 / Determinant, -H12 / Determinant, -H21 / Determinant, H11 / Determinant);
            return norm * inverseNorm;
        }
    }

    private static double FrobeniusNorm(params Complex[] values) => Math.Sqrt(values.Sum(v => Complex.Abs(v) * Complex.Abs(v)));
}
