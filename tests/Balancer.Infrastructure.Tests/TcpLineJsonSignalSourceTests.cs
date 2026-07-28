using System.Net;
using System.Net.Sockets;
using System.Text;
using Balancer.Infrastructure.Acquisition;
using Balancer.Infrastructure.Alarms;
using Balancer.Infrastructure.Logging;

namespace Balancer.Infrastructure.Tests;

public sealed class TcpLineJsonSignalSourceTests
{
    [Fact]
    public async Task ReadFramesAsync_returns_valid_frames_and_skips_invalid_json()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var writer = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var bytes = Encoding.UTF8.GetBytes("not-json\n{\"timestampUtc\":\"2026-07-28T08:00:00Z\",\"piezoA\":1.5,\"piezoB\":-2.5,\"tach\":1}\n");
            await stream.WriteAsync(bytes);
        });
        var alarms = new InMemoryAlarmService();
        await using var source = new TcpLineJsonSignalSource(Options(listener), NullLogger.Instance, alarms);

        await source.ConnectAsync();
        await using var enumerator = source.ReadFramesAsync().GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1.5d, enumerator.Current.PiezoA);
        Assert.Contains(alarms.ActiveAlarms, alarm => alarm.Code == "TCP_PROTOCOL");
        await writer;
    }

    [Fact]
    public async Task ReadFramesAsync_raises_timeout_alarm_when_server_sends_no_frame()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var clientConnected = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var accepted = listener.AcceptTcpClientAsync(clientConnected.Token).AsTask();
        var alarms = new InMemoryAlarmService();
        await using var source = new TcpLineJsonSignalSource(Options(listener, TimeSpan.FromMilliseconds(50)), NullLogger.Instance, alarms);

        await source.ConnectAsync();
        using var client = await accepted;
        await using var enumerator = source.ReadFramesAsync().GetAsyncEnumerator();

        await Assert.ThrowsAsync<TimeoutException>(async () => await enumerator.MoveNextAsync().AsTask());
        Assert.Contains(alarms.ActiveAlarms, alarm => alarm.Code == "TCP_TIMEOUT" && alarm.Severity == AlarmSeverity.Fault);
        Assert.Equal(ConnectionState.Faulted, source.Status.State);
    }

    private static TcpConnectionOptions Options(TcpListener listener, TimeSpan? readTimeout = null) =>
        new(((IPEndPoint)listener.LocalEndpoint).Address.ToString(), ((IPEndPoint)listener.LocalEndpoint).Port,
            TimeSpan.FromSeconds(2), readTimeout ?? TimeSpan.FromSeconds(2));

    private sealed class NullLogger : IAppLogger
    {
        public static NullLogger Instance { get; } = new();
        public void Error(Exception? exception, string messageTemplate, params object?[] propertyValues) { }
        public void Information(string messageTemplate, params object?[] propertyValues) { }
        public void Warning(string messageTemplate, params object?[] propertyValues) { }
    }
}
