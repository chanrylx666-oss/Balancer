using Balancer.Infrastructure.Alarms;

namespace Balancer.Infrastructure.Tests;

public sealed class AlarmServiceTests
{
    [Fact]
    public void Raise_then_acknowledge_updates_active_alarm()
    {
        var service = new InMemoryAlarmService();

        var alarm = service.Raise(AlarmSeverity.Warning, "TCP_TIMEOUT", "No frame arrived in time.");

        Assert.Single(service.ActiveAlarms);
        Assert.False(alarm.IsAcknowledged);
        Assert.True(service.Acknowledge(alarm.Id));
        Assert.True(service.ActiveAlarms.Single().IsAcknowledged);
    }
}
