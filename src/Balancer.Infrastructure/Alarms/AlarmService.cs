namespace Balancer.Infrastructure.Alarms;

public enum AlarmSeverity
{
    Information,
    Warning,
    Fault
}

public sealed record Alarm(Guid Id, AlarmSeverity Severity, string Code, string Message, DateTimeOffset RaisedAtUtc, bool IsAcknowledged = false);

public interface IAlarmService
{
    IReadOnlyCollection<Alarm> ActiveAlarms { get; }
    event EventHandler<Alarm>? Raised;
    event EventHandler<Alarm>? Acknowledged;
    Alarm Raise(AlarmSeverity severity, string code, string message);
    bool Acknowledge(Guid alarmId);
}

public sealed class InMemoryAlarmService : IAlarmService
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Alarm> _alarms = [];

    public IReadOnlyCollection<Alarm> ActiveAlarms
    {
        get { lock (_gate) return _alarms.Values.OrderByDescending(x => x.RaisedAtUtc).ToArray(); }
    }

    public event EventHandler<Alarm>? Raised;
    public event EventHandler<Alarm>? Acknowledged;

    public Alarm Raise(AlarmSeverity severity, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var alarm = new Alarm(Guid.NewGuid(), severity, code, message, DateTimeOffset.UtcNow);
        lock (_gate) _alarms.Add(alarm.Id, alarm);
        Raised?.Invoke(this, alarm);
        return alarm;
    }

    public bool Acknowledge(Guid alarmId)
    {
        Alarm? acknowledged;
        lock (_gate)
        {
            if (!_alarms.TryGetValue(alarmId, out var alarm) || alarm.IsAcknowledged) return false;
            acknowledged = alarm with { IsAcknowledged = true };
            _alarms[alarmId] = acknowledged;
        }
        Acknowledged?.Invoke(this, acknowledged);
        return true;
    }
}
