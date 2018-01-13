using System.Threading.Tasks;
using Akka.Actor;

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
            await _lighthouseSystem.Terminate();
        }
    }
}
