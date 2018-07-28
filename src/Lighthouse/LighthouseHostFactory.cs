using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using ConfigurationException = Akka.Configuration.ConfigurationException;

namespace Lighthouse
{
    /// <summary>
    /// Launcher for the Lighthouse <see cref="ActorSystem"/>
    /// </summary>
    public static class LighthouseHostFactory
    {
        public static ActorSystem LaunchLighthouse(string ipAddress = null, int? specifiedPort = null, string systemName = null)
        {
            systemName = systemName ?? Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim();
            ipAddress = ipAddress ?? Environment.GetEnvironmentVariable("CLUSTER_IP")?.Trim();
            if (specifiedPort == null)
            {
                var envPort = Environment.GetEnvironmentVariable("CLUSTER_PORT")?.Trim();
                if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out var actualPort))
                {
                    specifiedPort = actualPort;
                }
            }

            var clusterConfig = ConfigurationFactory.ParseString(File.ReadAllText("akka.hocon"));

            var lighthouseConfig = clusterConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null && string.IsNullOrEmpty(systemName))
            {
                systemName = lighthouseConfig.GetString("actorsystem", systemName);
            }

            var remoteConfig = clusterConfig.GetConfig("akka.remote");

            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = remoteConfig.GetString("dot-netty.tcp.public-hostname") ??
                            "127.0.0.1"; //localhost as a final default
            }
           
            int port = specifiedPort ?? remoteConfig.GetInt("dot-netty.tcp.port");

            if (port == 0) throw new ConfigurationException("Need to specify an explicit port for Lighthouse. Found an undefined port or a port value of 0 in App.config.");

            var selfAddress = $"akka.tcp://{systemName}@{ipAddress}:{port}";

            /*
             * Sanity check
             */
            Console.WriteLine($"[Lighthouse] ActorSystem: {systemName}; IP: {ipAddress}; PORT: {port}");
            Console.WriteLine("[Lighthouse] Performing pre-boot sanity check. Should be able to parse address [{0}]", selfAddress);
            selfAddress = new Address("akka.tcp", systemName, ipAddress.Trim(), port).ToString();
            Console.WriteLine("[Lighthouse] Parse successful.");

            var clusterSeeds = Environment.GetEnvironmentVariable("CLUSTER_SEEDS")?.Trim();

            var seeds = clusterConfig.GetStringList("akka.cluster.seed-nodes").ToList();
            if (!string.IsNullOrEmpty(clusterSeeds))
            {
                var tempSeeds = clusterSeeds.Trim('[', ']').Split(',').ToList();
                if (tempSeeds.Any())
                {
                    seeds = tempSeeds;
                }
            }

           
            if (!seeds.Contains(selfAddress))
            {
                seeds.Add(selfAddress);
            }

            var injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [", (current, seed) => current + (@"""" + seed + @""", "));
            injectedClusterConfigString += "]";

            var finalConfig = ConfigurationFactory.ParseString(
                string.Format(@"akka.remote.dot-netty.tcp.public-hostname = {0} 
akka.remote.dot-netty.tcp.port = {1}", ipAddress, port))
                .WithFallback(ConfigurationFactory.ParseString(injectedClusterConfigString))
                .WithFallback(clusterConfig);

            return ActorSystem.Create(systemName, finalConfig);
        }
    }
}
