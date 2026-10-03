using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CachedEfCore.SqlAnalysis
{
    public class GenericSqlQueryEntityExtractor : ISqlQueryEntityExtractor
    {
        public IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(TableEntityMapping tableEntities, string sql)
        {
            var result = tableEntities.Mapping.Where(x => sql.Contains(x.Key, StringComparison.OrdinalIgnoreCase)).SelectMany(static x => x.Value);

            return result;
        }
    }
}