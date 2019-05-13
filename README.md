# Lighthouse

**Lighthouse** is a simple service-discovery tool for Akka.Cluster, designed to make it easier to play nice with PaaS deployments like Azure / Elastic Beanstalk / AppHarbor.

The way it works: Lighthouse runs on a static address and _is not updated during deployments of your other Akka.Cluster services_. You don't treat it like the rest of your services. It just stays there as a fixed entry point into the cluster while the rest of your application gets deployed, redeployed, scaled up, scaled down, and so on around it. This eliminates the complexity of traditional service discovery apparatuses by relying on Akka.Cluster's own built-in protocols to do the heavy lifting.

If you do need to make an update to Lighthouse, here are some cases where that might make sense:

1. To upgrade Akka.NET itself;
2. To install additional [Petabridge.Cmd](https://cmd.petabridge.com/) modules;
3. To change the Akka.Remote serialization format (since that affects how Lighthouse communicates with the rest of your Akka.NET cluster); or
4. To install additional monitoring or tracing tools, such as [Phobos](https://phobos.petabridge.com/).

## Running Lighthouse