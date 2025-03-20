using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Collections.Immutable;

namespace CachedEfCore.SqlAnalysis
{
    public partial class SqlQueryEntityExtractor : ISqlQueryEntityExtractor
    {
        public IEnumerable<IEntityType> GetStateChangingEntityTypesFromSql(IDictionary<string, ImmutableArray<IEntityType>> tableEntities, string sql)
        {
            var aliases = GetAliases(sql).Dictionary;

            var regex = StateChangingTables();
            var matches = regex.Matches(sql);

            foreach (Match match in matches)
            {
                ImmutableArray<IEntityType> entityTypes;
                var tableName = GetTableMatch(match, 8).Value;

                if (tableEntities.TryGetValue(tableName, out entityTypes))
                {
                    foreach (var entityType in entityTypes)
                    {
                        yield return entityType;
                    }
                }
                else if (aliases.TryGetValue(tableName, out var tableAliases))
                {
                    foreach (var tableAlias in tableAliases)
                    {
                        if (tableEntities.TryGetValue(tableAlias, out entityTypes))
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

        private static Dictionary<string, List<string>>.AlternateLookup<ReadOnlySpan<char>> GetAliases(string sql)
        {
            var regex = GetTableAliases();
            var matches = regex.Matches(sql);
            Dictionary<string, List<string>> aliases = new();
            var aliasesSpan = aliases.GetAlternateLookup<ReadOnlySpan<char>>();

            foreach (Match item in matches)
            {
                var table = GetTableMatch(item, 2).Value;
                var alias = GetTableMatch(item, 6).ValueSpan;

                ref var refVal = ref CollectionsMarshal.GetValueRefOrAddDefault(aliasesSpan, alias, out var exists);
                if (!exists)
                {
                    refVal = new(2);
                }
                
                refVal!.Add(table);
            }

            return aliasesSpan;
        }

        private static Group GetTableMatch(Match item, int index)
        {
            for (int i = index; i < index + 3; i++)
            {
                var group = item.Groups[i];
                if (group.Success)
                {
                    return group;
                }
            }

            throw new KeyNotFoundException();
        }

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string TablesRegex = @"(\[(.+?)\]|""(.+?)""|\s?([^\s\[\]"";]+)\s?)";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string StateChangingTablesRegex = @$"(UPDATE|DELETE\s+({TablesRegex}\s+)?FROM|INSERT\s+INTO)\s+{TablesRegex}";

        [StringSyntax(StringSyntaxAttribute.Regex)]
        private const string GetTableAliasesRegex = @$"FROM\s{TablesRegex}\s+(?:AS\s+)?{TablesRegex}";

        [GeneratedRegex(StateChangingTablesRegex, RegexOptions.IgnoreCase)]
        private static partial Regex StateChangingTables();

        [GeneratedRegex(GetTableAliasesRegex, RegexOptions.IgnoreCase)]
        private static partial Regex GetTableAliases();
    }
}