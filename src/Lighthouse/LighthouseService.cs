using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;

namespace Lighthouse
{
    public class LighthouseService
    {
        private readonly string _ipAddress;
        private readonly int? _port;

        private ActorSystem _lighthouseSystem;

        public LighthouseService() : this(null, null) { }

        public LighthouseService(string ipAddress, int? port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public void Start()
        {
            _lighthouseSystem = LighthouseHostFactory.LaunchLighthouse(_ipAddress, _port);
        }

        public async Task StopAsync()
        {
            await Cluster.Get(_lighthouseSystem).LeaveAsync();
            await _lighthouseSystem.Terminate();
        }
    }
}
