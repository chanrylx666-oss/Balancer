using System.Numerics;

namespace Balancer.Core.Acquisition;

public sealed record SimulationInfluenceMatrix(Complex H11, Complex H12, Complex H21, Complex H22);

public sealed class SimulationSignalOptions
{
    public double Rpm { get; init; } = 1_500;
    public int SampleRateHz { get; init; } = 10_000;
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(2);
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UnixEpoch;
    public Complex PlaneAUnbalance { get; init; } = Complex.One;
    public Complex PlaneBUnbalance { get; init; } = Complex.One;
    public SimulationInfluenceMatrix InfluenceMatrix { get; init; } = new(Complex.One, Complex.Zero, Complex.Zero, Complex.One);
    public double NoiseStandardDeviation { get; init; }
    public TimeSpan TachJitter { get; init; }
    public double TachDropoutProbability { get; init; }
    public int RandomSeed { get; init; } = 1;
}

/// <summary>Deterministic, controllable two-piezo plus keyphasor source for offline calibration testing.</summary>
public sealed class SimulationSignalSource : ISignalSource
{
    private readonly SimulationSignalOptions _options;

    public SimulationSignalSource(SimulationSignalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Rpm <= 0 || options.SampleRateHz <= 0 || options.Duration <= TimeSpan.Zero ||
            options.NoiseStandardDeviation < 0 || options.TachDropoutProbability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        _options = options;
    }

    public async IAsyncEnumerable<SignalFrame> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var random = new Random(_options.RandomSeed);
        var total = (int)Math.Floor(_options.Duration.TotalSeconds * _options.SampleRateHz);
        var period = 60.0 / _options.Rpm;
        var responseA = _options.InfluenceMatrix.H11 * _options.PlaneAUnbalance + _options.InfluenceMatrix.H12 * _options.PlaneBUnbalance;
        var responseB = _options.InfluenceMatrix.H21 * _options.PlaneAUnbalance + _options.InfluenceMatrix.H22 * _options.PlaneBUnbalance;
        var pulseOffsets = BuildPulseOffsets(random, period, _options.Duration.TotalSeconds);

        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seconds = index / (double)_options.SampleRateHz;
            var timestamp = _options.StartTime + TimeSpan.FromSeconds(seconds);
            var angle = 2 * Math.PI * seconds / period;
            var tach = pulseOffsets.Any(pulse => seconds >= pulse && seconds < pulse + 1.0 / _options.SampleRateHz) ? 1.0 : 0.0;
            yield return new SignalFrame(
                new SignalSample(timestamp, RealAtAngle(responseA, angle) + Gaussian(random) * _options.NoiseStandardDeviation),
                new SignalSample(timestamp, RealAtAngle(responseB, angle) + Gaussian(random) * _options.NoiseStandardDeviation),
                new SignalSample(timestamp, tach));
            await Task.Yield();
        }
    }

    private List<double> BuildPulseOffsets(Random random, double period, double duration)
    {
        var offsets = new List<double>();
        for (var revolutions = 0; revolutions * period < duration; revolutions++)
        {
            if (random.NextDouble() < _options.TachDropoutProbability) continue;
            var jitter = (random.NextDouble() * 2 - 1) * _options.TachJitter.TotalSeconds;
            offsets.Add(Math.Max(0, revolutions * period + jitter));
        }
        return offsets;
    }

    private static double RealAtAngle(Complex vector, double angle) => (vector * Complex.FromPolarCoordinates(1, angle)).Real;

    private static double Gaussian(Random random)
    {
        var u1 = Math.Max(random.NextDouble(), double.Epsilon);
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * random.NextDouble());
    }
}
