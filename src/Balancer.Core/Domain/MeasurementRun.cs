namespace Balancer.Core.Domain;

public sealed record MeasurementRun(VibrationVector Vibration, double Rpm, DataQuality Quality)
{
    public void Validate()
    {
        if (!double.IsFinite(Rpm) || Rpm <= 0)
            throw new ArgumentOutOfRangeException(nameof(Rpm), "RPM must be positive.");
        if (Quality == DataQuality.Rejected)
            throw new InvalidOperationException("A rejected measurement cannot be used for calibration.");
    }
}
