#### 1.3.2 May 1 2020 ####
* Updated to use Akka.NET v1.4.5.
* Updated Docker .NET Core runtime base image from nanoserver-1803 to nanoserver-1809, 1803 are deprecated.
* Followed [Akka.Remote performance best practices](https://getakka.net/articles/remoting/performance.html) and disabled batching for Lighthouse in order to reduce CPU utilization by default.
