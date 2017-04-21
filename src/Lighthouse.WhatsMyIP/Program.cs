using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Topshelf;

namespace Lighthouse.WhatsMyIP
{
    class Program
    {
        static int Main(string[] args)
        {
            TopshelfExitCode exitCode = HostFactory.Run(x =>
            {
                x.Service<WhatsMyIPService>(s =>
                {
                    s.ConstructUsing(_ => new WhatsMyIPService());
                    s.WhenStarted(svc => svc.Start());
                    s.WhenStopped(svc => svc.Stop());
                });

                x.SetServiceName("Lighthouse.WhatsMyIP");
                x.SetDisplayName("Lighthouse Client Self-Discovery");
                x.SetDescription("Lighthouse Client Self-Discovery for Akka.NET Clusters");

                x.SetStartTimeout(TimeSpan.FromSeconds(30));
                x.RunAsNetworkService();
                x.StartAutomatically();
                x.UseNLog();
                x.EnableServiceRecovery(r => r.RestartService(1));
            });
            return (int) exitCode;
        }
    }
}
