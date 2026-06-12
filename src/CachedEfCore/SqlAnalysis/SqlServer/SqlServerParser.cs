using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CachedEfCore.SqlAnalysis.SqlServer
{
    /// <summary>
    /// Simple sql server parser for detecting table identifiers that changes state
    /// <para>
    /// This parser assumes valid SQL syntax for performance reasons
    /// </para>
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class SqlServerParser
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private SqlSourceCode _sqlSourceCode = null!;
        private readonly List<ReadOnlyMemory<char>> _tablesIdentifiers = new();
        private readonly Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> _tablesAliases = new(ReadOnlyMemoryCharComparer.OrdinalIgnoreCase);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceSeparators()
        {
            var sqlSourceCode = _sqlSourceCode;

            while (sqlSourceCode.HasCurrent() && (sqlSourceCode.AdvanceIf(SqlServerSyntaxFacts.IsSeparator) || TryParseMultiLineComment() || TryParseSingleLineComment()))
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsOnlyText(scoped in ReadOnlySpan<char> text)
            => _sqlSourceCode.TryAdvanceText(text) && SqlServerSyntaxFacts.IsIdentifierPartCharacter(_sqlSourceCode.Current) == false;

        private HashSet<ReadOnlyMemory<char>> GetResult()
        {
            var result = new HashSet<ReadOnlyMemory<char>>(ReadOnlyMemoryCharComparer.OrdinalIgnoreCase);

            foreach (var item in _tablesIdentifiers)
            {
                if (_tablesAliases.TryGetValue(item, out var aliases))
                {
                    PopulateTables(aliases);
                }
                else
                {
                    result.Add(item);
                }
            }

            return result;

            void PopulateTables(List<ReadOnlyMemory<char>> aliases)
            {
                foreach (var alias in aliases)
                {
                    if (_tablesAliases.TryGetValue(alias, out var nestedAliases))
                    {
                        PopulateTables(nestedAliases);
                    }
                    else
                    {
                        result.Add(alias);
                    }
                }
            }
        }

        private readonly static FrozenSet<string> StateChangingKeywords = new[] { "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE", "BULK" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        private readonly static FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> StateChangingKeywordsSpanLookup = StateChangingKeywords.GetAlternateLookup<ReadOnlySpan<char>>();
        private static bool IsAnyStateChangingKeyword(scoped in ReadOnlySpan<char> value)
        {
            return StateChangingKeywordsSpanLookup.Contains(value);
        }

        public HashSet<ReadOnlyMemory<char>> Parse(string sql)
        {
            var sqlSourceCode = new SqlSourceCode(sql);

            _sqlSourceCode = sqlSourceCode;

            try
            {
                AdvanceSeparators();

                if (IsOnlyText("WITH"))
                {
                    ParseCte();
                    AdvanceSeparators();
                }

                while (sqlSourceCode.HasCurrent())
                {
                    var current = sqlSourceCode.Current;

                    switch (current)
                    {
                        case 'u' or 'U'
                            when IsOnlyText("UPDATE"):
                            ParseUpdate();
                            break;

                        case 'd' or 'D'
                            when IsOnlyText("DELETE"):
                            ParseDelete();
                            break;

                        case 'i' or 'I'
                            when IsOnlyText("INSERT"):
                            ParseInsert();
                            break;

                        case 'm' or 'M'
                            when IsOnlyText("MERGE"):
                            ParseMerge();
                            break;

                        case 't' or 'T'
                            when IsOnlyText("TRUNCATE"):
                            ParseTruncate();
                            break;

                        case 'b' or 'B'
                            when IsOnlyText("BULK"):
                            ParseBulkInsert();
                            break;

                        case '(':
                            ParseParenthesisExpression();
                            AdvanceSeparators();
                            break;

                        case '\'':
                            ParseStringLiteral();
                            AdvanceSeparators();
                            break;

                        default:
                            if (CanBeIdentifier())
                            {
                                ParseIdentifierOrMemberExpression(out _);
                                AdvanceSeparators();
                            }
                            else
                            {
                                sqlSourceCode.Advance();
                            }
                            break;
                    }
                }

                var result = GetResult().ToHashSet();

                return result;
            }
            finally
            {
                sqlSourceCode.Dispose();
                _sqlSourceCode = null!;

                _tablesIdentifiers.Clear();
                _tablesAliases.Clear();
            }
        }
                
        private void ParseInsert()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/insert-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            TryParseTopExpressionPercent();

            if (_sqlSourceCode.TryAdvanceText("INTO"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);

            AdvanceSeparators();

            if (_sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
            }

            if (_sqlSourceCode.Current == '(')
            {
                ParseParenthesisExpression();
                AdvanceSeparators();
            }

            if (_sqlSourceCode.TryAdvanceText("OUTPUT"))
            {
                AdvanceSeparators();
                ParseOutputClause(static keyword => keyword.Equals("VALUES", StringComparison.OrdinalIgnoreCase)
                    || keyword.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
                    || keyword.Equals("EXEC", StringComparison.OrdinalIgnoreCase)
                    || keyword.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase)
                );
            }

            AdvanceSeparators();

            if (_sqlSourceCode.TryAdvanceText("VALUES"))
            {
                do
                {
                    AdvanceSeparators();
                    ParseParenthesisExpression();
                } while (_sqlSourceCode.HasCurrent() && _sqlSourceCode.AdvanceIf(','));

                AdvanceSeparators();
            }
            else if (_sqlSourceCode.TryAdvanceText("DEFAULT"))
            {
                AdvanceSeparators();
                _sqlSourceCode.TryAdvanceText("VALUES");
                AdvanceSeparators();
            }
        }

        private void ParseUpdate()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/queries/update-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            TryParseTopExpressionPercent();

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
                AdvanceSeparators();
            }

            sqlSourceCode.Expect("SET");

            while (sqlSourceCode.HasCurrent())
            {
                AdvanceSeparators();
                var columnExpressionLastIdentifier = ParseIdentifierOrMemberExpression(out var isEnclosed, out var isMemberExpression);
                AdvanceSeparators();

                if (!isEnclosed && isMemberExpression && columnExpressionLastIdentifier.Span.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
                {
                    TryAdvanceParenthesis();

                    AdvanceSeparators();

                    if (sqlSourceCode.TryAdvanceText("FROM"))
                    {
                        // UPDATE u SET Value.WRITE('SQL ', 6, 0) FROM Test u WHERE Id = 1;
                        ParseTableMemberExpressionOrTableMemberExpressionAsAliasAddOnlyIfAliasEquals(identifier);
                        return;
                    }

                    if (sqlSourceCode.AdvanceIf(','))
                    {
                        continue;
                    }
                }

                sqlSourceCode.AdvanceUntil('=');
                sqlSourceCode.Advance();

                AdvanceSeparators();

                while (sqlSourceCode.HasCurrent())
                {
                    switch (sqlSourceCode.Current)
                    {
                        case '(':
                            ParseParenthesisExpression();
                            AdvanceSeparators();
                            break;
                        case '\'':
                            ParseStringLiteral();
                            AdvanceSeparators();
                            break;
                        case '-':
                            if (TryParseSingleLineComment())
                            {
                                break;
                            }
                            sqlSourceCode.Advance();
                            break;
                        case '/':
                            if (TryParseMultiLineComment())
                            {
                                break;
                            }
                            sqlSourceCode.Advance();
                            break;

                        default:
                            if (CanBeIdentifier())
                            {
                                var savePos = sqlSourceCode.Index;
                                var lastIdentifier = ParseIdentifierOrMemberExpression(out var cannotBeFunc);

                                if (!cannotBeFunc)
                                {
                                    var lastIdentifierSpan = lastIdentifier.Span;

                                    if (SqlServerSyntaxFacts.IsKeyword(lastIdentifier.Span))
                                    {
                                        if (lastIdentifierSpan.Equals("FROM", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ParseTableMemberExpressionOrTableMemberExpressionAsAliasAddOnlyIfAliasEquals(identifier);
                                            return;
                                        }
                                        else if (lastIdentifierSpan.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase))
                                        {
                                            AdvanceSeparators();
                                            ParseOutputClause();
                                            AdvanceSeparators();

                                            if (sqlSourceCode.TryAdvanceText("FROM"))
                                            {
                                                ParseTableMemberExpressionOrTableMemberExpressionAsAliasAddOnlyIfAliasEquals(identifier);
                                            }
                                            else if (sqlSourceCode.TryAdvanceText("OPTION"))
                                            {
                                                ParseOptionClause();
                                            }

                                            return;
                                        }
                                        else if (IsAnyStateChangingKeyword(lastIdentifierSpan))
                                        {
                                            sqlSourceCode.Index = savePos;
                                            return;
                                        }
                                    }

                                    AdvanceSeparators();

                                    if (sqlSourceCode.HasCurrent())
                                    {
                                        TryAdvanceParenthesis();
                                    }
                                }
                            }
                            else
                            {
                                sqlSourceCode.AdvanceUntil(static c => c == ',' || SqlServerSyntaxFacts.IsSeparator(c));
                            }

                            AdvanceSeparators();
                            break;
                    }

                    if (!sqlSourceCode.HasCurrent())
                    {
                        return;
                    }

                    if (sqlSourceCode.AdvanceIf(','))
                    {
                        break;
                    }
                    else
                    {
                        AdvanceSeparators();
                    }
                }
            }

            return;

            bool TryAdvanceParenthesis()
            {
                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                    return true;
                }

                AdvanceSeparators();

                return false;
            }
        }

        private void ParseDelete()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/delete-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            TryParseTopExpressionPercent();

            if (_sqlSourceCode.TryAdvanceText("FROM"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            if (_sqlSourceCode.TryAdvanceText("OUTPUT"))
            {
                ParseOutputClause();
                AdvanceSeparators();
            }

            if (_sqlSourceCode.TryAdvanceText("FROM"))
            {
                AdvanceSeparators();
            }
            else if (_sqlSourceCode.TryAdvanceText("OPTION"))
            {
                ParseOptionClause();
                AdvanceSeparators();
            }

            if (!_sqlSourceCode.HasCurrent())
            {
                return;
            }

            if (CanBeIdentifier())
            {
                ParseTableMemberExpressionOrTableMemberExpressionAsAliasAddOnlyIfAliasEquals(identifier);
            }
        }

        private void ParseMerge()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/merge-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            TryParseTopExpressionPercent();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.TryAdvanceText("INTO"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
                AdvanceSeparators();
            }

            if (!sqlSourceCode.TryAdvanceText("USING"))
            {
                if (sqlSourceCode.TryAdvanceText("AS"))
                {
                    AdvanceSeparators();
                }

                var alias = ParseIdentifier(out var _);
                AdvanceSeparators();
                AddTableAlias(alias, identifier);
                AdvanceSeparators();

                sqlSourceCode.TryAdvanceText("USING");
            }

            AdvanceSeparators();

            if (sqlSourceCode.Current == '(')
            {
                ParseParenthesisExpression();
            }
            else
            {
                var tableSource = ParseIdentifierOrMemberExpression(out var tableSourceIsEnclosed);
            }

            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();
                var alias = ParseIdentifier(out var _);
                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                }
            }

            sqlSourceCode.TryAdvanceText("ON");

            while (sqlSourceCode.HasCurrent())
            {
                AdvanceSeparators();

                var jumpNumber = 0;
                AdvanceUntilNonExpressionKeyword(keyword =>
                {
                    if (keyword.Equals("WHEN", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpNumber = 1;
                        return (true, false);
                    }
                    else if (keyword.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpNumber = 2;
                        return (true, false);
                    }
                    else if (keyword.Equals("OPTION", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpNumber = 3;
                        return (true, false);
                    }
                    else if (IsAnyStateChangingKeyword(keyword))
                    {
                        jumpNumber = 4;
                        return (true, true);
                    }

                    return (false, false);
                });
                AdvanceSeparators();

                switch (jumpNumber)
                {
                    case 1:
                        if (sqlSourceCode.TryAdvanceText("MATCHED"))
                        {
                            ParseMergeThenMatched();
                        }
                        else if (sqlSourceCode.TryAdvanceText("NOT"))
                        {
                            AdvanceSeparators();
                            sqlSourceCode.TryAdvanceText("MATCHED");
                            AdvanceSeparators();

                            if (sqlSourceCode.TryAdvanceText("BY"))
                            {
                                AdvanceSeparators();
                                if (sqlSourceCode.TryAdvanceText("TARGET"))
                                {
                                    ParseMergeThenNotMatched();
                                }
                                else if (sqlSourceCode.TryAdvanceText("SOURCE"))
                                {
                                    ParseMergeThenMatched();
                                }
                            }
                            else
                            {
                                ParseMergeThenNotMatched();
                            }
                        }
                        break;

                    case 2:
                        ParseOutputClause(static keyword =>
                        {
                            return keyword.Equals("OPTION", StringComparison.OrdinalIgnoreCase)
                                || IsAnyStateChangingKeyword(keyword);
                        });
                        AdvanceSeparators();
                        break;
                    case 3:
                        ParseOptionClause();
                        AdvanceSeparators();
                        break;

                    case 0:
                    case 4:
                    default:
                        return;
                }
            }

            void ParseMergeThenMatched()
            {
                AdvanceUntilNonExpressionKeyword(static keyword => (keyword.Equals("THEN", StringComparison.OrdinalIgnoreCase), false));

                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("UPDATE"))
                {
                    AdvanceSeparators();

                    sqlSourceCode.Expect("SET");

                    while (sqlSourceCode.HasCurrent())
                    {
                        AdvanceSeparators();
                        var columnExpressionLastIdentifier = ParseIdentifierOrMemberExpression(out var isEnclosed, out var isMemberExpression);
                        AdvanceSeparators();

                        if (!isEnclosed && isMemberExpression && columnExpressionLastIdentifier.Span.Equals("WRITE", StringComparison.OrdinalIgnoreCase))
                        {
                            TryAdvanceParenthesis();

                            if (!sqlSourceCode.HasCurrent())
                            {
                                return;
                            }

                            AdvanceSeparators();

                            if (sqlSourceCode.AdvanceIf(','))
                            {
                                continue;
                            }

                            return;
                        }

                        sqlSourceCode.AdvanceUntil('=');
                        sqlSourceCode.Advance();

                        AdvanceSeparators();

                        while (sqlSourceCode.HasCurrent())
                        {
                            switch (sqlSourceCode.Current)
                            {
                                case '(':
                                    ParseParenthesisExpression();
                                    AdvanceSeparators();
                                    break;
                                case '\'':
                                    ParseStringLiteral();
                                    AdvanceSeparators();
                                    break;
                                case '-':
                                    if (TryParseSingleLineComment())
                                    {
                                        break;
                                    }
                                    sqlSourceCode.Advance();
                                    break;
                                case '/':
                                    if (TryParseMultiLineComment())
                                    {
                                        break;
                                    }
                                    sqlSourceCode.Advance();
                                    break;

                                default:
                                    if (CanBeIdentifier())
                                    {
                                        var savePos = sqlSourceCode.Index;

                                        var lastIdentifier = ParseIdentifierOrMemberExpression(out var cannotBeFunc);

                                        if (!cannotBeFunc)
                                        {
                                            AdvanceSeparators();
                                            var lastIdentifierSpan = lastIdentifier.Span;

                                            if (lastIdentifierSpan.Equals("WHEN", StringComparison.OrdinalIgnoreCase))
                                            {
                                                AdvanceSeparators();

                                                if (sqlSourceCode.TryAdvanceText("NOT") || sqlSourceCode.TryAdvanceText("MATCHED"))
                                                {
                                                    sqlSourceCode.Index = savePos;
                                                    return;
                                                }
                                            }
                                            else if (lastIdentifierSpan.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase))
                                            {
                                                AdvanceSeparators();
                                                ParseOutputClause();
                                                AdvanceSeparators();

                                                if (sqlSourceCode.TryAdvanceText("OPTION"))
                                                {
                                                    ParseOptionClause();
                                                }

                                                return;
                                            }
                                            else if (sqlSourceCode.TryAdvanceText("OPTION"))
                                            {
                                                ParseOptionClause();
                                            }
                                            else if (IsAnyStateChangingKeyword(lastIdentifierSpan))
                                            {
                                                sqlSourceCode.Index = savePos;
                                                return;
                                            }

                                            if (sqlSourceCode.HasCurrent())
                                            {
                                                TryAdvanceParenthesis();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        sqlSourceCode.AdvanceUntil(static c => c == ',' || SqlServerSyntaxFacts.IsSeparator(c));
                                    }

                                    AdvanceSeparators();
                                    break;
                            }

                            if (!sqlSourceCode.HasCurrent())
                            {
                                return;
                            }

                            if (sqlSourceCode.AdvanceIf(','))
                            {
                                break;
                            }
                            else
                            {
                                AdvanceSeparators();
                            }
                        }
                    }

                    bool TryAdvanceParenthesis()
                    {
                        if (sqlSourceCode.Current == '(')
                        {
                            ParseParenthesisExpression();
                            return true;
                        }

                        AdvanceSeparators();

                        return false;
                    }
                }
                else if (sqlSourceCode.TryAdvanceText("DELETE"))
                {
                    AdvanceSeparators();
                }
            }

            void ParseMergeThenNotMatched()
            {
                AdvanceUntilNonExpressionKeyword(static keyword => (keyword.Equals("THEN", StringComparison.OrdinalIgnoreCase), false));

                AdvanceSeparators();

                sqlSourceCode.TryAdvanceText("INSERT");

                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                    AdvanceSeparators();
                }

                if (sqlSourceCode.TryAdvanceText("VALUES"))
                {
                    AdvanceSeparators();
                    ParseParenthesisExpression();
                }
                else if (sqlSourceCode.TryAdvanceText("DEFAULT"))
                {
                    AdvanceSeparators();
                    sqlSourceCode.TryAdvanceText("VALUES");
                }

                AdvanceSeparators();
            }
        }

        private void ParseTruncate()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/truncate-table-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            sqlSourceCode.TryAdvanceText("TABLE");
            AdvanceSeparators();

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            if (sqlSourceCode.HasCurrent())
            {
                return;
            }

            if (sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
                AdvanceSeparators();
            }
        }

        private void ParseBulkInsert()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/statements/bulk-insert-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("INSERT"))
            {
                return;
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed, out var _);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            sqlSourceCode.TryAdvanceText("FROM");
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
                AdvanceSeparators();
            }
        }

        private static readonly SearchValues<char> CteParseParenthesisExpressionWithSSearchValues = SearchValues.Create([..ParseParenthesisExpressionSearchChars, 'S', 's']);
        private void ParseCte()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/queries/with-common-table-expression-transact-sql?view=sql-server-ver17

            var sqlSourceCode = _sqlSourceCode;

            do
            {
                AdvanceSeparators();
                var identifier = ParseIdentifier(out var enclosed);
                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                    AdvanceSeparators();
                }

                if (!sqlSourceCode.TryAdvanceText("AS"))
                {
                    return;
                }

                AdvanceSeparators();

                var hasSeenSelect = false;

                ParseParenthesisExpression(CteParseParenthesisExpressionWithSSearchValues, sqlServerParser =>
                {
                    if (hasSeenSelect == false && sqlServerParser.IsOnlyText("SELECT"))
                    {
                        var identifiers = ParseSelect();

                        if (identifiers.Identifier.HasValue)
                        {
                            AddOnlyTableAlias(identifier, identifiers.Identifier.Value);
                        }

                        hasSeenSelect = true;
                    }
                    else
                    {
                        sqlServerParser._sqlSourceCode.Advance();
                    }
                });

                AdvanceSeparators();
            }
            while (sqlSourceCode.HasCurrent() && sqlSourceCode.AdvanceIf(','));
        }

        private (ReadOnlyMemory<char>? Identifier, ReadOnlyMemory<char>? Alias) ParseSelect()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/queries/select-clause-transact-sql?view=sql-server-ver17

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.TryAdvanceText("ALL") || sqlSourceCode.TryAdvanceText("DISTINCT"))
            {
                AdvanceSeparators();
            }

            if (TryParseTopExpressionPercent())
            {
                if (sqlSourceCode.TryAdvanceText("WITH"))
                {
                    AdvanceSeparators();
                    sqlSourceCode.TryAdvanceText("TIES");
                }
                AdvanceSeparators();
            }

            (ReadOnlyMemory<char>? Identifier, ReadOnlyMemory<char>? Alias) fromTable = (null, null);

            int jumpIndex = 0;

            do
            {
                AdvanceUntilKeyword(keyword =>
                {
                    if (jumpIndex < 1 && keyword.Equals("FROM", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 1;
                        return (true, false);
                    }

                    if (jumpIndex < 2 && keyword.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 2;
                        return (true, false);
                    }

                    if (jumpIndex < 3 && keyword.Equals("GROUP", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 3;
                        return (true, false);
                    }

                    if (jumpIndex < 4 && keyword.Equals("HAVING", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 4;
                        return (true, false);
                    }

                    if (jumpIndex < 5 && keyword.Equals("ORDER", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 5;
                        return (true, false);
                    }

                    if (jumpIndex < 6 && keyword.Equals("OPTION", StringComparison.OrdinalIgnoreCase))
                    {
                        jumpIndex = 6;
                        return (true, false);
                    }

                    if (jumpIndex < 7 && IsAnyStateChangingKeyword(keyword))
                    {
                        jumpIndex = 7;
                        return (true, true);
                    }

                    return (false, false);
                });

                AdvanceSeparators();

                switch (jumpIndex)
                {
                    // FROM
                    case 1:
                        fromTable = ParseGetTableMemberExpressionOrTableMemberExpressionAsAlias();
                        break;

                    // WHERE
                    case 2:
                        break;

                    // GROUP BY
                    case 3:
                        sqlSourceCode.TryAdvanceText("BY");

                        break;

                    // HAVING
                    case 4:
                        break;

                    // ORDER BY
                    case 5:
                        sqlSourceCode.TryAdvanceText("BY");

                        break;

                    // OPTION
                    case 6:
                        break;
                    case 7:
                    default:
                        return fromTable;
                }

                AdvanceSeparators();
            }
            while (sqlSourceCode.HasCurrent() && sqlSourceCode.Current != ')');

            return fromTable;
        }

        private bool CanBeIdentifier()
        {
            var current = _sqlSourceCode.Current;
            var isIdentifier = current switch
            {
                '[' => true,
                '"' => true,
                _ => SqlServerSyntaxFacts.IsIdentifierFirstCharacter(current),
            };
            return isIdentifier;
        }

        private ReadOnlyMemory<char> ParseIdentifier(out bool enclosed)
        {
            // enclosed identifiers may not have separators
            // update[identifier]
            AdvanceSeparators();

            switch (_sqlSourceCode.Current)
            {
                case '[':
                    {
                        // https://learn.microsoft.com/en-sg/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#bracket-delimited-identifiers

                        var identifier = ParseBracketEnclosedIdentifier();
                        enclosed = true;

                        if (!identifier.Escaped)
                        {
                            var enclosingIdentifier = _sqlSourceCode.Sql.AsMemory(identifier.Start, identifier.Length);
                            return enclosingIdentifier;
                        }
                        else
                        {
                            // [really]]bad]]identifier] is a valid identifier -> really]bad]identifier

                            var enclosingIdentifier = _sqlSourceCode.Sql.Substring(identifier.Start, identifier.Length);

                            enclosingIdentifier = enclosingIdentifier.Replace("]]", "]");

                            return enclosingIdentifier.AsMemory();
                        }
                    }
                case '"':
                    {
                        // https://learn.microsoft.com/en-sg/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#double-quote-delimited-identifiers

                        var identifier = ParseDoubleQuoteEnclosedIdentifier();
                        enclosed = true;

                        if (identifier.DoubleQuotesCount == 2)
                        {
                            var enclosingIdentifier = _sqlSourceCode.Sql.AsMemory(identifier.Start, identifier.Length);
                            return enclosingIdentifier;
                        }
                        else
                        {
                            // "really""bad""identifier" is a valid identifier -> really"bad"identifier

                            var enclosingIdentifier = _sqlSourceCode.Sql.Substring(identifier.Start, identifier.Length);

                            enclosingIdentifier = enclosingIdentifier.Replace("\"\"", "\"");

                            return enclosingIdentifier.AsMemory();
                        }
                    }

                default:
                    {
                        // https://learn.microsoft.com/en-sg/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#rules-for-regular-identifiers

                        var startIndex = _sqlSourceCode.Index;
                        _sqlSourceCode.Expect(SqlServerSyntaxFacts.IsIdentifierFirstCharacter);
                        _sqlSourceCode.AdvanceUntil(static c => !SqlServerSyntaxFacts.IsIdentifierPartCharacter(c));
                    
                        var identifier = _sqlSourceCode.Sql.AsMemory(startIndex, _sqlSourceCode.Index - startIndex);

                        // may be a reserved Keyword
                        enclosed = false;

                        return identifier;
                    }
            }
        }

        private (int Start, int Length, bool Escaped) ParseBracketEnclosedIdentifier()
        {
            const char closingCharacter = ']';

            var sqlSourceCode = _sqlSourceCode;

            var startIndex = sqlSourceCode.Index;

            sqlSourceCode.Expect('[');

            var hasEscaped = false;

            while (sqlSourceCode.HasCurrent())
            {
                sqlSourceCode.AdvanceUntil(closingCharacter);
                sqlSourceCode.Advance();

                if (sqlSourceCode.AdvanceIf(closingCharacter))
                {
                    hasEscaped = true;
                }
                else
                {
                    break;
                }
            }

            var finalStartIndex = startIndex + 1;
            var endIndex = sqlSourceCode.Index - finalStartIndex - 1;

            return (finalStartIndex, endIndex, hasEscaped);
        }

        private (int Start, int Length, int DoubleQuotesCount) ParseDoubleQuoteEnclosedIdentifier()
        {
            const char closingCharacter = '"';

            var sqlSourceCode = _sqlSourceCode;

            var startIndex = sqlSourceCode.Index;

            var doubleQuotesCount = 1;
            sqlSourceCode.Expect(closingCharacter);

            while (sqlSourceCode.HasCurrent())
            {
                sqlSourceCode.AdvanceUntil(closingCharacter);
                sqlSourceCode.Advance();
                ++doubleQuotesCount;

                if (sqlSourceCode.AdvanceIf(closingCharacter))
                {
                    ++doubleQuotesCount;
                }
                else if ((doubleQuotesCount & 1) == 0)
                {
                    break;
                }
            }

            var finalStartIndex = startIndex + 1;
            var endIndex = sqlSourceCode.Index - finalStartIndex - 1;

            return (finalStartIndex, endIndex, doubleQuotesCount);
        }

        /// <returns>Last identifier</returns>
        private ReadOnlyMemory<char> ParseIdentifierOrMemberExpression(out bool lastIsEnclosed)
            => ParseIdentifierOrMemberExpression(out lastIsEnclosed, out _);

        /// <returns>Last identifier</returns>
        private ReadOnlyMemory<char> ParseIdentifierOrMemberExpression(out bool lastIsEnclosed, out bool isMemberExpression)
        {
            var lastIdentifier = ParseIdentifier(out lastIsEnclosed);
            isMemberExpression = false;

            while (_sqlSourceCode.HasCurrent() && _sqlSourceCode.AdvanceIf('.'))
            {
                isMemberExpression = true;
                AdvanceSeparators();
                lastIdentifier = ParseIdentifier(out lastIsEnclosed);
            }

            return lastIdentifier;
        }

        /// <summary>
        /// Finds the exit of the output by keywords FROM WHERE OPTION
        /// </summary>
        private void ParseOutputClause()
        {
            ParseOutputClause(static keyword =>
            {
                return keyword.Equals("FROM", StringComparison.OrdinalIgnoreCase)
                    || keyword.Equals("WHERE", StringComparison.OrdinalIgnoreCase)
                    || keyword.Equals("OPTION", StringComparison.OrdinalIgnoreCase);
            });
        }

        private void AdvanceUntilNonExpressionKeyword(Func<ReadOnlySpan<char>, (bool IsEnd, bool ReturnToLastIndexWhenIsEnd)> isEnd)
        {
            AdvanceUntilKeyword(keyword => 
            {
                if (keyword.Equals("CASE", StringComparison.OrdinalIgnoreCase))
                {
                    ParseCase();
                    return (false, false);
                }
                return isEnd(keyword);
            });
        }

        private void ParseCase()
        {
            var caseCount = 1;
            AdvanceUntilKeyword(keyword =>
            {
                if (keyword.Equals("CASE", StringComparison.OrdinalIgnoreCase))
                {
                    caseCount++;
                }
                else if (keyword.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    caseCount--;
                    return (caseCount == 0, false);
                }
                return (false, false);
            });
        }

        private void AdvanceUntilKeyword(Func<ReadOnlySpan<char>, (bool IsEnd, bool ReturnToLastIndexWhenIsEnd)> isEnd)
        {
            var sqlSourceCode = _sqlSourceCode;

            while (sqlSourceCode.HasCurrent())
            {
                var current = sqlSourceCode.Current;

                switch (current)
                {
                    case '(':
                        ParseParenthesisExpression();
                        break;

                    case ')':
                        // was already inside (
                        return;
                    case '\'':
                        ParseStringLiteral();
                        break;
                    case '-':
                        if (TryParseSingleLineComment())
                        {
                            break;
                        }

                        sqlSourceCode.Advance();
                        break;

                    case '/':
                        if (TryParseMultiLineComment())
                        {
                            break;
                        }

                        sqlSourceCode.Advance();
                        break;
                    default:
                        if (CanBeIdentifier())
                        {
                            var lastIndex = sqlSourceCode.Index;
                            var lastIdentifier = ParseIdentifierOrMemberExpression(out var cannotBeKeyword);

                            if (!cannotBeKeyword)
                            {
                                var lastIdentifierSpan = lastIdentifier.Span;

                                if (SqlServerSyntaxFacts.IsKeyword(lastIdentifier.Span))
                                {
                                    var isEndResult = isEnd(lastIdentifierSpan);

                                    if (isEndResult.IsEnd)
                                    {
                                        if (isEndResult.ReturnToLastIndexWhenIsEnd)
                                        {
                                            sqlSourceCode.Index = lastIndex;
                                        }

                                        return;
                                    }
                                }
                            }
                        }
                        else if (char.IsDigit(current))
                        {
                            sqlSourceCode.AdvanceUntil(static c => !char.IsDigit(c));
                        }
                        else
                        {
                            sqlSourceCode.Advance();
                        }

                        break;
                }

                if (!sqlSourceCode.HasCurrent())
                {
                    return;
                }

                AdvanceSeparators();
            }
        }

        private void ParseOutputClause(Func<ReadOnlySpan<char>, bool> isEnd)
        {
            AdvanceUntilKeyword(keyword =>
            {
                if (keyword.Equals("INTO", StringComparison.OrdinalIgnoreCase))
                {
                    AdvanceSeparators();
                    var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
                    AddTableIdentifier(identifier);
                    return (true, false);
                }

                return (isEnd(keyword), true);
            });
        }

        private void ParseOptionClause()
        {
            if (_sqlSourceCode.TryAdvanceText("OPTION"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
            }
        }

        private bool TryParseTopExpressionPercent()
        {
            // [ TOP ( expression ) [ PERCENT ] ]

            if (_sqlSourceCode.TryAdvanceText("TOP"))
            {
                AdvanceSeparators();

                ParseParenthesisExpression();

                AdvanceSeparators();

                if (_sqlSourceCode.TryAdvanceText("PERCENT"))
                {
                    AdvanceSeparators();
                }
                return true;
            }

            return false;
        }

        private bool TryParseSingleLineComment()
        {
            if (!_sqlSourceCode.TryAdvanceText("--"))
            {
                return false;
            }

            while (_sqlSourceCode.HasCurrent() && _sqlSourceCode.AdvanceIf(static c => !SqlServerSyntaxFacts.IsNewLine(c)))
            {
            }

            return true;
        }

        private bool TryParseMultiLineComment()
        {
            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("/*"))
            {
                return false;
            }

            var commentsDepth = 1;

            while (sqlSourceCode.HasCurrent())
            {
                if (sqlSourceCode.TryAdvanceText("/*"))
                {
                    ++commentsDepth;
                }

                if (sqlSourceCode.TryAdvanceText("*/") && --commentsDepth == 0)
                {
                    return true;
                }

                sqlSourceCode.Advance();
            }

            return true;
        }

        private static ReadOnlySpan<char> ParseParenthesisExpressionSearchChars => ['\'', '[', '"', '-', '/', '(', ')'];
        private readonly static SearchValues<char> ParseParenthesisExpressionSearchValues = SearchValues.Create(ParseParenthesisExpressionSearchChars);
        private void ParseParenthesisExpression()
        {
            ParseParenthesisExpression(ParseParenthesisExpressionSearchValues, static sqlServerParser =>
            {
                sqlServerParser._sqlSourceCode.Advance();
            });
        }

        private void ParseParenthesisExpression(SearchValues<char> searchValues, Action<SqlServerParser> onDefault)
        {
            var sqlSourceCode = _sqlSourceCode;

            sqlSourceCode.Expect('(');

            var openingCount = 1;
            while (sqlSourceCode.HasCurrent() && openingCount > 0)
            {
                var index = sqlSourceCode.Remaining.IndexOfAny(searchValues);

                if (index == -1 || index == sqlSourceCode.Sql.Length)
                {
                    sqlSourceCode.Index = sqlSourceCode.Sql.Length;
                    return;
                }

                sqlSourceCode.Index += index;

                switch (sqlSourceCode.Current)
                {
                    case '\'':
                        ParseStringLiteral();
                        break;

                    case '[':
                        ParseBracketEnclosedIdentifier();
                        break;

                    case '"':
                        ParseDoubleQuoteEnclosedIdentifier();
                        break;

                    case '-':
                        if (TryParseSingleLineComment())
                        {
                            break;
                        }

                        sqlSourceCode.Advance();
                        break;

                    case '/':
                        if (TryParseMultiLineComment())
                        {
                            break;
                        }

                        sqlSourceCode.Advance();
                        break;

                    case '(':
                        ++openingCount;
                        sqlSourceCode.Advance();
                        break;

                    case ')':
                        --openingCount;
                        sqlSourceCode.Advance();
                        break;

                    default:
                        onDefault(this);
                        break;
                }

                AdvanceSeparators();
            }
        }

        private void ParseStringLiteral()
        {
            var sqlSourceCode = _sqlSourceCode;

            sqlSourceCode.Expect('\'');

            var characterCount = 1;

            while (sqlSourceCode.HasCurrent())
            {
                sqlSourceCode.AdvanceUntil('\'');
                sqlSourceCode.Advance();
                ++characterCount;

                if (!sqlSourceCode.HasCurrent())
                {
                    return;
                }

                if (sqlSourceCode.AdvanceIf('\''))
                {
                    ++characterCount;
                }
                else if ((characterCount & 1) == 0)
                {
                    break;
                }
            }
        }

        private void ParseTableMemberExpressionOrTableMemberExpressionAsAlias()
        {
            (var identifier, var alias) = ParseGetTableMemberExpressionOrTableMemberExpressionAsAlias();

            if (identifier is null)
            {
                return;
            }

            AddTableIdentifier(identifier.Value);

            if (alias is not null)
            {
                AddTableAlias(alias.Value, identifier.Value);
            }
        }

        private (ReadOnlyMemory<char>? Identifier, ReadOnlyMemory<char>? Alias) ParseGetTableMemberExpressionOrTableMemberExpressionAsAlias()
        {
            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AdvanceSeparators();

            if (!enclosed && SqlServerSyntaxFacts.IsKeyword(identifier.Span))
            {
                return (null, null);
            }

            var sqlSourceCode = _sqlSourceCode;

            var hasAs = sqlSourceCode.TryAdvanceText("AS", StringComparison.OrdinalIgnoreCase);

            if (hasAs)
            {
                AdvanceSeparators();
            }
            else if (!sqlSourceCode.HasCurrent())
            {
                return (identifier, null);
            }

            if (CanBeIdentifier())
            {
                var start = sqlSourceCode.Index;

                var alias = ParseIdentifier(out var aliasEnclosed);

                if (!hasAs && !aliasEnclosed && SqlServerSyntaxFacts.IsKeyword(alias.Span))
                {
                    sqlSourceCode.Index = start;
                    return (identifier, null);
                }

                AdvanceSeparators();

                return (identifier, alias);
            }

            return (identifier, null);
        }

        private void ParseTableMemberExpressionOrTableMemberExpressionAsAliasAddOnlyIfAliasEquals(scoped in ReadOnlyMemory<char> firstIdentifier)
        {
            (var identifier, var alias) = ParseGetTableMemberExpressionOrTableMemberExpressionAsAlias();

            if (identifier is null || alias is null
                || !alias.Value.Span.Equals(firstIdentifier.Span, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AddTableIdentifier(identifier.Value);

            AddTableAlias(alias.Value, identifier.Value);
        }

        private void AddTableAlias(scoped in ReadOnlyMemory<char> alias, scoped in ReadOnlyMemory<char> identifier)
        {
            AddOnlyTableAlias(alias, identifier);

            AddTableIdentifier(alias);
        }

        private void AddOnlyTableAlias(scoped in ReadOnlyMemory<char> alias, scoped in ReadOnlyMemory<char> identifier)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_tablesAliases, alias, out var exists);
            if (!exists)
            {
                list = new List<ReadOnlyMemory<char>>(2);
            }
            list!.Add(identifier);
        }

        private void AddTableIdentifier(scoped in ReadOnlyMemory<char> identifier)
        {
            _tablesIdentifiers.Add(identifier);
        }

        private sealed class ReadOnlyMemoryCharComparer : IEqualityComparer<ReadOnlyMemory<char>>
        {
            public static readonly ReadOnlyMemoryCharComparer OrdinalIgnoreCase = new(StringComparison.OrdinalIgnoreCase);

            private readonly StringComparison _comparison;

            public ReadOnlyMemoryCharComparer(StringComparison comparison)
            {
                _comparison = comparison;
            }

            public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
            {
                return x.Span.Equals(y.Span, _comparison);
            }

            public int GetHashCode(ReadOnlyMemory<char> obj)
            {
                return string.GetHashCode(obj.Span, _comparison);
            }
        }

        private static class SqlServerSyntaxFacts
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSeparator(char c)
                => char.IsWhiteSpace(c) || c == ';';

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsIdentifierFirstCharacter(char c)
                => char.IsLetter(c) || c == '_' || c == '@' || c == '#';

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsIdentifierPartCharacter(char c)
                => char.IsLetterOrDigit(c) || c == '@' || c == '$' || c == '#' || c == '_';

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNewLine(char ch)
            {
                // new-line-character:
                //   Carriage return character (U+000D)
                //   Line feed character (U+000A)
                //   Next line character (U+0085)
                //   Line separator character (U+2028)
                //   Paragraph separator character (U+2029)

                return ch == '\r'
                    || ch == '\n'
                    || ch == '\u0085'
                    || ch == '\u2028'
                    || ch == '\u2029';
            }

            public static bool IsKeyword(scoped in ReadOnlySpan<char> identifier)
                => KeywordsSpanLookup.Contains(identifier);

            public static readonly FrozenSet<string> Keywords = new[] {
                "ABSOLUTE", "EXEC", "OVERLAPS", "ACTION", "EXECUTE", "PAD", "ADA", "EXISTS", "PARTIAL", "ADD",
                "EXTERNAL", "PASCAL", "ALL", "EXTRACT", "POSITION", "ALLOCATE", "FALSE", "PRECISION",
                "ALTER", "FETCH", "PREPARE", "AND", "FIRST", "PRESERVE", "ANY", "FLOAT", "PRIMARY", "ARE",
                "FOR", "PRIOR", "AS", "FOREIGN", "PRIVILEGES", "ASC", "FORTRAN", "PROCEDURE", "ASSERTION",
                "FOUND", "PUBLIC", "AT", "FROM", "READ", "AUTHORIZATION", "FULL", "REAL", "AVG", "GET", "REFERENCES",
                "BEGIN", "GLOBAL", "RELATIVE", "BETWEEN", "GO", "RESTRICT", "BIT", "GOTO", "REVOKE", "BIT_LENGTH",
                "GRANT", "RIGHT", "BOTH", "GROUP", "ROLLBACK", "BY", "HAVING", "ROWS", "CASCADE", "HOUR", "SCHEMA",
                "CASCADED", "IDENTITY", "SCROLL", "CASE", "IMMEDIATE", "SECOND", "CAST", "IN", "SECTION", "CATALOG",
                "INCLUDE", "SELECT", "CHAR", "INDEX", "SESSION", "CHAR_LENGTH", "INDICATOR", "SESSION_USER", "CHARACTER",
                "INITIALLY", "SET", "CHARACTER_LENGTH", "INNER", "SIZE", "CHECK", "INPUT", "SMALLINT", "CLOSE",
                "INSENSITIVE", "SOME", "COALESCE", "INSERT", "SPACE", "COLLATE", "INT", "SQL", "COLLATION", "INTEGER",
                "SQLCA", "COLUMN", "INTERSECT", "SQLCODE", "COMMIT", "INTERVAL", "SQLERROR", "CONNECT", "INTO",
                "SQLSTATE", "CONNECTION", "IS", "SQLWARNING", "CONSTRAINT", "ISOLATION", "SUBSTRING", "CONSTRAINTS",
                "JOIN", "SUM", "CONTINUE", "KEY", "SYSTEM_USER", "CONVERT", "LANGUAGE", "TABLE", "CORRESPONDING",
                "LAST", "TEMPORARY", "COUNT", "LEADING", "THEN", "CREATE", "LEFT", "TIME", "CROSS", "LEVEL", "TIMESTAMP",
                "CURRENT", "LIKE", "TIMEZONE_HOUR", "CURRENT_DATE", "LOCAL", "TIMEZONE_MINUTE", "CURRENT_TIME",
                "LOWER", "TO", "CURRENT_TIMESTAMP", "MATCH", "TRAILING", "CURRENT_USER", "MAX", "CURSOR", "MIN",
                "TRANSLATE", "DATE", "MINUTE", "TRANSLATION", "DAY", "MODULE", "TRIM", "DEALLOCATE", "MONTH", "TRUE",
                "DEC", "NAMES", "UNION", "DECIMAL", "NATIONAL", "UNIQUE", "DECLARE", "NATURAL", "UNKNOWN", "DEFAULT",
                "NCHAR", "UPDATE", "DEFERRABLE", "NEXT", "UPPER", "DEFERRED", "NO", "USAGE", "DELETE", "NONE", "USER",
                "DESC", "NOT", "USING", "DESCRIBE", "NULL", "VALUE", "DESCRIPTOR", "NULLIF", "VALUES", "DIAGNOSTICS",
                "NUMERIC", "VARCHAR", "DISCONNECT", "OCTET_LENGTH", "VARYING", "DISTINCT", "OF", "VIEW", "DOMAIN",
                "ON", "WHEN", "DOUBLE", "ONLY", "WHENEVER", "DROP", "OPEN", "WHERE", "ELSE", "WITH", "END", "OR", "WORK",
                "END-EXEC", "ORDER", "WRITE", "ESCAPE", "OUTER", "YEAR", "EXCEPT", "OUTPUT", "ZONE", "EXCEPTION",
                "FILE", "RAISERROR", "FILLFACTOR", "READTEXT", "RECONFIGURE", "FREETEXT", "FREETEXTTABLE", "REPLICATION",
                "BACKUP", "RESTORE", "FUNCTION", "RETURN", "BREAK", "REVERT", "BROWSE", "BULK", "HOLDLOCK", "ROWCOUNT",
                "ROWGUIDCOL", "IDENTITY_INSERT", "RULE", "CHECKPOINT", "IDENTITYCOL", "SAVE", "IF", "CLUSTERED",
                "SECURITYAUDIT", "SEMANTICKEYPHRASETABLE", "SEMANTICSIMILARITYDETAILSTABLE", "SEMANTICSIMILARITYTABLE",
                "COMPUTE", "CONTAINS", "SETUSER", "CONTAINSTABLE", "SHUTDOWN", "KILL", "STATISTICS", "LINENO", "LOAD",
                "TABLESAMPLE", "MERGE", "TEXTSIZE", "NOCHECK", "NONCLUSTERED", "TOP", "TRAN", "DATABASE", "TRANSACTION",
                "DBCC", "TRIGGER", "TRUNCATE", "OFF", "TRY_CONVERT", "OFFSETS", "TSEQUAL", "DENY", "OPENDATASOURCE",
                "UNPIVOT", "DISK", "OPENQUERY", "OPENROWSET", "UPDATETEXT", "DISTRIBUTED", "OPENXML", "USE", "OPTION",
                "DUMP", "OVER", "WAITFOR", "ERRLVL", "PERCENT", "PIVOT", "PLAN", "WHILE", "WITHIN GROUP", "PRINT",
                "WRITETEXT", "EXIT", "PROC" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            public static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KeywordsSpanLookup = Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }
}
