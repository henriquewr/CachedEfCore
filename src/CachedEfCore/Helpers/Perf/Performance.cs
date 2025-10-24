using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CachedEfCore.Helpers.Perf
{
#if TEST_BUILD
    public
#else
    internal
#endif
        class Performance
    {
        private static void ForceGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Stopwatch Measure(Action actionToMeasure, int iterations = 10000, int warmupIterations = 1000)
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                actionToMeasure();
            }

            ForceGC();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                actionToMeasure();
            }
            sw.Stop();
            return sw;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<Stopwatch> MeasureTask(Func<Task> actionToMeasure, int iterations = 10000, int warmupIterations = 1000)
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                await actionToMeasure();
            }

            ForceGC();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await actionToMeasure();
            }
            sw.Stop();
            return sw;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async ValueTask<Stopwatch> MeasureValueTask(Func<ValueTask> actionToMeasure, int iterations = 10000, int warmupIterations = 1000)
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                await actionToMeasure();
            }

            ForceGC();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await actionToMeasure();
            }
            sw.Stop();
            return sw;
        }
    }
}
