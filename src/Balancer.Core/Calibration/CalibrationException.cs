namespace Balancer.Core.Calibration;

public sealed class CalibrationException(string message) : InvalidOperationException(message);
