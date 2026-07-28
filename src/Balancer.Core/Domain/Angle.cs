namespace Balancer.Core.Domain;

/// <summary>Mechanical angle referenced to the tachometer rising edge; rotation direction is positive.</summary>
public readonly record struct Angle
{
    private Angle(double degrees) => Degrees = Normalize(degrees);

    public double Degrees { get; }
    public double Radians => Degrees * Math.PI / 180d;
    public static Angle Zero => new(0);

    public static Angle FromDegrees(double degrees)
    {
        if (!double.IsFinite(degrees))
            throw new ArgumentOutOfRangeException(nameof(degrees));

        return new Angle(degrees);
    }

    public static Angle FromRadians(double radians)
    {
        if (!double.IsFinite(radians))
            throw new ArgumentOutOfRangeException(nameof(radians));

        return FromDegrees(radians * 180d / Math.PI);
    }

    private static double Normalize(double degrees)
    {
        var normalized = degrees % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }
}
