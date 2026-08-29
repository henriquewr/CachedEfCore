using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public interface ICachedEfCoreOptions
    {
        Type SqlQueryEntityExtractorType { get; }
        JsonSerializerOptions KeyGeneratorJsonSerializerOptions { get; }
        List<Type> NonEvaluableTypes { get; }
    }
}