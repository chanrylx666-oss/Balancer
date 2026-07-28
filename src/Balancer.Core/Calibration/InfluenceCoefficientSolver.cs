using System.Numerics;
using Balancer.Core.Domain;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Core.Calibration;

public sealed class InfluenceCoefficientSolver
{
    private const double DeterminantTolerance = 1e-12;

    public CalibrationResult Calibrate(
        VibrationVector baseline,
        TrialWeight trialA,
        MeasurementRun afterTrialA,
        TrialWeight trialB,
        MeasurementRun afterTrialB)
    {
        ArgumentNullException.ThrowIfNull(trialA);
        ArgumentNullException.ThrowIfNull(afterTrialA);
        ArgumentNullException.ThrowIfNull(trialB);
        ArgumentNullException.ThrowIfNull(afterTrialB);

        try
        {
            trialA.Validate();
            trialB.Validate();
            afterTrialA.Validate();
            afterTrialB.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new CalibrationException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new CalibrationException(exception.Message);
        }

        if (trialA.Plane != Plane.A || trialB.Plane != Plane.B)
            throw new CalibrationException("Calibration requires an isolated trial weight on plane A, followed by plane B.");

        var trialAValue = trialA.Unbalance.ToComplex();
        var trialBValue = trialB.Unbalance.ToComplex();
        var responseA = Subtract(afterTrialA.Vibration, baseline);
        var responseB = Subtract(afterTrialB.Vibration, baseline);

        if (responseA.Magnitude <= DeterminantTolerance || responseB.Magnitude <= DeterminantTolerance)
            throw new CalibrationException("Trial weight response is too small for a reliable calibration.");

        var matrix = new InfluenceCoefficientMatrix(
            responseA.AtSensorA / trialAValue,
            responseB.AtSensorA / trialBValue,
            responseA.AtSensorB / trialAValue,
            responseB.AtSensorB / trialBValue);

        EnsureInvertible(matrix);
        return new CalibrationResult(matrix, matrix.ConditionNumber);
    }

    public CorrectionRecommendation Solve(InfluenceCoefficientMatrix matrix, VibrationVector baseline, double planeARadiusMillimeters, double planeBRadiusMillimeters)
    {
        ValidateRadius(planeARadiusMillimeters, nameof(planeARadiusMillimeters));
        ValidateRadius(planeBRadiusMillimeters, nameof(planeBRadiusMillimeters));
        EnsureInvertible(matrix);

        var determinant = matrix.Determinant;
        var correctionA = (-matrix.H22 * baseline.AtSensorA + matrix.H12 * baseline.AtSensorB) / determinant;
        var correctionB = (matrix.H21 * baseline.AtSensorA - matrix.H11 * baseline.AtSensorB) / determinant;
        var residual = matrix.Multiply(new[] { correctionA, correctionB }).Add(baseline);

        return new CorrectionRecommendation(
            new PlaneCorrection(Plane.A, UnbalanceVector.FromComplex(correctionA), planeARadiusMillimeters),
            new PlaneCorrection(Plane.B, UnbalanceVector.FromComplex(correctionB), planeBRadiusMillimeters),
            residual.Magnitude,
            matrix.ConditionNumber);
    }

    private static VibrationVector Subtract(VibrationVector left, VibrationVector right)
        => new(left.AtSensorA - right.AtSensorA, left.AtSensorB - right.AtSensorB);

    private static void EnsureInvertible(InfluenceCoefficientMatrix matrix)
    {
        if (!double.IsFinite(matrix.Determinant.Real) || !double.IsFinite(matrix.Determinant.Imaginary) || Complex.Abs(matrix.Determinant) <= DeterminantTolerance)
            throw new CalibrationException("Influence coefficient matrix is singular or ill-conditioned.");
    }

    private static void ValidateRadius(double radius, string parameterName)
    {
        if (!double.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(parameterName, "Correction radius must be positive.");
    }
}
