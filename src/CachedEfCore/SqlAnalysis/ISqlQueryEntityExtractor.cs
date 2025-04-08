using CachedEfCore.EntityMapping;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

namespace CachedEfCore.SqlAnalysis
{
    public interface ISqlQueryEntityExtractor
    {
        IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(TableEntityMapping tableEntities, string sql);
    }
}