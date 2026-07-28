namespace Balancer.Infrastructure.Acquisition;

/// <summary>Common asynchronous contract for simulated, TCP, serial, or DAQ signal sources.</summary>
public interface ISignalSource : IAsyncDisposable
{
    ConnectionStatus Status { get; }

    event EventHandler<ConnectionStatus>? StatusChanged;
    event EventHandler<SignalSourceError>? Faulted;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<SignalFrame> ReadFramesAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

/// <summary>Adapts a simulation or hardware producer that already exposes async frames.</summary>
public sealed class AsyncEnumerableSignalSourceAdapter : ISignalSource
{
    private readonly Func<CancellationToken, IAsyncEnumerable<SignalFrame>> _frames;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    public AsyncEnumerableSignalSourceAdapter(Func<CancellationToken, IAsyncEnumerable<SignalFrame>> frames) =>
        _frames = frames ?? throw new ArgumentNullException(nameof(frames));

    public ConnectionStatus Status => _status;
    public event EventHandler<ConnectionStatus>? StatusChanged;
    public event EventHandler<SignalSourceError>? Faulted;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetStatus(ConnectionState.Connected, "Signal producer ready.");
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<SignalFrame> ReadFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Status.State != ConnectionState.Connected)
            throw new InvalidOperationException("Connect the signal source before reading frames.");

        await using var enumerator = _frames(cancellationToken).GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Faulted?.Invoke(this, new SignalSourceError(SignalSourceErrorKind.Unexpected, "Signal producer stopped unexpectedly.", exception));
                throw;
            }

            if (!hasNext) yield break;
            yield return enumerator.Current;
        }
    }

    public Task DisconnectAsync()
    {
        SetStatus(ConnectionState.Disconnected, null);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    private void SetStatus(ConnectionState state, string? detail)
    {
        _status = new ConnectionStatus(state, detail, DateTimeOffset.UtcNow);
        StatusChanged?.Invoke(this, _status);
    }
}
