namespace Balancer.Infrastructure.Configuration;

public sealed record ApplicationSettings(
    string ActiveRecipeName = "默认双平面演示",
    string LogDirectory = "logs",
    ConnectionSettings Connection = null!)
{
    public static ApplicationSettings Default { get; } = new(
        Connection: new ConnectionSettings("Simulation", "127.0.0.1", 9000, 2000, 3000));
}

public sealed record ConnectionSettings(
    string InterfaceType,
    string Host,
    int Port,
    int ConnectTimeoutMs,
    int ReadTimeoutMs);
