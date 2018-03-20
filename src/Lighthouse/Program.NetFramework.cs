#if NET452
using Topshelf;
#endif

namespace Lighthouse
{
    public partial class Program
    {
#if NET452
        public static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.SetServiceName("Lighthouse");
                x.SetDisplayName("Lighthouse");
                x.SetDescription("Seed node for the Akka Cluster");

                x.UseAssemblyInfoForServiceInfo();
                x.RunAsLocalSystem();
                x.StartAutomatically();

                x.Service<LighthouseService>(sc =>
                {
                    sc.ConstructUsing(() => new LighthouseService());

                    // the start and stop methods for the service
                    sc.WhenStarted(s => s.Start());
                    sc.WhenStopped(s => s.StopAsync().Wait());
                });

                x.EnableServiceRecovery(r => r.RestartService(1));
            });
        }
#endif
    }
}
