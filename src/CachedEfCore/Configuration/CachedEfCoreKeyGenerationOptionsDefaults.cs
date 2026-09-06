using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CachedEfCore.Configuration
{
    public class CachedEfCoreKeyGenerationOptionsDefaults
    {
        public static List<Type> DefaultNonEvaluableTypes => new List<Type>
        {
            typeof(DbContext),
            typeof(DbSet<>), // DbContext.SomeEntity
#pragma warning disable EF1001 
            typeof(EntityQueryable<>), // DbContext.SomeEntity.Where(x => true).GetType(),
#pragma warning restore EF1001
            typeof(QueryRootExpression),
        };
        public static JsonSerializerOptions DefaultJsonSerializerOptions => new JsonSerializerOptions { IncludeFields = true };
    }
}
