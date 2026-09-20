using BenchmarkDotNet.Running;

namespace CachedEfCore.Caching.FusionCache.Benchmarks
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
