using Xunit;

namespace CachedEfCore.Caching.Specification.Tests.Collections
{
    [CollectionDefinition(nameof(NonParallelCollection), DisableParallelization = true)]
    public class NonParallelCollection
    {
    }
}
