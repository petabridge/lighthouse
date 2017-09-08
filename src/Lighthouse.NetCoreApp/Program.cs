using System;
using System.Threading.Tasks;

namespace Lighthouse.NetCoreApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var lighthouseService = new LighthouseService();
            lighthouseService.Start();
            Console.ReadLine();
            lighthouseService.StopAsync().Wait();
        }
    }
}