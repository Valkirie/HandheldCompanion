using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using System;
using System.Diagnostics;
using System.IO;

namespace ControllerService
{
    internal static class Program
    {
        private static Logger logger;

        public static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            var configuration = new ConfigurationBuilder()
                        .AddJsonFile("servicesettings.json")
                        .Build();

            logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            string proc = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(proc);

            if (processes.Length > 1)
            {
                logger.Fatal("{0} is already running. Exiting.", proc);
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging(builder =>
                    {
                        builder.SetMinimumLevel(LogLevel.Information);
                        builder.AddSerilog(logger, true);
                    });

                    services.AddSingleton(new LoggerFactory().AddSerilog(logger));

                    services.AddHostedService<ControllerService>();
                });
        }
    }
}