namespace Balancer.Core.Domain;

public sealed record TrialWeight(Plane Plane, double MassGrams, double RadiusMillimeters, Angle Angle)
{
    public UnbalanceVector Unbalance => new(MassGrams * RadiusMillimeters, Angle);

    public void Validate()
    {
        if (!double.IsFinite(MassGrams) || MassGrams <= 0)
            throw new ArgumentOutOfRangeException(nameof(MassGrams), "Trial mass must be positive.");
        if (!double.IsFinite(RadiusMillimeters) || RadiusMillimeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(RadiusMillimeters), "Trial radius must be positive.");
    }
}
