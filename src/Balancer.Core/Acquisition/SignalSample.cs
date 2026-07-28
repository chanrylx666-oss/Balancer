namespace Balancer.Core.Acquisition;

/// <summary>A single timestamped value from one acquisition channel.</summary>
public readonly record struct SignalSample(DateTimeOffset Timestamp, double Value);

/// <summary>One coherent sample of the two vibration channels and the keyphasor.</summary>
public readonly record struct SignalFrame(SignalSample PiezoA, SignalSample PiezoB, SignalSample Tachometer)
{
    public DateTimeOffset Timestamp => PiezoA.Timestamp;
}

/// <summary>Pluggable source of timestamped three-channel acquisition data.</summary>
public interface ISignalSource
{
    IAsyncEnumerable<SignalFrame> ReadAsync(CancellationToken cancellationToken = default);
}
