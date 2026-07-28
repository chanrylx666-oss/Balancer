using System.Numerics;
using Balancer.Core.Calibration;
using Balancer.Core.Domain;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Core.Tests;

public sealed class CalibrationSessionTests
{
    [Fact]
    public void Record_requiresBaselineThenPlaneAThenPlaneB()
    {
        var session = new CalibrationSession();
        var run = new MeasurementRun(new VibrationVector(Complex.One, Complex.One), 1500, DataQuality.Good);
        var trialA = new TrialWeight(Plane.A, 10, 25, Angle.Zero);
        var trialB = new TrialWeight(Plane.B, 10, 25, Angle.Zero);

        Assert.Throws<InvalidOperationException>(() => session.RecordTrial(Plane.A, trialA, run));
        session.RecordBaseline(run);
        session.RecordTrial(Plane.A, trialA, run);
        session.RecordTrial(Plane.B, trialB, run);

        Assert.True(session.IsReady);
    }

    [Fact]
    public void BuildCalibration_rejectsExcessiveRpmDeviation()
    {
        var session = new CalibrationSession(maximumRpmDeviationPercent: 1);
        var run = new MeasurementRun(new VibrationVector(Complex.One, Complex.One), 1500, DataQuality.Good);
        session.RecordBaseline(run);
        session.RecordTrial(Plane.A, new TrialWeight(Plane.A, 10, 25, Angle.Zero), run);
        session.RecordTrial(Plane.B, new TrialWeight(Plane.B, 10, 25, Angle.Zero), run with { Rpm = 1530 });

        Assert.Throws<CalibrationException>(() => session.BuildCalibration(new InfluenceCoefficientSolver()));
    }
}
