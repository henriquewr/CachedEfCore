using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CachedEfCore.Configuration
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
    public class CachedEfCoreService
    {
        protected virtual string GetDebuggerDisplay()
        {
            return $"{ServiceDescriptor.ServiceType} -> {ServiceDescriptor.ImplementationType}";
        }
        public required ServiceDescriptor ServiceDescriptor { get; set; }
        public required object? Options { get; set; }
        public required Func<CachedEfCoreService, int>? GetServiceProviderHashCode { get; set; }
        public required Func<ShouldUseSameServiceProviderArgs, bool>? ShouldUseSameServiceProvider { get; set; }
    }

    public readonly struct ShouldUseSameServiceProviderArgs
    {
        public required IEnumerable<CachedEfCoreService> OtherServices { get; init; }
        public required CachedEfCoreService ThisService { get; init; }
    }
}
