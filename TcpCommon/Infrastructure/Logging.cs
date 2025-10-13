using Serilog;

namespace TcpCommon.Infrastructure;

public static class Logging
{
    private static bool _isInitialized = false;

    public static void Configure(string logFilePath = "logs/app.log")
    {
        if (_isInitialized)
        {
            return;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _isInitialized = true;
    }

    public static ILogger GetLogger<T>()
    {
        return Log.ForContext<T>();
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
    }
}