using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using NLog;

namespace Lighthouse.WhatsMyIP.Controllers
{
    public class WhatsMyIPController : ApiController
    {
        // GET api/whatsmyip
        public HttpResponseMessage Get(HttpRequestMessage request = null)
        {
            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(request.GetClientIpAddress(), Encoding.UTF8, "application/json");
            return response;
        }
    }

    public class WhatsMyIPHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var logger = LogManager.GetCurrentClassLogger();
            logger.Info($"Incoming request from IP address: {request.GetClientIpAddress()}");
            return base.SendAsync(request, cancellationToken);
        }
    }

    public static class HttpRequestMessageExtensions
    {
        private const string OwinContext = "MS_OwinContext";

        public static string GetClientIpAddress(this HttpRequestMessage request)
        {
            if (request.Properties.ContainsKey(OwinContext))
            {
                dynamic ctx = request.Properties[OwinContext];
                if (ctx != null)
                {
                    return $"{ctx.Request.Scheme}://{ctx.Request.RemoteIpAddress}:{ctx.Request.RemotePort}";
                }
            }
            return null;
        }
    }
}
