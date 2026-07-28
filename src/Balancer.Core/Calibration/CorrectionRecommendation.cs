using Balancer.Core.Domain;
using Plane = Balancer.Core.Domain.Plane;

namespace Balancer.Core.Calibration;

public sealed record PlaneCorrection(Plane Plane, UnbalanceVector Unbalance, double RadiusMillimeters)
{
    public double MassGrams => Unbalance.GramMillimeters / RadiusMillimeters;
}

public sealed record CorrectionRecommendation(PlaneCorrection PlaneA, PlaneCorrection PlaneB, double ResidualMagnitude, double ConditionNumber);
