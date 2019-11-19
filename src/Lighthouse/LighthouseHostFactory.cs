// -----------------------------------------------------------------------
// <copyright file="LighthouseHostFactory.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using static System.String;

namespace Lighthouse
{
    /// <summary>
    ///     Launcher for the Lighthouse <see cref="ActorSystem" />
    /// </summary>
    public static class LighthouseHostFactory
    {
        public static ActorSystem LaunchLighthouse(string ipAddress = null, int? specifiedPort = null,
            string systemName = null)
        {
            systemName = systemName ?? Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim();


            // Set environment variables for use inside Akka.Bootstrap.Docker
            // If overrides were provided to this method.
            //if (!string.IsNullOrEmpty(ipAddress)) Environment.SetEnvironmentVariable("CLUSTER_IP", ipAddress);

            //if (specifiedPort != null)
            //    Environment.SetEnvironmentVariable("CLUSTER_PORT", specifiedPort.Value.ToString());

            var useDocker = !(IsNullOrEmpty(Environment.GetEnvironmentVariable("CLUSTER_IP")?.Trim()) ||
                             IsNullOrEmpty(Environment.GetEnvironmentVariable("CLUSTER_SEEDS")?.Trim()));

            var clusterConfig = ConfigurationFactory.ParseString(File.ReadAllText("akka.hocon"));

            // If none of the environment variables expected by Akka.Bootstrap.Docker are set, use only what's in HOCON
            if (useDocker)
                clusterConfig = clusterConfig.BootstrapFromDocker();

            var lighthouseConfig = clusterConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null && IsNullOrEmpty(systemName))
                systemName = lighthouseConfig.GetString("actorsystem", systemName);

            ipAddress = clusterConfig.GetString("akka.remote.dot-netty.tcp.public-hostname", "127.0.0.1");
            var port = clusterConfig.GetInt("akka.remote.dot-netty.tcp.port");

            var sslEnabled = clusterConfig.GetBoolean("akka.remote.dot-netty.tcp.enable-ssl");
            var selfAddress = sslEnabled ? new Address("akka.ssl.tcp", systemName, ipAddress.Trim(), port).ToString()
                    : new Address("akka.tcp", systemName, ipAddress.Trim(), port).ToString();

            /*
             * Sanity check
             */
            Console.WriteLine($"[Lighthouse] ActorSystem: {systemName}; IP: {ipAddress}; PORT: {port}");
            Console.WriteLine("[Lighthouse] Performing pre-boot sanity check. Should be able to parse address [{0}]",
                selfAddress);
            Console.WriteLine("[Lighthouse] Parse successful.");


            var seeds = clusterConfig.GetStringList("akka.cluster.seed-nodes").ToList();

            Config injectedClusterConfigString = null;


            if (!seeds.Contains(selfAddress))
            {
                seeds.Add(selfAddress);

                if (seeds.Count > 1)
                {
                    injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [",
                        (current, seed) => current + @"""" + seed + @""", ");
                    injectedClusterConfigString += "]";
                }
                else
                {
                    injectedClusterConfigString = "akka.cluster.seed-nodes = [\"" + selfAddress + "\"]";
                }
            }


            var finalConfig = injectedClusterConfigString != null
                ? injectedClusterConfigString
                    .WithFallback(clusterConfig)
                : clusterConfig;

            return ActorSystem.Create(systemName, finalConfig);
        }
    }
}