using System;
using System.Threading.Tasks;
using System.Web.Http;
using Lighthouse.WhatsMyIP.Controllers;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(Lighthouse.WhatsMyIP.Startup))]

namespace Lighthouse.WhatsMyIP
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
            config.MessageHandlers.Add(new WhatsMyIPHandler());

            appBuilder.UseWebApi(config);
        }
    }
}
