using System.IO;
using System.Windows;
using Balancer.Infrastructure.Alarms;
using Balancer.Infrastructure.Logging;
using Balancer.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Balancer.Wpf;

public partial class App : Application
{
    private readonly ServiceProvider _services;

    public App()
    {
        var collection = new ServiceCollection();
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "balancer-.log");
        var serilog = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File(logPath, rollingInterval: RollingInterval.Day).CreateLogger();
        collection.AddSingleton<IAppLogger>(new SerilogAppLogger(serilog));
        collection.AddSingleton<IAlarmService, InMemoryAlarmService>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainWindow>();
        _services = collection.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services.Dispose();
        base.OnExit(e);
    }
}
