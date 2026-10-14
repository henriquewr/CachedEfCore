using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CachedEfCore.PostgreSQL.SqlAnalysis
{
    /// <summary>
    /// Simple postgre sql parser for detecting table identifiers that changes state
    /// <para>
    /// This parser assumes valid SQL syntax for performance reasons
    /// </para>
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class PostgreSqlParser
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private SqlSourceCode _sqlSourceCode = null!;
        private readonly List<ReadOnlyMemory<char>> _tablesIdentifiers = new();
        private readonly Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> _tablesAliases = new(ReadOnlyMemoryCharComparer.OrdinalIgnoreCase);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceSeparators()
        {
            var sqlSourceCode = _sqlSourceCode;

            while (sqlSourceCode.HasCurrent() && (sqlSourceCode.AdvanceIf(PostgreSqlSyntaxFacts.IsSeparator) || TryParseMultiLineComment() || TryParseSingleLineComment()))
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsOnlyText(scoped in ReadOnlySpan<char> text)
            => _sqlSourceCode.TryAdvanceText(text) && PostgreSqlSyntaxFacts.IsIdentifierPartCharacter(_sqlSourceCode.Current) == false;

        private HashSet<ReadOnlyMemory<char>> GetResult()
        {
            var result = new HashSet<ReadOnlyMemory<char>>(ReadOnlyMemoryCharComparer.OrdinalIgnoreCase);

            var tablesAliases = _tablesAliases;

            foreach (var item in _tablesIdentifiers)
            {
                if (tablesAliases.TryGetValue(item, out var aliases))
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
                    if (tablesAliases.TryGetValue(alias, out var nestedAliases))
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

        private readonly static FrozenSet<string> StateChangingKeywords = new[] { "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE", "COPY" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
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

                        case 'c' or 'C'
                            when IsOnlyText("COPY"):
                            ParseCopy();
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

        private bool ParseInsert()
        {
            // https://www.postgresql.org/docs/current/sql-insert.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("INTO"))
            {
                return false;
            }

            AdvanceSeparators();

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);

            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();

                var alias = ParseIdentifier(out var aliasEnclosed);
                AdvanceSeparators();

                AddOnlyTableAlias(alias, identifier);
            }

            if (sqlSourceCode.Current == '(')
            {
                ParseParenthesisExpression();
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("OVERRIDING"))
            {
                AdvanceSeparators();
                if (sqlSourceCode.TryAdvanceText("SYSTEM") || sqlSourceCode.TryAdvanceText("USER"))
                {
                    AdvanceSeparators();
                }

                sqlSourceCode.TryAdvanceText("VALUE");
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("DEFAULT"))
            {
                AdvanceSeparators();
                sqlSourceCode.TryAdvanceText("VALUES");
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("VALUES"))
            {
                do
                {
                    AdvanceSeparators();
                    ParseParenthesisExpression();
                } while (sqlSourceCode.HasCurrent() && sqlSourceCode.AdvanceIf(','));

                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("SELECT"))
            {
                ParseSelect();
            }

            if (sqlSourceCode.TryAdvanceText("ON"))
            {
                AdvanceSeparators();
                sqlSourceCode.TryAdvanceText("CONFLICT");
                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                    AdvanceSeparators();

                    AdvanceUntilNonExpressionKeyword(static keyword =>
                    {
                        if (keyword.Equals("DO", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, false);
                        }
                        return (false, false);
                    });
                    AdvanceSeparators();
                }
                else if (sqlSourceCode.TryAdvanceText("ON"))
                {
                    AdvanceSeparators();
                    sqlSourceCode.TryAdvanceText("CONSTRAINT");
                    ParseIdentifier(out _);
                    AdvanceSeparators();
                }

                if (sqlSourceCode.TryAdvanceText("DO"))
                {
                    AdvanceSeparators();
                }

                if (sqlSourceCode.TryAdvanceText("NOTHING"))
                {
                    AdvanceSeparators();
                }
                else if (sqlSourceCode.TryAdvanceText("UPDATE"))
                {
                    AdvanceSeparators();
                    sqlSourceCode.TryAdvanceText("SET");
                    AdvanceSeparators();
                }
            }

            if (sqlSourceCode.TryAdvanceText("RETURNING"))
            {
                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("WITH"))
                {
                    AdvanceSeparators();
                    if (sqlSourceCode.Current == '(')
                    {
                        ParseParenthesisExpression();
                        AdvanceSeparators();
                    }
                }

                if (sqlSourceCode.TryAdvanceText("*"))
                {
                    AdvanceSeparators();
                }
            }

            return true;
        }

        private bool ParseUpdate()
        {
            // https://www.postgresql.org/docs/current/sql-update.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.HasCurrent())
            {
                return false;
            }

            if (sqlSourceCode.TryAdvanceText("ONLY"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("*"))
            {
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();
            }

            if (!sqlSourceCode.HasCurrent())
            {
                return false;
            }

            if (!sqlSourceCode.TryAdvanceText("SET"))
            {
                ParseIdentifier(out _);
                AdvanceSeparators();
                if (!sqlSourceCode.TryAdvanceText("SET"))
                {
                    return false;
                }
            }

            AddTableIdentifier(identifier);

            return true;
        }

        private bool ParseDelete()
        {
            // https://www.postgresql.org/docs/current/sql-delete.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("FROM"))
            {
                return false;
            }
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("ONLY"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("*"))
            {
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();
                ParseIdentifier(out _);
                AdvanceSeparators();
            }

            return true;
        }

        private bool ParseMerge()
        {
            // https://www.postgresql.org/docs/current/sql-merge.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("INTO"))
            {
                return false;
            }
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("ONLY"))
            {
                AdvanceSeparators();
            }

            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AddTableIdentifier(identifier);
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("*"))
            {
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();
                ParseIdentifier(out _);
                AdvanceSeparators();
            }
            else if (CanBeIdentifier())
            {
                ParseIdentifier(out _);
                AdvanceSeparators();
            }

            if (!sqlSourceCode.TryAdvanceText("USING"))
            {
                return false;
            }
            AdvanceSeparators();

            if (sqlSourceCode.TryAdvanceText("ONLY"))
            {
                AdvanceSeparators();
            }

            if (sqlSourceCode.Current == '(')
            {
                ParseParenthesisExpression();
                AdvanceSeparators();
            }
            else
            {
                ParseIdentifierOrMemberExpression(out _);
                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("*"))
                {
                    AdvanceSeparators();
                }
            }

            if (sqlSourceCode.TryAdvanceText("AS"))
            {
                AdvanceSeparators();
                ParseIdentifier(out _);
                AdvanceSeparators();
            }
            else if (CanBeIdentifier())
            {
                ParseIdentifier(out _);
                AdvanceSeparators();
            }

            if (!sqlSourceCode.TryAdvanceText("ON"))
            {
                return false;
            }

            while (sqlSourceCode.HasCurrent() && sqlSourceCode.Current != ')')
            {
                AdvanceSeparators();

                AdvanceUntilNonExpressionKeyword(keyword =>
                {
                    if (keyword.Equals("THEN", StringComparison.OrdinalIgnoreCase))
                    {
                        AdvanceSeparators();

                        if (sqlSourceCode.TryAdvanceText("DO"))
                        {
                            AdvanceSeparators();
                            sqlSourceCode.TryAdvanceText("NOTHING");
                        }
                        else
                        {
                            _ = sqlSourceCode.TryAdvanceText("INSERT")
                                || sqlSourceCode.TryAdvanceText("UPDATE")
                                || sqlSourceCode.TryAdvanceText("DELETE");
                        }

                        return (false, false);
                    }
                    else if (IsAnyStateChangingKeyword(keyword))
                    {
                        return (true, true);
                    }

                    return (false, false);
                });
                AdvanceSeparators();
            }

            return true;
        }

        private bool ParseTruncate()
        {
            // https://www.postgresql.org/docs/current/sql-truncate.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.TryAdvanceText("TABLE"))
            {
                AdvanceSeparators();
            }

            do
            {
                if (sqlSourceCode.TryAdvanceText("ONLY"))
                {
                    AdvanceSeparators();
                }

                var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
                AddTableIdentifier(identifier);
                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("*"))
                {
                    AdvanceSeparators();
                }
            } while (sqlSourceCode.HasCurrent() && sqlSourceCode.TryAdvanceText(","));

            if (sqlSourceCode.TryAdvanceText("RESTART") || sqlSourceCode.TryAdvanceText("CONTINUE"))
            {
                AdvanceSeparators();
                if (!sqlSourceCode.TryAdvanceText("IDENTITY"))
                {
                    return false;
                }
                AdvanceSeparators();
            }

            if (sqlSourceCode.TryAdvanceText("CASCADE"))
            {
                AdvanceSeparators();
            }
            else if (sqlSourceCode.TryAdvanceText("RESTRICT"))
            {
                AdvanceSeparators();
            }

            return true;
        }

        private static readonly SearchValues<char> CopyParseParenthesisExpressionSearchValues = SearchValues.Create([
            ..ParseParenthesisExpressionSearchChars, 
            //'S', 's', not useful
            //'V', 'v', not useful
            'I', 'i',
            'U', 'u',
            'D', 'd',
            'M', 'm',
        ]);
        private bool ParseCopy()
        {
            // https://www.postgresql.org/docs/current/sql-copy.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.Current == '(')
            {
                // Confirmed COPY TO
                ParseParenthesisQuery();
            }
            else
            {
                // COPY TO or COPY FROM
                var identifier = ParseIdentifierOrMemberExpression(out var enclosed, out var _);
                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisQuery();
                    AdvanceSeparators();
                }

                if (sqlSourceCode.TryAdvanceText("FROM"))
                {
                    // COPY FROM
                    AddTableIdentifier(identifier);
                }
            }

            return true;

            void ParseParenthesisQuery() 
                => ParseParenthesisExpression(CopyParseParenthesisExpressionSearchValues, static postgreSqlParser =>
                {
                    var current = postgreSqlParser._sqlSourceCode.Current;

                    switch (current)
                    {
                        case 'I' or 'i'
                            when postgreSqlParser.IsOnlyText("INSERT"):
                            postgreSqlParser.ParseInsert();
                            break;

                        case 'U' or 'u'
                            when postgreSqlParser.IsOnlyText("UPDATE"):
                            postgreSqlParser.ParseUpdate();
                            break;

                        case 'D' or 'd'
                            when postgreSqlParser.IsOnlyText("DELETE"):
                            postgreSqlParser.ParseDelete();
                            break;

                        case 'M' or 'm'
                            when postgreSqlParser.IsOnlyText("MERGE"):
                            postgreSqlParser.ParseMerge();
                            break;

                        default:
                            postgreSqlParser._sqlSourceCode.Advance();
                            break;
                    }
                });
        }

        private static readonly SearchValues<char> CteParseParenthesisExpressionSearchValues = SearchValues.Create([
            ..ParseParenthesisExpressionSearchChars, 
            //'S', 's', not useful
            //'V', 'v', not useful
            'I', 'i',
            'U', 'u',
            'D', 'd',
            'M', 'm',
            //'T', 't', not useful
            'W', 'w',
        ]);
        private bool ParseCte()
        {
            // https://www.postgresql.org/docs/current/queries-with.html
            // https://www.postgresql.org/docs/18/sql-select.html with_query

            var sqlSourceCode = _sqlSourceCode;

            do
            {
                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("RECURSIVE"))
                {
                    AdvanceSeparators();
                }

                var identifier = ParseIdentifier(out var enclosed);
                AdvanceSeparators();

                if (sqlSourceCode.Current == '(')
                {
                    ParseParenthesisExpression();
                    AdvanceSeparators();
                }

                if (!sqlSourceCode.TryAdvanceText("AS"))
                {
                    return false;
                }
                AdvanceSeparators();

                if (sqlSourceCode.TryAdvanceText("NOT"))
                {
                    AdvanceSeparators();
                }

                if (sqlSourceCode.TryAdvanceText("MATERIALIZED"))
                {
                    AdvanceSeparators();
                }

                var canStop = false;

                ParseParenthesisExpression(CteParseParenthesisExpressionSearchValues, postgreSqlParser =>
                {
                    if (canStop)
                    {
                        postgreSqlParser._sqlSourceCode.Advance();
                        return;
                    }

                    var current = postgreSqlParser._sqlSourceCode.Current;

                    switch (current)
                    {
                        case 'I' or 'i'
                            when postgreSqlParser.IsOnlyText("INSERT"):
                            canStop |= postgreSqlParser.ParseInsert();
                            break;

                        case 'U' or 'u'
                            when postgreSqlParser.IsOnlyText("UPDATE"):
                            canStop |= postgreSqlParser.ParseUpdate();
                            break;

                        case 'D' or 'd'
                            when postgreSqlParser.IsOnlyText("DELETE"):
                            canStop |= postgreSqlParser.ParseDelete();
                            break;
                        
                        case 'M' or 'm'
                            when postgreSqlParser.IsOnlyText("MERGE"):
                            canStop |= postgreSqlParser.ParseMerge();
                            break;

                        case 'W' or 'w'
                            when postgreSqlParser.IsOnlyText("WITH"):
                            postgreSqlParser.ParseCte();
                            break;

                        default:
                            postgreSqlParser._sqlSourceCode.Advance();
                            break;
                    }
                });

                AdvanceSeparators();
            }
            while (sqlSourceCode.HasCurrent() && sqlSourceCode.AdvanceIf(','));

            return true;
        }

        private void ParseSelect()
        {
            // https://www.postgresql.org/docs/current/sql-select.html

            AdvanceSeparators();

            var sqlSourceCode = _sqlSourceCode;

            if (sqlSourceCode.TryAdvanceText("ALL") || sqlSourceCode.TryAdvanceText("DISTINCT"))
            {
                AdvanceSeparators();
            }
        }

        private bool CanBeIdentifier()
        {
            var current = _sqlSourceCode.Current;
            var isIdentifier = current switch
            {
                '"' => true,
                _ => PostgreSqlSyntaxFacts.IsIdentifierFirstCharacter(current),
            };
            return isIdentifier;
        }

        private ReadOnlyMemory<char> ParseIdentifier(out bool enclosed)
        {
            // enclosed identifiers may not have separators
            // update"identifier"
            AdvanceSeparators();

            // https://www.postgresql.org/docs/18/sql-syntax-lexical.html#SQL-SYNTAX-IDENTIFIERS

            var sqlSourceCode = _sqlSourceCode;

            bool canBeUnicodeEscapedIdentifier = false;

            switch (sqlSourceCode.Current)
            {
                case '"':
                    {
                        var identifier = ParseDoubleQuoteEnclosedIdentifier();
                        enclosed = true;

                        ReadOnlyMemory<char> enclosingIdentifier;

                        if (identifier.DoubleQuotesCount == 2)
                        {
                            enclosingIdentifier = sqlSourceCode.Sql.AsMemory(identifier.Start, identifier.Length);
                        }
                        else
                        {
                            // "really""bad""identifier" is a valid identifier -> really"bad"identifier

                            var enclosingIdentifierStr = sqlSourceCode.Sql.Substring(identifier.Start, identifier.Length);

                            enclosingIdentifier = enclosingIdentifierStr.Replace("\"\"", "\"").AsMemory();
                        }

                        if (canBeUnicodeEscapedIdentifier)
                        {
                            AdvanceSeparators();

                            if (sqlSourceCode.TryAdvanceText("UESCAPE"))
                            {
                                AdvanceSeparators();
                                ParseStringLiteral();
                            }
                        }

                        return enclosingIdentifier;
                    }
                default:
                    {
                        if (sqlSourceCode.TryAdvanceText("U&"))
                        {
                            canBeUnicodeEscapedIdentifier = true;
                            // escaped Unicode identifier
                            goto case '"';
                        }

                        var startIndex = sqlSourceCode.Index;
                        sqlSourceCode.Expect(PostgreSqlSyntaxFacts.IsIdentifierFirstCharacter);
                        sqlSourceCode.AdvanceUntil(static c => !PostgreSqlSyntaxFacts.IsIdentifierPartCharacter(c));
                    
                        var identifier = sqlSourceCode.Sql.AsMemory(startIndex, sqlSourceCode.Index - startIndex);

                        // may be a reserved Keyword
                        enclosed = false;

                        return identifier;
                    }
            }
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

            var sqlSourceCode = _sqlSourceCode;

            while (sqlSourceCode.HasCurrent())
            {
                AdvanceSeparators();

                if (!sqlSourceCode.HasCurrent() || !sqlSourceCode.AdvanceIf('.'))
                {
                    return lastIdentifier;
                }

                AdvanceSeparators();

                if (!sqlSourceCode.HasCurrent() || sqlSourceCode.TryAdvanceText("*"))
                {
                    return lastIdentifier;
                }

                isMemberExpression = true;
                AdvanceSeparators();
                lastIdentifier = ParseIdentifier(out lastIsEnclosed);
            }

            return lastIdentifier;
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

                                if (PostgreSqlSyntaxFacts.IsKeyword(lastIdentifier.Span))
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

        private bool TryParseSingleLineComment()
        {
            var sqlSourceCode = _sqlSourceCode;

            if (!sqlSourceCode.TryAdvanceText("--"))
            {
                return false;
            }

            while (sqlSourceCode.HasCurrent() && sqlSourceCode.AdvanceIf(static c => !PostgreSqlSyntaxFacts.IsNewLine(c)))
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

        private static ReadOnlySpan<char> ParseParenthesisExpressionSearchChars => ['\'', '"', '-', '/', '(', ')'];
        private readonly static SearchValues<char> ParseParenthesisExpressionSearchValues = SearchValues.Create(ParseParenthesisExpressionSearchChars);
        private void ParseParenthesisExpression()
        {
            ParseParenthesisExpression(ParseParenthesisExpressionSearchValues, static postgreSqlParser =>
            {
                postgreSqlParser._sqlSourceCode.Advance();
            });
        }
        
        private void ParseParenthesisExpression(SearchValues<char> searchValues, Action<PostgreSqlParser> onDefault)
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

        private (ReadOnlyMemory<char>? Identifier, ReadOnlyMemory<char>? Alias) ParseGetTableMemberExpressionOrTableMemberExpressionAsAlias()
        {
            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AdvanceSeparators();

            if (!enclosed && PostgreSqlSyntaxFacts.IsKeyword(identifier.Span))
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

                if (!hasAs && !aliasEnclosed && PostgreSqlSyntaxFacts.IsKeyword(alias.Span))
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

        private static class PostgreSqlSyntaxFacts
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSeparator(char c)
                => char.IsWhiteSpace(c) || c == ';';

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsIdentifierFirstCharacter(char c)
                => char.IsLetter(c) || c == '_';

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsIdentifierPartCharacter(char c)
                => char.IsLetterOrDigit(c) || c == '$' || c == '_';

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
                "A", "ABORT", "ABS", "ABSENT", "ABSOLUTE", "ACCESS", "ACCORDING", "ACOS", "ACTION", "ADA", "ADD", "ADMIN", "AFTER", 
                "AGGREGATE", "ALL", "ALLOCATE", "ALSO", "ALTER", "ALWAYS", "ANALYSE", "ANALYZE", "AND", "ANY", "ANY_VALUE",
                "ARE", "ARRAY", "ARRAY_AGG", "ARRAY_​MAX_​CARDINALITY", "AS", "ASC", "ASENSITIVE", "ASIN", "ASSERTION", 
                "ASSIGNMENT", "ASYMMETRIC", "AT", "ATAN", "ATOMIC", "ATTACH", "ATTRIBUTE", "ATTRIBUTES", "AUTHORIZATION",
                "AVG", "BACKWARD", "BASE64", "BEFORE", "BEGIN", "BEGIN_FRAME", "BEGIN_PARTITION", "BERNOULLI", "BETWEEN", 
                "BIGINT", "BINARY", "BIT", "BIT_LENGTH", "BLOB", "BLOCKED", "BOM", "BOOLEAN", "BOTH", "BREADTH", "BTRIM", 
                "BY", "C", "CACHE", "CALL", "CALLED", "CARDINALITY", "CASCADE", "CASCADED", "CASE", "CAST", "CATALOG", 
                "CATALOG_NAME", "CEIL", "CEILING", "CHAIN", "CHAINING", "CHAR", "CHARACTER", "CHARACTERISTICS", "CHARACTERS", 
                "CHARACTER_LENGTH", "CHARACTER_​SET_​CATALOG", "CHARACTER_SET_NAME", "CHARACTER_SET_SCHEMA", "CHAR_LENGTH", 
                "CHECK", "CHECKPOINT", "CLASS", "CLASSIFIER", "CLASS_ORIGIN", "CLOB", "CLOSE", "CLUSTER", "COALESCE", "COBOL",
                "COLLATE", "COLLATION", "COLLATION_CATALOG", "COLLATION_NAME", "COLLATION_SCHEMA", "COLLECT", "COLUMN", "COLUMNS",
                "COLUMN_NAME", "COMMAND_FUNCTION", "COMMAND_​FUNCTION_​CODE", "COMMENT", "COMMENTS", "COMMIT", "COMMITTED",
                "COMPRESSION", "CONCURRENTLY", "CONDITION", "CONDITIONAL", "CONDITION_NUMBER", "CONFIGURATION", "CONFLICT",
                "CONNECT", "CONNECTION", "CONNECTION_NAME", "CONSTRAINT", "CONSTRAINTS", "CONSTRAINT_CATALOG", "CONSTRAINT_NAME",
                "CONSTRAINT_SCHEMA", "CONSTRUCTOR", "CONTAINS", "CONTENT", "CONTINUE", "CONTROL", "CONVERSION", "CONVERT",
                "COPARTITION", "COPY", "CORR", "CORRESPONDING", "COS", "COSH", "COST", "COUNT", "COVAR_POP", "COVAR_SAMP",
                "CREATE", "CROSS", "CSV", "CUBE", "CUME_DIST", "CURRENT", "CURRENT_CATALOG", "CURRENT_DATE", "CURRENT_​DEFAULT_​TRANSFORM_​GROUP",
                "CURRENT_PATH", "CURRENT_ROLE", "CURRENT_ROW", "CURRENT_SCHEMA", "CURRENT_TIME", "CURRENT_TIMESTAMP",
                "CURRENT_​TRANSFORM_​GROUP_​FOR_​TYPE", "CURRENT_USER", "CURSOR", "CURSOR_NAME", "CYCLE", "DATA", "DATABASE",
                "DATALINK", "DATE", "DATETIME_​INTERVAL_​CODE", "DATETIME_​INTERVAL_​PRECISION", "DAY", "DB", "DEALLOCATE",
                "DEC", "DECFLOAT", "DECIMAL", "DECLARE", "DEFAULT", "DEFAULTS", "DEFERRABLE", "DEFERRED", "DEFINE", "DEFINED",
                "DEFINER", "DEGREE", "DELETE", "DELIMITER", "DELIMITERS", "DENSE_RANK", "DEPENDS", "DEPTH", "DEREF", "DERIVED",
                "DESC", "DESCRIBE", "DESCRIPTOR", "DETACH", "DETERMINISTIC", "DIAGNOSTICS", "DICTIONARY", "DISABLE", "DISCARD",
                "DISCONNECT", "DISPATCH", "DISTINCT", "DLNEWCOPY", "DLPREVIOUSCOPY", "DLURLCOMPLETE", "DLURLCOMPLETEONLY", 
                "DLURLCOMPLETEWRITE", "DLURLPATH", "DLURLPATHONLY", "DLURLPATHWRITE", "DLURLSCHEME", "DLURLSERVER", "DLVALUE",
                "DO", "DOCUMENT", "DOMAIN", "DOUBLE", "DROP", "DYNAMIC", "DYNAMIC_FUNCTION", "DYNAMIC_​FUNCTION_​CODE", "EACH",
                "ELEMENT", "ELSE", "EMPTY", "ENABLE", "ENCODING", "ENCRYPTED", "END", "END-EXEC", "END_FRAME", "END_PARTITION",
                "ENFORCED", "ENUM", "EQUALS", "ERROR", "ESCAPE", "EVENT", "EVERY", "EXCEPT", "EXCEPTION", "EXCLUDE", "EXCLUDING",
                "EXCLUSIVE", "EXEC", "EXECUTE", "EXISTS", "EXP", "EXPLAIN", "EXPRESSION", "EXTENSION", "EXTERNAL", "EXTRACT",
                "FALSE", "FAMILY", "FETCH", "FILE", "FILTER", "FINAL", "FINALIZE", "FINISH", "FIRST", "FIRST_VALUE", "FLAG", 
                "FLOAT", "FLOOR", "FOLLOWING", "FOR", "FORCE", "FOREIGN", "FORMAT", "FORTRAN", "FORWARD", "FOUND", "FRAME_ROW",
                "FREE", "FREEZE", "FROM", "FS", "FULFILL", "FULL", "FUNCTION", "FUNCTIONS", "FUSION", "G", "GENERAL", "GENERATED",
                "GET", "GLOBAL", "GO", "GOTO", "GRANT", "GRANTED", "GREATEST", "GROUP", "GROUPING", "GROUPS", "HANDLER", "HAVING", 
                "HEADER", "HEX", "HIERARCHY", "HOLD", "HOUR", "ID", "IDENTITY", "IF", "IGNORE", "ILIKE", "IMMEDIATE", "IMMEDIATELY",
                "IMMUTABLE", "IMPLEMENTATION", "IMPLICIT", "IMPORT", "IN", "INCLUDE", "INCLUDING", "INCREMENT", "INDENT", "INDEX", 
                "INDEXES", "INDICATOR", "INHERIT", "INHERITS", "INITIAL", "INITIALLY", "INLINE", "INNER", "INOUT", "INPUT", "INSENSITIVE", 
                "INSERT", "INSTANCE", "INSTANTIABLE", "INSTEAD", "INT", "INTEGER", "INTEGRITY", "INTERSECT", "INTERSECTION", "INTERVAL", 
                "INTO", "INVOKER", "IS", "ISNULL", "ISOLATION", "JOIN", "JSON", "JSON_ARRAY", "JSON_ARRAYAGG", "JSON_EXISTS", "JSON_OBJECT",
                "JSON_OBJECTAGG", "JSON_QUERY", "JSON_SCALAR", "JSON_SERIALIZE", "JSON_TABLE", "JSON_TABLE_PRIMITIVE", "JSON_VALUE", "K",
                "KEEP", "KEY", "KEYS", "KEY_MEMBER", "KEY_TYPE", "LABEL", "LAG", "LANGUAGE", "LARGE", "LAST", "LAST_VALUE", "LATERAL", 
                "LEAD", "LEADING", "LEAKPROOF", "LEAST", "LEFT", "LENGTH", "LEVEL", "LIBRARY", "LIKE", "LIKE_REGEX", "LIMIT", "LINK", 
                "LISTAGG", "LISTEN", "LN", "LOAD", "LOCAL", "LOCALTIME", "LOCALTIMESTAMP", "LOCATION", "LOCATOR", "LOCK", "LOCKED", "LOG",
                "LOG10", "LOGGED", "LOWER", "LPAD", "LTRIM", "M", "MAP", "MAPPING", "MATCH", "MATCHED", "MATCHES", "MATCH_NUMBER", 
                "MATCH_RECOGNIZE", "MATERIALIZED", "MAX", "MAXVALUE", "MEASURES", "MEMBER", "MERGE", "MERGE_ACTION", "MESSAGE_LENGTH",
                "MESSAGE_OCTET_LENGTH", "MESSAGE_TEXT", "METHOD", "MIN", "MINUTE", "MINVALUE", "MOD", "MODE", "MODIFIES", "MODULE", 
                "MONTH", "MORE", "MOVE", "MULTISET", "MUMPS", "NAME", "NAMES", "NAMESPACE", "NATIONAL", "NATURAL", "NCHAR", "NCLOB", 
                "NESTED", "NESTING", "NEW", "NEXT", "NFC", "NFD", "NFKC", "NFKD", "NIL", "NO", "NONE", "NORMALIZE", "NORMALIZED", "NOT",
                "NOTHING", "NOTIFY", "NOTNULL", "NOWAIT", "NTH_VALUE", "NTILE", "NULL", "NULLABLE", "NULLIF", "NULLS", "NULL_ORDERING",
                "NUMBER", "NUMERIC", "OBJECT", "OBJECTS", "OCCURRENCE", "OCCURRENCES_REGEX", "OCTETS", "OCTET_LENGTH", "OF", "OFF", "OFFSET",
                "OIDS", "OLD", "OMIT", "ON", "ONE", "ONLY", "OPEN", "OPERATOR", "OPTION", "OPTIONS", "OR", "ORDER", "ORDERING", "ORDINALITY", 
                "OTHERS", "OUT", "OUTER", "OUTPUT", "OVER", "OVERFLOW", "OVERLAPS", "OVERLAY", "OVERRIDING", "OWNED", "OWNER", "P", "PAD", 
                "PARALLEL", "PARAMETER", "PARAMETER_MODE", "PARAMETER_NAME", "PARAMETER_​ORDINAL_​POSITION", "PARAMETER_​SPECIFIC_​CATALOG", 
                "PARAMETER_​SPECIFIC_​NAME", "PARAMETER_​SPECIFIC_​SCHEMA", "PARSER", "PARTIAL", "PARTITION", "PASCAL", "PASS", "PASSING", 
                "PASSTHROUGH", "PASSWORD", "PAST", "PATH", "PATTERN", "PER", "PERCENT", "PERCENTILE_CONT", "PERCENTILE_DISC", "PERCENT_RANK",
                "PERIOD", "PERMISSION", "PERMUTE", "PIPE", "PLACING", "PLAN", "PLANS", "PLI", "POLICY", "PORTION", "POSITION", "POSITION_REGEX",
                "POWER", "PRECEDES", "PRECEDING", "PRECISION", "PREPARE", "PREPARED", "PRESERVE", "PREV", "PRIMARY", "PRIOR", "PRIVATE",
                "PRIVILEGES", "PROCEDURAL", "PROCEDURE", "PROCEDURES", "PROGRAM", "PRUNE", "PTF", "PUBLIC", "PUBLICATION", "QUOTE", "QUOTES",
                "RANGE", "RANK", "READ", "READS", "REAL", "REASSIGN", "RECOVERY", "RECURSIVE", "REF", "REFERENCES", "REFERENCING", "REFRESH", 
                "REGR_AVGX", "REGR_AVGY", "REGR_COUNT", "REGR_INTERCEPT", "REGR_R2", "REGR_SLOPE", "REGR_SXX", "REGR_SXY", "REGR_SYY", "REINDEX",
                "RELATIVE", "RELEASE", "RENAME", "REPEATABLE", "REPLACE", "REPLICA", "REQUIRING", "RESET", "RESPECT", "RESTART", "RESTORE", 
                "RESTRICT", "RESULT", "RETURN", "RETURNED_CARDINALITY", "RETURNED_LENGTH", "RETURNED_​OCTET_​LENGTH", "RETURNED_SQLSTATE", 
                "RETURNING", "RETURNS", "REVOKE", "RIGHT", "ROLE", "ROLLBACK", "ROLLUP", "ROUTINE", "ROUTINES", "ROUTINE_CATALOG", "ROUTINE_NAME", 
                "ROUTINE_SCHEMA", "ROW", "ROWS", "ROW_COUNT", "ROW_NUMBER", "RPAD", "RTRIM", "RULE", "RUNNING", "SAVEPOINT", "SCALAR", 
                "SCALE", "SCHEMA", "SCHEMAS", "SCHEMA_NAME", "SCOPE", "SCOPE_CATALOG", "SCOPE_NAME", "SCOPE_SCHEMA", "SCROLL", "SEARCH", 
                "SECOND", "SECTION", "SECURITY", "SEEK", "SELECT", "SELECTIVE", "SELF", "SEMANTICS", "SENSITIVE", "SEQUENCE", "SEQUENCES",
                "SERIALIZABLE", "SERVER", "SERVER_NAME", "SESSION", "SESSION_USER", "SET", "SETOF", "SETS", "SHARE", "SHOW", "SIMILAR", 
                "SIMPLE", "SIN", "SINH", "SIZE", "SKIP", "SMALLINT", "SNAPSHOT", "SOME", "SORT_DIRECTION", "SOURCE", "SPACE", "SPECIFIC", 
                "SPECIFICTYPE", "SPECIFIC_NAME", "SQL", "SQLCODE", "SQLERROR", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "SQRT", "STABLE", 
                "STANDALONE", "START", "STATE", "STATEMENT", "STATIC", "STATISTICS", "STDDEV_POP", "STDDEV_SAMP", "STDIN", "STDOUT", "STORAGE",
                "STORED", "STRICT", "STRING", "STRIP", "STRUCTURE", "STYLE", "SUBCLASS_ORIGIN", "SUBMULTISET", "SUBSCRIPTION", "SUBSET", "SUBSTRING",
                "SUBSTRING_REGEX", "SUCCEEDS", "SUM", "SUPPORT", "SYMMETRIC", "SYSID", "SYSTEM", "SYSTEM_TIME", "SYSTEM_USER", "T", "TABLE", 
                "TABLES", "TABLESAMPLE", "TABLESPACE", "TABLE_NAME", "TAN", "TANH", "TARGET", "TEMP", "TEMPLATE", "TEMPORARY", "TEXT", "THEN", 
                "THROUGH", "TIES", "TIME", "TIMESTAMP", "TIMEZONE_HOUR", "TIMEZONE_MINUTE", "TO", "TOKEN", "TOP_LEVEL_COUNT", "TRAILING", "TRANSACTION",
                "TRANSACTIONS_​COMMITTED", "TRANSACTIONS_​ROLLED_​BACK", "TRANSACTION_ACTIVE", "TRANSFORM", "TRANSFORMS", "TRANSLATE", "TRANSLATE_REGEX",
                "TRANSLATION", "TREAT", "TRIGGER", "TRIGGER_CATALOG", "TRIGGER_NAME", "TRIGGER_SCHEMA", "TRIM", "TRIM_ARRAY", "TRUE", "TRUNCATE", "TRUSTED",
                "TYPE", "TYPES", "UESCAPE", "UNBOUNDED", "UNCOMMITTED", "UNCONDITIONAL", "UNDER", "UNENCRYPTED", "UNION", "UNIQUE", "UNKNOWN", "UNLINK", 
                "UNLISTEN", "UNLOGGED", "UNMATCHED", "UNNAMED", "UNNEST", "UNTIL", "UNTYPED", "UPDATE", "UPPER", "URI", "USAGE", "USER", 
                "USER_​DEFINED_​TYPE_​CATALOG", "USER_​DEFINED_​TYPE_​CODE", "USER_​DEFINED_​TYPE_​NAME", "USER_​DEFINED_​TYPE_​SCHEMA", "USING", "UTF16", 
                "UTF32", "UTF8", "VACUUM", "VALID", "VALIDATE", "VALIDATOR", "VALUE", "VALUES", "VALUE_OF", "VARBINARY", "VARCHAR", "VARIADIC", 
                "VARYING", "VAR_POP", "VAR_SAMP", "VERBOSE", "VERSION", "VERSIONING", "VIEW", "VIEWS", "VIRTUAL", "VOLATILE", "WHEN", "WHENEVER",
                "WHERE", "WHITESPACE", "WIDTH_BUCKET", "WINDOW", "WITH", "WITHIN", "WITHOUT", "WORK", "WRAPPER", "WRITE", "XML", "XMLAGG", 
                "XMLATTRIBUTES", "XMLBINARY", "XMLCAST", "XMLCOMMENT", "XMLCONCAT", "XMLDECLARATION", "XMLDOCUMENT", "XMLELEMENT", "XMLEXISTS", 
                "XMLFOREST", "XMLITERATE", "XMLNAMESPACES", "XMLPARSE", "XMLPI", "XMLQUERY", "XMLROOT", "XMLSCHEMA", "XMLSERIALIZE", "XMLTABLE", 
                "XMLTEXT", "XMLVALIDATE", "YEAR", "YES", "ZONE" 
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            public static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KeywordsSpanLookup = Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }
}
