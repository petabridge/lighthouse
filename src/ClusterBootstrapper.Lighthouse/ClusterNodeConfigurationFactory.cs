using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Configuration.Hocon;

namespace ClusterBootstrapper.Lighthouse
{
    public static class ClusterNodeConfigurationFactory
    {
        internal static Uri GetSelfIpDiscoveryUri(Config config)
        {
            var seedNodes = config.GetStringList("akka.cluster.seed-nodes");
            Uri seedNodeUri;
            Uri.TryCreate(seedNodes[0], UriKind.Absolute, out seedNodeUri);
            var seedNodeHttpUri = new UriBuilder("http", seedNodeUri.Host, 9000, "api/whatsmyip");

            return seedNodeHttpUri.Uri;
        }
        internal static Uri GetSeedNodeDiscoveryUri(Config config)
        {
            var seedNodes = config.GetStringList("akka.cluster.seed-nodes");
            Uri seedNodeUri;
            Uri.TryCreate(seedNodes[0], UriKind.Absolute, out seedNodeUri);
            var seedNodeHttpUri = new UriBuilder("http", seedNodeUri.Host, 9000, "api/seednodes");

            return seedNodeHttpUri.Uri;
        }

        internal static string GetSelfIpAddress(HttpClient client, Uri selfIpDiscoveryUri)
        {
            var response = client.GetAsync(selfIpDiscoveryUri).Result;
            return response.Content.ReadAsStringAsync().Result;
        }

        internal static string GetAllSeedNodes(HttpClient client, Uri seedNodeDiscoveryUri)
        {
            var response = client.GetAsync(seedNodeDiscoveryUri).Result;
            return response.Content.ReadAsStringAsync().Result;
        }

        internal static Config CreateRemoteConfig(string selfIpAddress)
        {
            Uri selfAddressUri;
            Uri.TryCreate(selfIpAddress, UriKind.Absolute, out selfAddressUri);
            var selfIp = selfAddressUri.Host;
            var selfPort = selfAddressUri.Port;

            var remoteConfig =
                Akka.Configuration.ConfigurationFactory.ParseString($@"akka.remote.helios.tcp.public-hostname = ""{selfIp}""
                                                 akka.remote.helios.tcp.port = {selfPort}");
            return remoteConfig;
        }

        internal static Config CreateClusterConfig(string allSeedNodesList)
        {
            var clusterConfig =
                Akka.Configuration.ConfigurationFactory.ParseString($@"akka.cluster.seed-nodes = {allSeedNodesList}");
            return clusterConfig;
        }

        public static Config CreateConfig()
        {
            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;

            var selfIpDiscoveryUri = GetSelfIpDiscoveryUri(config);
            var seedNodeDiscoveryUri = GetSeedNodeDiscoveryUri(config);

            string selfIpAddress;
            string allSeedNodes;
            using (var client = new HttpClient())
            {
                selfIpAddress = GetSelfIpAddress(client, selfIpDiscoveryUri);
                allSeedNodes = GetAllSeedNodes(client, seedNodeDiscoveryUri);
            }

            var remoteConfig = CreateRemoteConfig(selfIpAddress);
            var clusterConfig = CreateClusterConfig(allSeedNodes);
            var finalConfig = remoteConfig
                                .WithFallback(clusterConfig)
                                .WithFallback(config);
            return finalConfig;
        }
    }
}
