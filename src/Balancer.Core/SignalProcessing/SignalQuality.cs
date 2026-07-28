namespace Balancer.Core.SignalProcessing;

public enum SignalQualityCode
{
    Valid,
    InsufficientTachPulses,
    ExcessiveSpeedVariation,
    InsufficientSamples
}

public sealed record SignalQuality(SignalQualityCode Code, string Message)
{
    public bool IsValid => Code == SignalQualityCode.Valid;

    public static SignalQuality Valid { get; } = new(SignalQualityCode.Valid, "Signal quality is acceptable.");
}
