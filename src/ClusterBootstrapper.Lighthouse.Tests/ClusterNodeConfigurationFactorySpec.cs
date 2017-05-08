using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace ClusterBootstrapper.Lighthouse.Tests
{
    public class ClusterNodeConfigurationFactorySpec
    {
        private readonly ITestOutputHelper _output;

        public ClusterNodeConfigurationFactorySpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Should_get_seed_node_ip_address_from_hocon()
        {
            Config config =
                ConfigurationFactory.ParseString(
                    @"akka.cluster.seed-nodes = [""akka.tcp://webcrawler@172.22.144.2:4053""]");
            var ip = ClusterNodeConfigurationFactory.GetSelfIpDiscoveryUri(config).ToString();
            ip.Should().Be("http://172.22.144.2:9000/api/whatsmyip");
        }

        [Fact]
        public void Node_self_address_response_should_be_formatted_correctly()
        {
            Mock<HttpClientHandler> mockHandler = new Mock<HttpClientHandler>();
            var selfIpAddressUri = new Uri("http://172.22.144.2:9000/api/whatsmyip");

            mockHandler.SetupGetStringAsync(selfIpAddressUri, "http://172.22.144.3:12345");
            HttpClient client = new HttpClient(mockHandler.Object);

            var selfIpResponse = ClusterNodeConfigurationFactory.GetSelfIpAddress(client, selfIpAddressUri);
            selfIpResponse.Should().Be("http://172.22.144.3:12345");
        }

        [Fact]
        public void Should_create_valid_hocon_replacements()
        {
            var selfIpAddress = "http://172.22.144.3:12345";

            var remoteConfig = ClusterNodeConfigurationFactory.CreateRemoteConfig(selfIpAddress);
            remoteConfig.ToString().Should().Be(@"{
  akka : {
    remote : {
      helios : {
        tcp : {
          public-hostname : 172.22.144.3
          port : 12345
        }
      }
    }
  }
}");
        }
    }

    internal static class MockHttpClientHandlerExtensions
    {
        public static void SetupGetStringAsync(this Mock<HttpClientHandler> mockHandler, Uri requestUri, string response)
        {
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.Is<HttpRequestMessage>(message => message.RequestUri == requestUri), ItExpr.IsAny<CancellationToken>())
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) }));
        }
    }
}