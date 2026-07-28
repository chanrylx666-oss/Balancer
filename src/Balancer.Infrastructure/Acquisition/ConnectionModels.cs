namespace Balancer.Infrastructure.Acquisition;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted
}

public sealed record TcpConnectionOptions(
    string Host,
    int Port,
    TimeSpan ConnectTimeout,
    TimeSpan ReadTimeout,
    int MaximumLineLength = 16_384)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("Host is required.", nameof(Host));
        if (Port is < 1 or > 65_535) throw new ArgumentOutOfRangeException(nameof(Port));
        if (ConnectTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
        if (ReadTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ReadTimeout));
        if (MaximumLineLength is < 1 or > 1_048_576) throw new ArgumentOutOfRangeException(nameof(MaximumLineLength));
    }
}

public sealed record ConnectionStatus(ConnectionState State, string? Detail, DateTimeOffset ChangedAtUtc)
{
    public static ConnectionStatus Disconnected { get; } = new(ConnectionState.Disconnected, null, DateTimeOffset.UtcNow);
}

public sealed record SignalFrame(DateTimeOffset TimestampUtc, double PiezoA, double PiezoB, bool Tach);

public enum SignalSourceErrorKind
{
    Connection,
    Timeout,
    Protocol,
    Disconnected,
    Unexpected
}

public sealed record SignalSourceError(SignalSourceErrorKind Kind, string Message, Exception? Exception = null);
