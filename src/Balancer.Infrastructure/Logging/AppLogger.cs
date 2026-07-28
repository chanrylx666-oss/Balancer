using Serilog;

namespace Balancer.Infrastructure.Logging;

public interface IAppLogger
{
    void Information(string messageTemplate, params object?[] propertyValues);
    void Warning(string messageTemplate, params object?[] propertyValues);
    void Error(Exception? exception, string messageTemplate, params object?[] propertyValues);
}

/// <summary>Structured logging adapter; message-template properties remain queryable in Serilog sinks.</summary>
public sealed class SerilogAppLogger : IAppLogger
{
    private readonly ILogger _logger;

    public SerilogAppLogger(ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    public void Information(string messageTemplate, params object?[] propertyValues) => _logger.Information(messageTemplate, propertyValues);
    public void Warning(string messageTemplate, params object?[] propertyValues) => _logger.Warning(messageTemplate, propertyValues);
    public void Error(Exception? exception, string messageTemplate, params object?[] propertyValues) => _logger.Error(exception, messageTemplate, propertyValues);
}
