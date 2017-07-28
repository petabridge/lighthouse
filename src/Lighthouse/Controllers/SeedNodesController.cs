using System.Collections.Generic;
using System.Configuration;
using System.Web.Http;
using Akka.Configuration;
using Akka.Configuration.Hocon;

namespace Lighthouse.Controllers
{
    public class SeedNodesController : ApiController
    {
        // GET api/seednodes
        public IHttpActionResult Get()
        {
            var config = SeedNodeConfigurationHelpers.GetAkkaConfig();
            var selfSeedNodeAddress = SeedNodeConfigurationHelpers.GetSelfSeedNodeAddress(config);
            var allSeedNodes = SeedNodeConfigurationHelpers.GetAllSeedNodes(config, selfSeedNodeAddress);

            return Ok(allSeedNodes);
        }
    }

    public static class SeedNodeConfigurationHelpers
    {
        public static IList<string> GetAllSeedNodes(Config akkaConfig, string selfSeedNode)
        {
            var allSeedNodes = new List<string>();
            if (akkaConfig.GetStringList("akka.cluster.seed-nodes").Count > 0)
            {
                var otherSeedNodes = akkaConfig.GetStringList("akka.cluster.seed-nodes");
                allSeedNodes.AddRange(otherSeedNodes);
                if (!otherSeedNodes.Contains(selfSeedNode))
                {
                    allSeedNodes.Add(selfSeedNode);
                }
            }
            else
            {
                allSeedNodes.Add(selfSeedNode);
            }

            return allSeedNodes;
        }

        public static string GetSelfSeedNodeAddress(Config akkaConfig)
        {
            var systemName = "lighthouse";
            var lighthouseConfig = akkaConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null)
            {
                systemName = lighthouseConfig.GetString("actorsystem", systemName);
            }

            var remoteConfig = akkaConfig.GetConfig("akka.remote");
            var selfIp = remoteConfig.GetString("helios.tcp.public-hostname") ?? "127.0.0.1";
            var selfPort = remoteConfig.GetInt("helios.tcp.port");
            var selfProtocol = remoteConfig.GetString("helios.tcp.transport-protocol");

            return $@"akka.{selfProtocol}://{systemName}@{selfIp}:{selfPort}";
        }

        public static Config GetAkkaConfig()
        {
            var section = (AkkaConfigurationSection) ConfigurationManager.GetSection("akka");
            return section.AkkaConfig;
        }
    }
}
