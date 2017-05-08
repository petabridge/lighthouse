using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Results;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using FluentAssertions;
using Lighthouse.Controllers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace ClusterBootstrapper.Lighthouse.Tests
{
    public class SeedNodeConfigurationBootstrapSpec
    {
        private readonly ITestOutputHelper _output;

        public SeedNodeConfigurationBootstrapSpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Should_parse_seed_node_list_into_config()
        {
            var allSeedNodes = @"[""akka.tcp://webcrawler@127.0.0.1:4053""]";

            var section = (AkkaConfigurationSection)ConfigurationManager.GetSection("akka");
            var config = section.AkkaConfig;

            var clusterConfig = ConfigurationFactory.ParseString($@"akka.cluster.seed-nodes = {allSeedNodes}");
            var finalConfig = clusterConfig.WithFallback(config);


            finalConfig.GetStringList("akka.cluster.seed-nodes").ShouldAllBeEquivalentTo(new List<string>() {"akka.tcp://webcrawler@127.0.0.1:4053"});
        }
    }
}
