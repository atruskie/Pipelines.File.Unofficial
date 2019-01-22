using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Pipelines.File.Unofficial.Benchmarks
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Pipelines!");

            var summary = BenchmarkRunner.Run<ReadingBenchmarks>();
        }
    }
}
