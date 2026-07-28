using System.Numerics;
using Balancer.Core.Acquisition;

namespace Balancer.Core.SignalProcessing;

public sealed record OneXAnalysis(Complex Vector, double Amplitude, double PhaseRadians, int RevolutionCount, SignalQuality Quality);

/// <summary>Calculates the synchronous 1X vector by averaging a DFT over complete tachometer revolutions.</summary>
public sealed class SynchronousVibrationAnalyzer
{
    private readonly int _minimumSamplesPerRevolution;

    public SynchronousVibrationAnalyzer(int minimumSamplesPerRevolution = 32)
    {
        if (minimumSamplesPerRevolution < 3) throw new ArgumentOutOfRangeException(nameof(minimumSamplesPerRevolution));
        _minimumSamplesPerRevolution = minimumSamplesPerRevolution;
    }

    public OneXAnalysis Analyze(IReadOnlyList<SignalSample> vibration, TachometerAnalysis tachometer)
    {
        ArgumentNullException.ThrowIfNull(vibration);
        ArgumentNullException.ThrowIfNull(tachometer);
        if (!tachometer.Quality.IsValid)
            return Invalid(tachometer.Quality);

        var vectors = new List<Complex>();
        for (var index = 0; index < tachometer.PulseTimes.Count - 1; index++)
        {
            var start = tachometer.PulseTimes[index];
            var end = tachometer.PulseTimes[index + 1];
            var period = (end - start).TotalSeconds;
            var samples = vibration.Where(sample => sample.Timestamp >= start && sample.Timestamp < end).ToArray();
            if (samples.Length < _minimumSamplesPerRevolution)
                return Invalid(new SignalQuality(SignalQualityCode.InsufficientSamples,
                    $"A complete revolution requires at least {_minimumSamplesPerRevolution} vibration samples."));

            var coefficient = samples.Aggregate(Complex.Zero, (sum, sample) =>
            {
                var phase = -2 * Math.PI * (sample.Timestamp - start).TotalSeconds / period;
                return sum + sample.Value * Complex.FromPolarCoordinates(1, phase);
            }) / samples.Length;
            vectors.Add(2 * coefficient);
        }

        if (vectors.Count == 0)
            return Invalid(new SignalQuality(SignalQualityCode.InsufficientSamples, "No complete rotation is available."));

        var vector = vectors.Aggregate(Complex.Zero, (sum, item) => sum + item) / vectors.Count;
        return new OneXAnalysis(vector, Complex.Abs(vector), Math.Atan2(vector.Imaginary, vector.Real), vectors.Count, SignalQuality.Valid);
    }

    private static OneXAnalysis Invalid(SignalQuality quality) => new(Complex.Zero, 0, 0, 0, quality);
}
