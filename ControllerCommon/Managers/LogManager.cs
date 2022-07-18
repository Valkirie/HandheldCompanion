using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using System.Diagnostics;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ControllerCommon.Managers
{
    public static class LogManager
    {
        private static ILogger logger;
        public static void Initialize(string name)
        {
            var configuration = new ConfigurationBuilder()
                        .AddJsonFile($"{name}.json")
                        .Build();

            var serilogLogger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            logger = new SerilogLoggerFactory(serilogLogger).CreateLogger(name);
        }

        public static void LogInformation(string message, params object[] args)
        {
            Trace.TraceInformation(message, args);
            logger.LogInformation(message, args);
        }

        public static void LogWarning(string message, params object[] args)
        {
            Trace.TraceWarning(message, args);
            logger.LogWarning(message, args);
        }

        public static void LogCritical(string message, params object[] args)
        {
            Trace.TraceError(message, args);
            logger.LogCritical(message, args);
        }

        public static void LogDebug(string message, params object[] args)
        {
            Trace.TraceInformation(message, args);
            logger.LogDebug(message, args);
        }

        public static void LogError(string message, params object[] args)
        {
            Trace.TraceError(message, args);
            logger.LogError(message, args);
        }
    }
}
