using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Results;
using Akka.Configuration;
using Akka.Configuration.Hocon;
using FluentAssertions;
using Lighthouse.Controllers;
using Xunit;
using Xunit.Abstractions;

namespace Lighthouse.Tests
{
    public class SeedNodesAPISpec
    {
        private readonly ITestOutputHelper _output;

        public SeedNodesAPISpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Should_append_self_seed_node_to_other_seed_nodes()
        {
            var config = SeedNodeConfigurationHelpers.GetAkkaConfig();
            var otherSeedNodes = SeedNodeConfigurationHelpers.GetAllSeedNodes(config, "akka.tcp://webcrawler@127.0.0.1:4053");

            var expectedAllSeedNodes =
                new List<String>()
                {
                    "akka.tcp://webcrawler@127.0.0.1:4053",
                    "akka.tcp://webcrawler@127.0.0.1:4054",
                    "akka.tcp://webcrawler@127.0.0.1:4055"
                };

            otherSeedNodes.Should().BeEquivalentTo(expectedAllSeedNodes);
        }

        [Fact]
        public void Should_build_self_seed_node_address()
        {
            var config = SeedNodeConfigurationHelpers.GetAkkaConfig();
            var selfSeedNodeAddress = SeedNodeConfigurationHelpers.GetSelfSeedNodeAddress(config);
            selfSeedNodeAddress.Should().Be(@"akka.tcp://webcrawler@172.22.144.2:4053");
        }

        [Fact]
        public void When_other_seed_nodes_exist_should_return_all_seed_nodes()
        {
            var config = SeedNodeConfigurationHelpers.GetAkkaConfig();
            var selfSeedNode = SeedNodeConfigurationHelpers.GetSelfSeedNodeAddress(config);

            var allSeedNodes = SeedNodeConfigurationHelpers.GetAllSeedNodes(config, selfSeedNode);
            var expectedAllSeedNodes =
                new List<String>()
                {
                    "akka.tcp://webcrawler@172.22.144.2:4053",
                    "akka.tcp://webcrawler@127.0.0.1:4054",
                    "akka.tcp://webcrawler@127.0.0.1:4055"
                };
            allSeedNodes.Should().BeEquivalentTo(expectedAllSeedNodes);
        }

        [Fact]
        public void When_no_seed_nodes_existing_only_returns_self_seed_node()
        {
            var configNoSeedNodes = ConfigurationFactory.ParseString(@"akka.cluster.seed-nodes = []");
            var hoconConfigSection = (AkkaConfigurationSection) ConfigurationManager.GetSection("akka");
            var hoconConfig = hoconConfigSection.AkkaConfig;
            var finalConfig = configNoSeedNodes.WithFallback(hoconConfig);

            var selfSeedNode = SeedNodeConfigurationHelpers.GetSelfSeedNodeAddress(finalConfig);
            var allSeedNodes = SeedNodeConfigurationHelpers.GetAllSeedNodes(finalConfig, selfSeedNode);

            allSeedNodes.Count.Should().Be(1);
            allSeedNodes[0].Should().Be(@"akka.tcp://webcrawler@172.22.144.2:4053");
        }

        [Fact]
        public void Build_seed_nodes_http_response()
        {
            var controller = new SeedNodesController();

            var actionResult = controller.Get();
            var response = actionResult as OkNegotiatedContentResult<IList<string>>;

            var expectedAllSeedNodes =
                new List<String>()
                {
                    "akka.tcp://webcrawler@172.22.144.2:4053",
                    "akka.tcp://webcrawler@127.0.0.1:4054",
                    "akka.tcp://webcrawler@127.0.0.1:4055"
                };

            response.Content.ShouldBeEquivalentTo(expectedAllSeedNodes);
        }
    }
}
