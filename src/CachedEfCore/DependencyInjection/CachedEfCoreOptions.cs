using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.DependencyInjection
{
    public partial class CachedEfCoreOptions
    {
        public required JsonSerializerOptions KeyGeneratorJsonSerializerOptions { get; set; }
        public required List<Type> NonEvaluableTypes { get; set; }
    }

    public partial class CachedEfCoreOptions
    {
        public static CachedEfCoreOptions CreateDefault()
        {
            return new CachedEfCoreOptions
            {
                KeyGeneratorJsonSerializerOptions = DefaultKeyGeneratorJsonSerializerOptions,
                NonEvaluableTypes = DefaultNonEvaluableTypes,
            };
        }

        public static List<Type> DefaultNonEvaluableTypes => new List<Type>
        {
            typeof(DbContext),
            typeof(DbSet<>), // DbContext.SomeEntity
#pragma warning disable EF1001 
            typeof(EntityQueryable<>) // DbContext.SomeEntity.Where(x => true).GetType(),
#pragma warning restore EF1001
        };
        public static JsonSerializerOptions DefaultKeyGeneratorJsonSerializerOptions => new JsonSerializerOptions { IncludeFields = true };
    }
}