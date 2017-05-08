using System;
using System.Configuration;
using Microsoft.Owin.Hosting;

namespace Lighthouse
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
