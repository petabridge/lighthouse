using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.Owin.Hosting;

namespace Lighthouse.WhatsMyIP
{
    public class WhatsMyIPService
    {
        private IDisposable _webServer;

        public void Start()
        {
            _webServer = WebApp.Start<Startup>(ConfigurationManager.AppSettings["BaseAddress"]);
        }

        public void Stop()
        {
            _webServer?.Dispose();
        }
    }
}
