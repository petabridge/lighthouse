// Copyright 2014-2015 Aaron Stannard, Petabridge LLC
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
