using ControllerCommon.Managers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using static ControllerCommon.WinAPI;

namespace ControllerService
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // force high priority
            using (Process process = Process.GetCurrentProcess())
                SetPriorityClass(process.Handle, (int)PriorityClass.HIGH_PRIORITY_CLASS);

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);

            // initialize log manager
            LogManager.Initialize("ControllerService");
            LogManager.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.ProductVersion);

            string proc = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(proc);

            if (processes.Length > 1)
            {
                LogManager.LogCritical("{0} is already running. Exiting...", proc);
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
                    });

                    services.AddHostedService<ControllerService>();
                });
        }
    }
}