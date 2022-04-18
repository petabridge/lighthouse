#### 1.6.0 June 16 2021 ####

* Dropped Topshelf support
* Migrated to [Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting) and `IHostedService`
* Significantly reduced idle CPU consumption by migrating to [`channel-executor` for dispatching](https://getakka.net/articles/actors/dispatchers.html#channelexecutor).