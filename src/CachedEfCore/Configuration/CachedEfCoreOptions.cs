using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreOptions : ICachedEfCoreOptions
    {
        public required JsonSerializerOptions KeyGeneratorJsonSerializerOptions { get; set; }
        public required List<Type> NonEvaluableTypes { get; set; }
    }

    public static class CachedEfCoreOptionsExtensions
    {
        extension(CachedEfCoreOptions options)
        {
            public static CachedEfCoreOptions CreateDefault()
            {
                return new CachedEfCoreOptions
                {
                    KeyGeneratorJsonSerializerOptions = CachedEfCoreOptions.DefaultKeyGeneratorJsonSerializerOptions,
                    NonEvaluableTypes = CachedEfCoreOptions.DefaultNonEvaluableTypes,
                };
            }

            public static List<Type> DefaultNonEvaluableTypes => new List<Type>
            {
                typeof(DbContext),
                typeof(DbSet<>), // DbContext.SomeEntity
    #pragma warning disable EF1001 
                typeof(EntityQueryable<>), // DbContext.SomeEntity.Where(x => true).GetType(),
    #pragma warning restore EF1001
                typeof(EntityQueryRootExpression),

            };
            public static JsonSerializerOptions DefaultKeyGeneratorJsonSerializerOptions => new JsonSerializerOptions { IncludeFields = true };
        }
    }
}
