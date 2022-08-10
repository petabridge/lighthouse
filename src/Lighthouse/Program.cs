// -----------------------------------------------------------------------
// <copyright file="Program.NetCore.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Lighthouse
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var (config, actorSystemName) = LighthouseConfigurator.LaunchLighthouse();
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureServices(services =>
            {
                services.AddAkka(actorSystemName, builder =>
                {
                    builder.AddHocon(config) // clustering / remoting automatically configured here
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
    }
}