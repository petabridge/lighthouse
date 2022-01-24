using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Tmds.Utils;
using Universe.CpuUsage;

namespace Lighthouse.Tests.Benchmark
{
    public static class Program
    {
        private const int TestLength = 5; // in seconds
        private const int TestDelay = 5; // in seconds
        private const int TestRepeat = 60;
        private const int TestClusterSize = 10;

        private const int WarmUpRepeat = 5;
        
        private const string ActorSystemName = "CpuTest";
        private const string LighthouseAddress = "127.0.0.1";
        private const int LighthousePort = 54053;

        private static readonly List<CpuUsage> Usages = new List<CpuUsage>();
        private static readonly List<Process> Processes = new List<Process>();

        public static async Task<int> Main(string[] args)
        {
            // ExecFunction hook
            if (ExecFunction.IsExecFunctionCommand(args))
                return ExecFunction.Program.Main(args);
            
            // Start lighthouse
            var service = new LighthouseService(LighthouseAddress, LighthousePort, ActorSystemName);
            service.Start();

            var executor = new FunctionExecutor(o =>
            {
                o.StartInfo.RedirectStandardError = true;
                o.OnExit = p =>
                {
                    if (p.ExitCode != 0)
                    {
                        var message =
                            "Function execution failed with exit code: " +
                            $"{p.ExitCode}{Environment.NewLine}{p.StandardError.ReadToEnd()}";
                        throw new Exception(message);
                    }
                };
            });

            // Spin up cluster nodes
            for (var port = LighthousePort + 1; port < LighthousePort + 1 + TestClusterSize; ++port)
            {
                Processes.Add(executor.Start(LaunchNode, new []
                {
                    LighthouseAddress,
                    LighthousePort.ToString(),
                    ActorSystemName,
                    port.ToString()
                }));
            }

            // Wait until things settles down 
            await Task.Delay(TimeSpan.FromSeconds(TestDelay));

            // Warm up
            foreach (var i in Enumerable.Range(1, WarmUpRepeat))
            {
                var start = CpuUsage.GetByProcess();
                await Task.Delay(TimeSpan.FromSeconds(TestLength));
                var end = CpuUsage.GetByProcess();
                var final = end - start;
                
                Console.WriteLine($"{i}. [Warmup] {final}");
            }
            Console.WriteLine();

            // Start benchmark
            foreach (var i in Enumerable.Range(1, TestRepeat))
            {
                var start = CpuUsage.GetByProcess();
                await Task.Delay(TimeSpan.FromSeconds(TestLength));
                var end = CpuUsage.GetByProcess();
                var final = end - start;
                
                Console.WriteLine($"{i}. Cpu Usage: {final}");
                Usages.Add(final.Value);
            }

            // Kill cluster node processes
            foreach (var process in Processes)
            {
                process.Kill();
                process.Dispose();
            }

            // Stop lighthouse
            await service.StopAsync();

            // Generate csv report
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine($"CPU Benchmark {now}");
            sb.AppendLine($"Sample Time,{TestLength},second(s)");
            sb.AppendLine($"Sample points,{TestRepeat}");
            sb.AppendLine($"Cluster size,{TestClusterSize},node(s)");
            sb.AppendLine("Sample time,User usage,User percent,Kernel usage,Kernel percent,Total usage,Total percent");
            foreach (var iter in Enumerable.Range(1, TestRepeat))
            {
                var usage = Usages[iter - 1];
                var user = usage.UserUsage.TotalSeconds;
                var kernel = usage.KernelUsage.TotalSeconds;
                var total = usage.TotalMicroSeconds / 1000000.0;
                sb.AppendLine($"{iter * TestLength},{user},{(user/TestLength)*100},{kernel},{(kernel/TestLength)*100},{total},{(total/TestLength)*100}");
            }
            
            await File.WriteAllTextAsync($"CpuBenchmark_{now.ToFileTime()}.csv", sb.ToString());
            
            // Generate console report
            sb.Clear();
            sb.AppendLine("CPU Benchmark complete.");
            sb.AppendLine();

            sb.AppendLine()
                .AppendLine(" CPU | Mean | StdErr | StdDev | Median |")
                .AppendLine("---- |----- |------- |------- |------- |")
                .AppendLine(CalculateResult(Usages
                    .Select(u => u.UserUsage.TotalMicroSeconds)
                    .OrderBy(i => i).ToArray(), "User"))
                .AppendLine(CalculateResult(Usages
                    .Select(u => u.KernelUsage.TotalMicroSeconds)
                    .OrderBy(i => i).ToArray(), "Kernel"));
            
            Console.WriteLine(sb.ToString());
            
            return 0;
        }

        private static string CalculateResult(long[] times, string name)
        {
            var medianIndex = times.Length / 2;
            
            var mean = times.Average();
            var stdDev = Math.Sqrt(times.Average(v => Math.Pow(v - mean, 2)));
            var stdErr = stdDev / Math.Sqrt(times.Length);
            double median;
            if (times.Length % 2 == 0)
                median = (times[medianIndex - 1] + times[medianIndex]) / 2.0;
            else
                median = times[medianIndex];

            return $" {name} | {(mean / 1000.0):N3} ms | {(stdErr / 1000.0):N3} ms | {(stdDev / 1000.0):N3} ms | {(median / 1000.0):N3} ms |";
        }
        
        // Function will be deployed in separate processes as Main()
        private static async Task<int> LaunchNode(string[] args)
        {
            var seedAddress = args[1];
            var seedPort = int.Parse(args[2]);
            var systemName = args[3];
            var port = int.Parse(args[4]);

            var config = ConfigurationFactory.ParseString($@"
                akka.actor.provider = cluster
                akka.cluster.seed-nodes = [""{new Address("akka.tcp", systemName, seedAddress.Trim(), seedPort)}""]
                akka.remote.dot-netty.tcp {{
                    public-hostname=""{seedAddress}""
                    port={port}
                }}");

            var system = ActorSystem.Create(systemName, config);
            
            // wait forever until we get killed
            await Task.Delay(TimeSpan.FromDays(1));

            return 0;
        }
    }
}
