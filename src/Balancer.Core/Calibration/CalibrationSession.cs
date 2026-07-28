using Balancer.Core.Domain;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Core.Calibration;

/// <summary>Enforces the safe baseline → plane A trial → plane B trial workflow.</summary>
public sealed class CalibrationSession(double maximumRpmDeviationPercent = 2d)
{
    private MeasurementRun? _baseline;
    private (TrialWeight Trial, MeasurementRun Run)? _trialA;
    private (TrialWeight Trial, MeasurementRun Run)? _trialB;

    public bool IsReady => _baseline is not null && _trialA is not null && _trialB is not null;
    public CalibrationStep NextStep => _baseline is null ? CalibrationStep.Baseline : _trialA is null ? CalibrationStep.TrialPlaneA : _trialB is null ? CalibrationStep.TrialPlaneB : CalibrationStep.Complete;

    public void RecordBaseline(MeasurementRun run)
    {
        if (NextStep != CalibrationStep.Baseline) throw new InvalidOperationException("Baseline has already been recorded.");
        run.Validate();
        _baseline = run;
    }

    public void RecordTrial(Plane plane, TrialWeight trial, MeasurementRun run)
    {
        if ((plane == Plane.A && NextStep != CalibrationStep.TrialPlaneA) ||
            (plane == Plane.B && NextStep != CalibrationStep.TrialPlaneB))
            throw new InvalidOperationException("Trials must be recorded in plane A then plane B order, with the previous trial removed.");
        if (trial.Plane != plane) throw new ArgumentException("Trial plane does not match the requested step.", nameof(trial));
        trial.Validate();
        run.Validate();
        if (plane == Plane.A) _trialA = (trial, run); else _trialB = (trial, run);
    }

    public CalibrationResult BuildCalibration(InfluenceCoefficientSolver solver)
    {
        ArgumentNullException.ThrowIfNull(solver);
        if (!IsReady) throw new CalibrationException("Baseline and both isolated trial runs are required.");
        var baseline = _baseline!;
        EnsureRpmMatches(baseline.Rpm, _trialA!.Value.Run.Rpm);
        EnsureRpmMatches(baseline.Rpm, _trialB!.Value.Run.Rpm);
        return solver.Calibrate(baseline.Vibration, _trialA.Value.Trial, _trialA.Value.Run, _trialB.Value.Trial, _trialB.Value.Run);
    }

    private void EnsureRpmMatches(double baselineRpm, double comparisonRpm)
    {
        var deviation = Math.Abs(comparisonRpm - baselineRpm) / baselineRpm * 100d;
        if (deviation > maximumRpmDeviationPercent)
            throw new CalibrationException($"RPM deviation ({deviation:F2}%) exceeds the allowed {maximumRpmDeviationPercent:F2}%.");
    }
}

public enum CalibrationStep { Baseline, TrialPlaneA, TrialPlaneB, Complete }
