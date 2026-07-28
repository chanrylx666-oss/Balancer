using System.Numerics;
using Balancer.Core.Calibration;
using Balancer.Core.Domain;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Core.Tests;

public sealed class InfluenceCoefficientSolverTests
{
    private readonly InfluenceCoefficientSolver _solver = new();

    [Fact]
    public void Solve_idealMatrix_recoversKnownCorrectionVectors()
    {
        var matrix = new InfluenceCoefficientMatrix(
            new Complex(2.0, 0.5), new Complex(-0.4, 1.0),
            new Complex(0.7, -1.2), new Complex(1.8, 0.3));
        var expectedCorrection = new[]
        {
            Complex.FromPolarCoordinates(120, Angle.FromDegrees(35).Radians),
            Complex.FromPolarCoordinates(80, Angle.FromDegrees(-110).Radians)
        };
        var baseline = -matrix.Multiply(expectedCorrection);

        var result = _solver.Solve(matrix, new VibrationVector(baseline[0], baseline[1]), 50, 80);

        AssertClose(expectedCorrection[0], result.PlaneA.Unbalance.ToComplex());
        AssertClose(expectedCorrection[1], result.PlaneB.Unbalance.ToComplex());
        Assert.True(result.ResidualMagnitude < 1e-9);
    }

    [Fact]
    public void Calibrate_validTrials_reconstructsInfluenceMatrixAndSolvesCorrection()
    {
        var expectedMatrix = new InfluenceCoefficientMatrix(
            new Complex(1.2, -0.3), new Complex(-0.5, 0.8),
            new Complex(0.4, 0.7), new Complex(1.6, -0.2));
        var original = new[] { new Complex(18, 4), new Complex(-7, 11) };
        var trialA = new TrialWeight(Plane.A, 10, 25, Angle.FromDegrees(20));
        var trialB = new TrialWeight(Plane.B, 12, 30, Angle.FromDegrees(145));
        var baseline = expectedMatrix.Multiply(original);
        var runA = new MeasurementRun(
            baseline.Add(expectedMatrix.Multiply(trialA.Unbalance, new UnbalanceVector(0, Angle.Zero))), 1500, DataQuality.Good);
        var runB = new MeasurementRun(
            baseline.Add(expectedMatrix.Multiply(new UnbalanceVector(0, Angle.Zero), trialB.Unbalance)), 1500, DataQuality.Good);

        var calibration = _solver.Calibrate(baseline, trialA, runA, trialB, runB);
        var correction = _solver.Solve(calibration.Matrix, baseline, 25, 30);

        AssertClose(expectedMatrix.H11, calibration.Matrix.H11);
        AssertClose(expectedMatrix.H12, calibration.Matrix.H12);
        AssertClose(expectedMatrix.H21, calibration.Matrix.H21);
        AssertClose(expectedMatrix.H22, calibration.Matrix.H22);
        AssertClose(-original[0], correction.PlaneA.Unbalance.ToComplex());
        AssertClose(-original[1], correction.PlaneB.Unbalance.ToComplex());
    }

    [Fact]
    public void Solve_singularMatrix_throwsCalibrationException()
    {
        var singular = new InfluenceCoefficientMatrix(Complex.One, Complex.One, Complex.One, Complex.One);

        Assert.Throws<CalibrationException>(() =>
            _solver.Solve(singular, new VibrationVector(Complex.One, Complex.One), 20, 20));
    }

    [Fact]
    public void Calibrate_zeroTrialWeight_throwsCalibrationException()
    {
        var baseline = new VibrationVector(Complex.One, Complex.One);
        var invalidTrial = new TrialWeight(Plane.A, 0, 20, Angle.Zero);
        var validTrial = new TrialWeight(Plane.B, 10, 20, Angle.Zero);
        var run = new MeasurementRun(baseline, 1500, DataQuality.Good);

        Assert.Throws<CalibrationException>(() =>
            _solver.Calibrate(baseline, invalidTrial, run, validTrial, run));
    }

    [Fact]
    public void Calibrate_rejectedMeasurement_throwsCalibrationException()
    {
        var baseline = new VibrationVector(Complex.One, Complex.One);
        var trialA = new TrialWeight(Plane.A, 10, 20, Angle.Zero);
        var trialB = new TrialWeight(Plane.B, 10, 20, Angle.Zero);
        var poorRun = new MeasurementRun(baseline, 1500, DataQuality.Rejected);

        Assert.Throws<CalibrationException>(() =>
            _solver.Calibrate(baseline, trialA, poorRun, trialB, poorRun));
    }

    private static void AssertClose(Complex expected, Complex actual, double tolerance = 1e-9)
        => Assert.True(Complex.Abs(expected - actual) < tolerance,
            $"Expected {expected}, actual {actual}.");
}
