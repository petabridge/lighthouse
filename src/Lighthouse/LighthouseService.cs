// -----------------------------------------------------------------------
// <copyright file="LighthouseService.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Actor;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Lighthouse
{
    public class LighthouseService
    {
        private readonly string _actorSystemName;
        private readonly string _ipAddress;
        private readonly int? _port;

        private ActorSystem _lighthouseSystem;

        public LighthouseService() : this(null, null, null)
        {
        }

        public LighthouseService(string ipAddress, int? port, string actorSystemName)
        {
            _ipAddress = ipAddress;
            _port = port;
            _actorSystemName = actorSystemName;
        }

        /// <summary>
        ///     Task completes once the Lighthouse <see cref="ActorSystem" /> has terminated.
        /// </summary>
        /// <remarks>
        ///     Doesn't actually invoke termination. Need to call <see cref="StopAsync" /> for that.
        /// </remarks>
        public Task TerminationHandle => _lighthouseSystem.WhenTerminated;

        public void Start()
        {
            _lighthouseSystem = LighthouseHostFactory.LaunchLighthouse(_ipAddress, _port, _actorSystemName);
            var pbm = PetabridgeCmd.Get(_lighthouseSystem);
            pbm.RegisterCommandPalette(ClusterCommands.Instance); // enable Akka.Cluster management commands
            pbm.RegisterCommandPalette(RemoteCommands.Instance); // enable Akka.Remote management commands
            pbm.Start();
        }

        public async Task StopAsync()
        {
            await CoordinatedShutdown.Get(_lighthouseSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}