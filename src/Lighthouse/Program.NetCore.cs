using System;

namespace Lighthouse
{
    public partial class Program
    {
#if NETCOREAPP1_1
        public static void Main(string[] args)
        {
            var lighthouseService = new LighthouseService();
            lighthouseService.Start();
            Console.ReadLine();
            lighthouseService.StopAsync().Wait();
        }
#endif
    }
}
