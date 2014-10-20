using System.ServiceProcess;
using Akka.Actor;

namespace Lighthouse.Service
{
    public partial class LighthouseService : ServiceBase
    {
        ActorSystem _lighthouse;

        public LighthouseService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            
            if (args.Length >= 2)
            {
                _lighthouse = LighthouseHost.LaunchLighthouse(args[0], int.Parse(args[1]));
            }
            else if (args.Length >= 1)
            {
                _lighthouse = LighthouseHost.LaunchLighthouse(args[0]);
            }
            else
            {
                _lighthouse = LighthouseHost.LaunchLighthouse();
            }
        }

        protected override void OnStop()
        {
            _lighthouse.Shutdown();
        }
    }
}
