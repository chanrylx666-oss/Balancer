using Balancer.Core.Domain;

namespace Balancer.Core.Calibration;

public sealed record CalibrationResult(InfluenceCoefficientMatrix Matrix, double ConditionNumber);
