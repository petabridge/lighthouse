// -----------------------------------------------------------------------
// <copyright file="Program.NetCore.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using Serilog;

namespace Lighthouse
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            InitializeLogger(args);
            var (config, actorSystemName) = LighthouseConfigurator.LaunchLighthouse();
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                services.AddAkka(actorSystemName, builder =>
                {
                    builder.AddHocon(config, HoconAddMode.Prepend) // clustering / remoting automatically configured here
                        .AddPetabridgeCmd(cmd =>
                        {
                            cmd.RegisterCommandPalette(ClusterCommands.Instance);
                            cmd.RegisterCommandPalette(new RemoteCommands());
                        });
                });
            });

            var host = hostBuilder.Build();
            await host.RunAsync();
        }

        private static void InitializeLogger(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger.Information("Initializing logging from (serilog.json) configuration");
            try
            {

                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("serilog.json")
                    .AddJsonFile($"serilog.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();

                var configuredLogger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                Log.Logger = configuredLogger;
            } 
            catch(Exception ex)
            {
                Log.Logger.Information("Log configuration failed ({Message}), continuing with default logger", ex.Message);
            }
        }
    }
}