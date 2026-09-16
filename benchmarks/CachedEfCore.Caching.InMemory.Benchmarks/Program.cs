using BenchmarkDotNet.Running;

namespace CachedEfCore.Caching.InMemory.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
