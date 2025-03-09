using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CachedEfCore.SqlAnalysis
{
    public interface ISqlQueryEntityExtractor
    {
        IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(IDictionary<string, ImmutableArray<IEntityType>> tableEntities, string sql);
    }
}