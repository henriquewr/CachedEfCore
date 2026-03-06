using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public interface ICachedEfCoreOptions
    {
        JsonSerializerOptions KeyGeneratorJsonSerializerOptions { get; }
        List<Type> NonEvaluableTypes { get; }
    }
}