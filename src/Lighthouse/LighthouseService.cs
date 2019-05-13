// Copyright 2014-2019 Aaron Stannard, Petabridge LLC
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Lighthouse
{
    public class LighthouseService
    {
        private readonly string _ipAddress;
        private readonly int? _port;
        private readonly string _actorSystemName;

        private ActorSystem _lighthouseSystem;

        public LighthouseService() : this(null, null, null) { }

        public LighthouseService(string ipAddress, int? port, string actorSystemName)
        {
            _ipAddress = ipAddress;
            _port = port;
            _actorSystemName = actorSystemName;
        }

        public void Start()
        {
            _lighthouseSystem = LighthouseHostFactory.LaunchLighthouse(_ipAddress, _port, _actorSystemName);
            var pbm = PetabridgeCmd.Get(_lighthouseSystem);
            pbm.RegisterCommandPalette(ClusterCommands.Instance); // enable Akka.Cluster management commands
            pbm.RegisterCommandPalette(RemoteCommands.Instance); // enable Akka.Remote management commands
            pbm.Start();
        }

        /// <summary>
        /// Task completes once the Lighthouse <see cref="ActorSystem"/> has terminated.
        /// </summary>
        /// <remarks>
        /// Doesn't actually invoke termination. Need to call <see cref="StopAsync"/> for that.
        /// </remarks>
        public Task TerminationHandle => _lighthouseSystem.WhenTerminated;

        public async Task StopAsync()
        {
            await CoordinatedShutdown.Get(_lighthouseSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}
