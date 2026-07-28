using System.Net.Sockets;
using System.Text;
using Balancer.Infrastructure.Alarms;
using Balancer.Infrastructure.Logging;

namespace Balancer.Infrastructure.Acquisition;

/// <summary>Reads UTF-8, newline-delimited JSON acquisition frames from a TCP server.</summary>
public sealed class TcpLineJsonSignalSource : ISignalSource
{
    private readonly TcpConnectionOptions _options;
    private readonly IAppLogger _logger;
    private readonly IAlarmService _alarms;
    private TcpClient? _client;
    private StreamReader? _reader;
    private ConnectionStatus _status = ConnectionStatus.Disconnected;
    private bool _disposed;

    public TcpLineJsonSignalSource(TcpConnectionOptions options, IAppLogger logger, IAlarmService alarms)
    {
        options.Validate();
        _options = options;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alarms = alarms ?? throw new ArgumentNullException(nameof(alarms));
    }

    public ConnectionStatus Status => _status;
    public event EventHandler<ConnectionStatus>? StatusChanged;
    public event EventHandler<SignalSourceError>? Faulted;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_client?.Connected == true) return;

        SetStatus(ConnectionState.Connecting, $"Connecting to {_options.Host}:{_options.Port}.");
        var client = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ConnectTimeout);
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token).ConfigureAwait(false);
            _client = client;
            _reader = new StreamReader(client.GetStream(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: false);
            SetStatus(ConnectionState.Connected, $"Connected to {_options.Host}:{_options.Port}.");
            _logger.Information("TCP acquisition connected to {Host}:{Port}.", _options.Host, _options.Port);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            ReportFault(new(SignalSourceErrorKind.Timeout, $"TCP connection timed out after {_options.ConnectTimeout}.", exception));
            throw new TimeoutException($"TCP connection timed out after {_options.ConnectTimeout}.", exception);
        }
        catch (Exception exception)
        {
            client.Dispose();
            ReportFault(new(SignalSourceErrorKind.Connection, $"Could not connect to {_options.Host}:{_options.Port}.", exception));
            throw;
        }
    }

    public async IAsyncEnumerable<SignalFrame> ReadFramesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_reader is null || Status.State != ConnectionState.Connected)
            throw new InvalidOperationException("Connect the TCP signal source before reading frames.");

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_options.ReadTimeout);
                line = await _reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                ReportFault(new(SignalSourceErrorKind.Timeout, $"No TCP frame arrived within {_options.ReadTimeout}.", exception));
                throw new TimeoutException($"No TCP frame arrived within {_options.ReadTimeout}.", exception);
            }
            catch (IOException exception)
            {
                ReportFault(new(SignalSourceErrorKind.Disconnected, "TCP connection was interrupted while reading a frame.", exception));
                yield break;
            }

            if (line is null)
            {
                ReportFault(new(SignalSourceErrorKind.Disconnected, "TCP server closed the acquisition connection."));
                yield break;
            }
            if (line.Length > _options.MaximumLineLength)
            {
                ReportProtocolIssue($"TCP frame exceeded the {_options.MaximumLineLength}-character limit.");
                continue;
            }

            var parsed = TcpLineJsonFrameParser.Parse(line);
            if (!parsed.IsSuccess)
            {
                ReportProtocolIssue(parsed.ErrorMessage!);
                continue;
            }

            yield return parsed.Frame!;
        }
    }

    public Task DisconnectAsync()
    {
        _reader?.Dispose();
        _reader = null;
        _client?.Dispose();
        _client = null;
        if (Status.State != ConnectionState.Disconnected) SetStatus(ConnectionState.Disconnected, null);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
    }

    private void ReportProtocolIssue(string message) => ReportFault(new(SignalSourceErrorKind.Protocol, message));

    private void ReportFault(SignalSourceError error)
    {
        var severity = error.Kind is SignalSourceErrorKind.Protocol ? AlarmSeverity.Warning : AlarmSeverity.Fault;
        _alarms.Raise(severity, $"TCP_{error.Kind.ToString().ToUpperInvariant()}", error.Message);
        _logger.Error(error.Exception, "TCP acquisition {Kind}: {Message}", error.Kind, error.Message);
        if (error.Kind is not SignalSourceErrorKind.Protocol) SetStatus(ConnectionState.Faulted, error.Message);
        Faulted?.Invoke(this, error);
    }

    private void SetStatus(ConnectionState state, string? detail)
    {
        _status = new ConnectionStatus(state, detail, DateTimeOffset.UtcNow);
        StatusChanged?.Invoke(this, _status);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
