using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;

namespace CachedEfCore.SqlAnalysis.SqlServer
{
    public partial class SqlServerQueryEntityExtractor : ISqlQueryEntityExtractor
    {
        [ThreadStatic]
        private static SqlServerParser _sqlServerParser = null!;

        public IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(TableEntityMapping tableEntities, string sql)
        {
            _sqlServerParser ??= new();

            var tables = _sqlServerParser.Parse(sql);

            var spanLookup = tableEntities.Mapping.GetAlternateLookup<ReadOnlySpan<char>>();

            foreach (var table in tables)
            {
                if (spanLookup.TryGetValue(table.Span, out var entityTypes))
                {
                    foreach (var entityType in entityTypes)
                    {
                        yield return entityType;
                    }
                }
            }
        }
    }
}