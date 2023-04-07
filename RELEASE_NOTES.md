#### 1.8.0 April 07 2023 ####

* [Bumped Akka.NET version to 1.5.2](https://github.com/akkadotnet/akka.net/releases/tag/1.5.2)
* **Major change**: Lighthouse now uses [Akka.Cluster's default split brain resolver: `keep-majority`](https://getakka.net/articles/clustering/split-brain-resolver.html)
* Lighthouse now runs on .NET 7.0
* DynamicPGO and ServerGC are both enabled, which should help reduce Lighthouse's latency when processing cluster system messages from other nodes.
