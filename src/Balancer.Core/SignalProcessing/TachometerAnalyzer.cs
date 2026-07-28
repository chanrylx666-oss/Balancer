using Balancer.Core.Acquisition;

namespace Balancer.Core.SignalProcessing;

public sealed record TachometerAnalysis(double Rpm, double SpeedVariation, IReadOnlyList<DateTimeOffset> PulseTimes, SignalQuality Quality);

/// <summary>Derives rotational speed and stability from rising keyphasor edges.</summary>
public sealed class TachometerAnalyzer
{
    private readonly double _threshold;
    private readonly double _maximumSpeedVariation;

    public TachometerAnalyzer(double threshold = 0.5, double maximumSpeedVariation = 0.03)
    {
        if (maximumSpeedVariation < 0) throw new ArgumentOutOfRangeException(nameof(maximumSpeedVariation));
        _threshold = threshold;
        _maximumSpeedVariation = maximumSpeedVariation;
    }

    public TachometerAnalysis Analyze(IReadOnlyList<SignalSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var pulses = new List<DateTimeOffset>();
        var wasHigh = false;
        foreach (var sample in samples)
        {
            var isHigh = sample.Value >= _threshold;
            if (isHigh && !wasHigh) pulses.Add(sample.Timestamp);
            wasHigh = isHigh;
        }

        if (pulses.Count < 2)
        {
            return new TachometerAnalysis(0, 0, pulses,
                new SignalQuality(SignalQualityCode.InsufficientTachPulses, "At least two keyphasor pulses are required."));
        }

        var intervals = pulses.Zip(pulses.Skip(1), (first, second) => (second - first).TotalSeconds).ToArray();
        if (intervals.Any(interval => interval <= 0))
        {
            return new TachometerAnalysis(0, 0, pulses,
                new SignalQuality(SignalQualityCode.InsufficientTachPulses, "Keyphasor timestamps must be increasing."));
        }

        var mean = intervals.Average();
        var standardDeviation = Math.Sqrt(intervals.Average(interval => Math.Pow(interval - mean, 2)));
        var variation = standardDeviation / mean;
        var rpm = 60.0 / mean;
        var quality = variation <= _maximumSpeedVariation
            ? SignalQuality.Valid
            : new SignalQuality(SignalQualityCode.ExcessiveSpeedVariation,
                $"Speed variation {variation:P2} exceeds the allowed {_maximumSpeedVariation:P2}.");
        return new TachometerAnalysis(rpm, variation, pulses, quality);
    }
}
