using CachedEfCore.DbContextOptionExtensions;
using CachedEfCore.KeyGeneration;
using CachedEfCore.KeyGeneration.ExpressionEvaluation;
using CachedEfCore.KeyGeneration.ExpressionKeyGen;
using CachedEfCore.KeyGeneration.TypeCompatibility;
using Microsoft.EntityFrameworkCore.Metadata;
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
                ServiceDescriptor = ServiceDescriptor.Scoped<KeyGeneratorVisitor>(sp =>
                {
                    var printabilityChecker = sp.GetRequiredService<IPrintabilityChecker>();
                    var model = sp.GetRequiredService<IModel>();
                    var cachedEfCoreEvalutableExpressionChecker = sp.GetRequiredService<ICachedEfCoreEvalutableExpressionChecker>();

                    return new KeyGeneratorVisitor(
                        printabilityChecker,
                        model,
                        cachedEfCoreEvalutableExpressionChecker,
                        jsonSerializerOptions
                    );
                }),
                GetServiceProviderHashCode = thisService => ((JsonSerializerOptions)thisService.Options!).GetHashCode(),
                ShouldUseSameServiceProvider = args => ((JsonSerializerOptions)args.ThisService.Options!).Equals(((JsonSerializerOptions)args.OtherServices.Single().Options!)),
                Options = jsonSerializerOptions,
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
                GetServiceProviderHashCode = thisService => ((List<Type>)thisService.Options!).GetHashCode(),
                ShouldUseSameServiceProvider = args => ((List<Type>)args.ThisService.Options!).SequenceEqual(((List<Type>)args.OtherServices.Single().Options!)),
                Options = nonEvaluableTypes,
            };

            _cachedEfCoreExtension.ReplaceService(service);

            return this;
        }
    }
}
