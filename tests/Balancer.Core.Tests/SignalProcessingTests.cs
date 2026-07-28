using System.Numerics;
using Balancer.Core.Acquisition;
using Balancer.Core.SignalProcessing;

namespace Balancer.Core.Tests;

public sealed class TachometerAnalyzerTests
{
    [Fact]
    public void Analyze_RegularPulses_ReturnsExpectedRpm()
    {
        var samples = TestSignals.Tachometer(rpm: 1_200, revolutions: 6, sampleRateHz: 10_000);
        var result = new TachometerAnalyzer().Analyze(samples);
        Assert.True(result.Quality.IsValid);
        Assert.Equal(1_200, result.Rpm, precision: 1);
        Assert.Equal(6, result.PulseTimes.Count);
    }

    [Fact]
    public void Analyze_MissingPulses_ReportsInsufficientPulses()
    {
        var result = new TachometerAnalyzer().Analyze(TestSignals.Tachometer(1_200, 1, 10_000));
        Assert.False(result.Quality.IsValid);
        Assert.Equal(SignalQualityCode.InsufficientTachPulses, result.Quality.Code);
    }

    [Fact]
    public void Analyze_LargeSpeedVariation_ReportsUnstableSpeed()
    {
        var result = new TachometerAnalyzer(maximumSpeedVariation: 0.05)
            .Analyze(TestSignals.TachometerAt(new[] { 0.0, 0.05, 0.10, 0.25, 0.30 }, 10_000));
        Assert.False(result.Quality.IsValid);
        Assert.Equal(SignalQualityCode.ExcessiveSpeedVariation, result.Quality.Code);
    }
}

public sealed class SynchronousVibrationAnalyzerTests
{
    [Fact]
    public void Analyze_OneXWaveform_RecoversAmplitudeAndPhase()
    {
        const double amplitude = 2.4, phaseRadians = 0.65;
        var tach = TestSignals.Tachometer(900, 8, 12_000);
        var vibration = TestSignals.OneX(tach, 900, amplitude, phaseRadians);
        var result = new SynchronousVibrationAnalyzer().Analyze(vibration, new TachometerAnalyzer().Analyze(tach));
        Assert.True(result.Quality.IsValid);
        Assert.Equal(amplitude, result.Amplitude, 2);
        Assert.Equal(phaseRadians, result.PhaseRadians, 2);
    }

    [Fact]
    public void Analyze_TooFewSamples_ReportsQualityIssue()
    {
        var tach = TestSignals.Tachometer(900, 4, 40);
        var result = new SynchronousVibrationAnalyzer(minimumSamplesPerRevolution: 16)
            .Analyze(TestSignals.OneX(tach, 900, 1, 0), new TachometerAnalyzer(maximumSpeedVariation: 1).Analyze(tach));
        Assert.False(result.Quality.IsValid);
        Assert.Equal(SignalQualityCode.InsufficientSamples, result.Quality.Code);
    }
}

public sealed class SimulationSignalSourceTests
{
    [Fact]
    public async Task ReadAsync_KnownModel_ProducesExpectedOneXResponses()
    {
        var matrix = new SimulationInfluenceMatrix(new(2.0, 0.5), new(-0.3, 0.2), new(0.15, -0.4), new(1.1, 0.8));
        var planeA = Complex.FromPolarCoordinates(1.2, 0.35);
        var planeB = Complex.FromPolarCoordinates(0.7, -0.55);
        var source = new SimulationSignalSource(new SimulationSignalOptions
        {
            Duration = TimeSpan.FromSeconds(1), SampleRateHz = 12_000, Rpm = 900, InfluenceMatrix = matrix,
            PlaneAUnbalance = planeA, PlaneBUnbalance = planeB, NoiseStandardDeviation = 0,
            TachJitter = TimeSpan.Zero, RandomSeed = 12
        });
        var frames = await TestSignals.CollectAsync(source);
        var tach = new TachometerAnalyzer().Analyze(frames.Select(x => x.Tachometer).ToArray());
        var analyzer = new SynchronousVibrationAnalyzer();
        var a = analyzer.Analyze(frames.Select(x => x.PiezoA).ToArray(), tach);
        var b = analyzer.Analyze(frames.Select(x => x.PiezoB).ToArray(), tach);
        var expectedA = matrix.H11 * planeA + matrix.H12 * planeB;
        var expectedB = matrix.H21 * planeA + matrix.H22 * planeB;
        Assert.True(a.Quality.IsValid);
        Assert.True(b.Quality.IsValid);
        Assert.Equal(Complex.Abs(expectedA), a.Amplitude, 2);
        Assert.Equal(Math.Atan2(expectedA.Imaginary, expectedA.Real), a.PhaseRadians, 2);
        Assert.Equal(Complex.Abs(expectedB), b.Amplitude, 2);
        Assert.Equal(Math.Atan2(expectedB.Imaginary, expectedB.Real), b.PhaseRadians, 2);
    }
}

internal static class TestSignals
{
    public static SignalSample[] Tachometer(double rpm, int revolutions, int sampleRateHz) =>
        TachometerAt(Enumerable.Range(0, revolutions).Select(i => i * 60.0 / rpm).ToArray(), sampleRateHz);

    public static SignalSample[] TachometerAt(IReadOnlyList<double> pulseSeconds, int sampleRateHz)
    {
        var duration = pulseSeconds[^1] + 0.03;
        var count = (int)Math.Ceiling(duration * sampleRateHz);
        return Enumerable.Range(0, count).Select(i =>
        {
            var seconds = i / (double)sampleRateHz;
            var pulse = pulseSeconds.Any(p => seconds >= p && seconds < p + 1.0 / sampleRateHz);
            return new SignalSample(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(seconds), pulse ? 1 : 0);
        }).ToArray();
    }

    public static SignalSample[] OneX(IReadOnlyList<SignalSample> times, double rpm, double amplitude, double phase)
    {
        var start = times[0].Timestamp;
        return times.Select(x => new SignalSample(x.Timestamp, amplitude * Math.Cos(2 * Math.PI * rpm / 60 * (x.Timestamp - start).TotalSeconds + phase))).ToArray();
    }

    public static async Task<List<SignalFrame>> CollectAsync(ISignalSource source)
    {
        var frames = new List<SignalFrame>();
        await foreach (var frame in source.ReadAsync()) frames.Add(frame);
        return frames;
    }
}
