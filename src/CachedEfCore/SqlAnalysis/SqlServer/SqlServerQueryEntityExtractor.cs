using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CachedEfCore.SqlAnalysis.SqlServer
{
    public class SqlServerQueryEntityExtractor : ISqlQueryEntityExtractor
    {
        [ThreadStatic]
        private static SqlServerParser? _sqlServerParser;

        public IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(TableEntityMapping tableEntities, string sql)
        {
            try
            {
                _sqlServerParser ??= new();

                var tables = _sqlServerParser.Parse(sql);
                return GetTables(tableEntities, tables);
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                // Its better to incorrectly invalidate everything than throwing an exception
                return tableEntities.Mapping.SelectMany(static x => x.Value);
            }

            static IEnumerable<IEntityType> GetTables(TableEntityMapping tableEntities, HashSet<ReadOnlyMemory<char>> tables)
            {
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
}
