# Lighthouse

**Lighthouse** is a simple service-discovery tool for Akka.Cluster, designed to make it easier to play nice with PaaS deployments like Azure / Elastic Beanstalk / AppHarbor.

The way it works: Lighthouse runs on a static address and _is not updated during deployments of your other Akka.Cluster services_. You don't treat it like the rest of your services. It just stays there as a fixed entry point into the cluster while the rest of your application gets deployed, redeployed, scaled up, scaled down, and so on around it. This eliminates the complexity of traditional service discovery apparatuses by relying on Akka.Cluster's own built-in protocols to do the heavy lifting.

If you do need to make an update to Lighthouse, here are some cases where that might make sense:

1. To upgrade Akka.NET itself;
2. To install additional [Petabridge.Cmd](https://cmd.petabridge.com/) modules;
3. To change the Akka.Remote serialization format (since that affects how Lighthouse communicates with the rest of your Akka.NET cluster); or
4. To install additional monitoring or tracing tools, such as [Phobos](https://phobos.petabridge.com/).

## Running Lighthouse
The easiest way to run Lighthouse is via [Petabridge's official Lighthouse Docker images on Docker Hub](https://hub.docker.com/r/petabridge/lighthouse):


**Linux Images**
```
docker pull petabridge/lighthouse:linux-latest
```

**Windows Images**
```
docker pull petabridge/lighthouse:windows-latest
```

All of these images run lighthouse on top of .NET Core 2.1 and expose the Akka.Cluster TCP endpoint on port 4053 by default. These images also come with [`Petabridge.Cmd.Host` installed](https://cmd.petabridge.com/articles/install/host-configuration.html) and exposed on TCP port 9110.

> Linux images also come with [the `pbm` client](https://cmd.petabridge.com/articles/install/index.html) installed as a global .NET Core tool, so you can remotely execute `pbm` commands inside the containers themselves without exposing `Petabridge.Cmd.Host` over the network. 
>
> This feature will be added to Windows container images as soon as [#80](https://github.com/petabridge/lighthouse/issues/80) is resolved.

### Environment Variables
Lighthouse configures itself largely through [the use of `Akka.Bootstrap.Docker`'s environment variables](https://github.com/petabridge/akkadotnet-bootstrap/tree/dev/src/Akka.Bootstrap.Docker#bootstrapping-your-akkanet-applications-with-docker):

* `ACTORSYSTEM` - the name of the `ActorSystem` Lighthouse will use to join the network.
* `CLUSTER_IP` - this value will replace the `akka.remote.dot-netty.tcp.public-hostname` at runtime. If this value is not provided, we will use `Dns.GetHostname()` instead.
* `CLUSTER_PORT` - the port number that will be used by Akka.Remote for inbound connections.
* `CLUSTER_SEEDS` - a comma-delimited list of seed node addresses used by Akka.Cluster. Here's [an example](https://github.com/petabridge/Cluster.WebCrawler/blob/9f854ff2bfb34464769f562936183ea7719da4ea/yaml/k8s-tracker-service.yaml#L46-L47). _Lighthouse will inject it's own address into this list at startup if it's not already present_.

Here's an example of running a single Lighthouse instance as a Docker container:

```
PS> docker run --name lighthouse1 --hostname lighthouse1 -p 4053:4053 -p 9110:9110 --env ACTORSYSTEM=webcrawler --env CLUSTER_IP=lighthouse1 --env CLUSTER_PORT=4053 --env CLUSTER_SEEDS="akka.tcp://webcrawler@lighthouse1:4053" petabridge/lighthouse:latest
```

### Running in .NET Framework
You can still run Lighthouse under .NET Framework 4.6.1 if you wish. Clone this repository and build the project. Lighthouse will run as a [Topshelf Windows Service](http://topshelf-project.com/) and can be installed as such.

### Examples of Lighthouse in the Wild
Looking for some complete examples of how to use Lighthouse? Here's some:

1. [Cluster.WebCrawler - webcrawling Akka.Cluster + Akka.Streams sample application.](https://github.com/petabridge/Cluster.WebCrawler)