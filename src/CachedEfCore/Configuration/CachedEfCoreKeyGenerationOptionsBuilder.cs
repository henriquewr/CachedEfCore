using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreKeyGenerationOptionsBuilder
    {
        protected CachedEfCoreKeyGenerationOptions KeyGenerationOptions { get; }

        public CachedEfCoreKeyGenerationOptionsBuilder(CachedEfCoreKeyGenerationOptions keyGenerationOptions)
        {
            KeyGenerationOptions = keyGenerationOptions;
        }

        public virtual CachedEfCoreKeyGenerationOptionsBuilder ConfigureJsonSerializer(Func<JsonSerializerOptions, JsonSerializerOptions> configure)
        {
            KeyGenerationOptions.JsonSerializerOptions = configure(CachedEfCoreKeyGenerationOptions.DefaultJsonSerializerOptions);

            return this;
        }

        public virtual CachedEfCoreKeyGenerationOptionsBuilder ConfigureNonEvaluableTypes(Func<List<Type>, List<Type>> configure)
        {
            KeyGenerationOptions.NonEvaluableTypes = configure(CachedEfCoreKeyGenerationOptions.DefaultNonEvaluableTypes);

            return this;
        }

        public CachedEfCoreKeyGenerationOptions Build()
        {
            return KeyGenerationOptions;
        }
    }
}
