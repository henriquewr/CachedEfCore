using CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.Cache.KeyGeneration.TypeCompatibility;
using CachedEfCore.DbContextOptionExtensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreKeyGenerationOptionsBuilder
    {
        private readonly CachedEfCoreDbContextOptionExtension _cachedEfCoreExtension;

        public CachedEfCoreKeyGenerationOptionsBuilder(CachedEfCoreDbContextOptionExtension cachedEfCoreExtension)
        {
            _cachedEfCoreExtension = cachedEfCoreExtension;
        }

        public virtual CachedEfCoreKeyGenerationOptionsBuilder ConfigureJsonSerializer(Func<JsonSerializerOptions, JsonSerializerOptions> configure)
        {
            var jsonSerializerOptions = configure(CachedEfCoreKeyGenerationOptionsDefaults.DefaultJsonSerializerOptions);
            var service = new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Scoped<KeyGeneratorVisitorJsonSerializerOptions>(sp => new KeyGeneratorVisitorJsonSerializerOptions { Options = jsonSerializerOptions }),
                GetServiceProviderHashCode = null,
                ShouldUseSameServiceProvider = null,
                Options = null,
            };

            _cachedEfCoreExtension.ReplaceService(service);

            return this;
        }

        public virtual CachedEfCoreKeyGenerationOptionsBuilder ConfigureNonEvaluableTypes(Func<List<Type>, List<Type>> configure)
        {
            var nonEvaluableTypes = configure(CachedEfCoreKeyGenerationOptionsDefaults.DefaultNonEvaluableTypes);

            var service = new CachedEfCoreService
            {
                ServiceDescriptor = ServiceDescriptor.Singleton<ITypeCompatibilityChecker>(sp =>
                {
                    return new TypeCompatibilityChecker(nonEvaluableTypes);
                }),
                GetServiceProviderHashCode = thisService => ((List<Type>)thisService.Options!).Aggregate(0, (hash, type) => HashCode.Combine(hash, type)),
                ShouldUseSameServiceProvider = args => ((List<Type>)args.ThisService.Options!).SequenceEqual(((List<Type>)args.OtherServices.Single().Options!)),
                Options = nonEvaluableTypes,
            };

            _cachedEfCoreExtension.ReplaceService(service);

            return this;
        }
    }
}
