using Akka.Actor;

namespace Lighthouse.App
{
    class Program
    {
        static void Main(string[] args)
        {
            ActorSystem lighthouse;
            if (args.Length >= 2)
            {
                lighthouse = LighthouseHost.LaunchLighthouse(args[0], int.Parse(args[1]));
            }
            else if (args.Length >= 1)
            {
                lighthouse = LighthouseHost.LaunchLighthouse(args[0]);
            }
            else
            {
                lighthouse = LighthouseHost.LaunchLighthouse();
            }
            
            lighthouse.AwaitTermination();
        }
    }
}
