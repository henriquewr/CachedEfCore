using System;
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
    public class SqlServerParser
    {
        private SqlSourceCode _sqlSourceCode = null!;
        private readonly List<ReadOnlyMemory<char>> _tablesIdentifiers = new();
        private readonly Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>> _tablesAliases = new(ReadOnlyMemoryCharComparer.OrdinalIgnoreCase);

        private static bool IsSeparator(char c)
            => char.IsSeparator(c) || c == '\t' || c == '\n' || c == '\r' || c == ';';

        private static bool IsIdentifierFirstCharacter(char c)
            => char.IsLetter(c) || c == '_' || c == '@' || c == '#';

        private static bool IsIdentifierPartCharacter(char c)
            => char.IsLetterOrDigit(c) || c == '@' || c == '$' || c == '#' || c == '_';

        private void AdvanceSeparators()
        {
            while (_sqlSourceCode.HasCurrent() && _sqlSourceCode.AdvanceIf(IsSeparator))
            {
            }
        }

        private IEnumerable<ReadOnlyMemory<char>> GetResult()
        {
            foreach (var item in _tablesIdentifiers)
            {
                if (_tablesAliases.TryGetValue(item, out var aliases))
                {
                    foreach (var alias in aliases)
                    {
                        yield return alias;
                    }
                }
                else
                {
                    yield return item;
                }
            }
        }

        public HashSet<ReadOnlyMemory<char>> Parse(string sql)
        {
            _sqlSourceCode = new SqlSourceCode(sql);
            _tablesIdentifiers.Clear();
            _tablesAliases.Clear();

            AdvanceSeparators();

            while (_sqlSourceCode.HasCurrent())
            {
                var current = _sqlSourceCode.Current;

                switch (current)
                {
                    case 'u'
                        when IsOnlyText("UPDATE"):
                    case 'U'
                        when IsOnlyText("UPDATE"):
                        ParseUpdate();
                        break;

                    case 'd'
                        when IsOnlyText("DELETE"):
                    case 'D'
                        when IsOnlyText("DELETE"):
                        ParseDelete();
                        break;

                    case 'i'
                        when IsOnlyText("INSERT"):
                    case 'I'
                        when IsOnlyText("INSERT"):
                        ParseInsert();
                        break;

                    case '(':
                        ParseParenthesisExpression();
                        AdvanceSeparators();
                        break;

                    case '\'':
                        ParseEnclosedExpression('\'');
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
                            _sqlSourceCode.Advance();
                        }
                        break;
                }
            }

            var result = GetResult().ToHashSet();

            return result;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            bool IsOnlyText(scoped in ReadOnlySpan<char> text)
                => _sqlSourceCode.TryAdvanceText(text) && IsIdentifierPartCharacter(_sqlSourceCode.Current) == false;
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
            _tablesIdentifiers.Add(identifier);

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
            _tablesIdentifiers.Add(identifier);
            AdvanceSeparators();

            if (_sqlSourceCode.TryAdvanceText("WITH"))
            {
                AdvanceSeparators();
                ParseParenthesisExpression();
                AdvanceSeparators();
            }

            _sqlSourceCode.Expect("SET");

            while (_sqlSourceCode.HasCurrent())
            {
                AdvanceSeparators();
                ParseIdentifierOrMemberExpression(out var isEnclosed);
                AdvanceSeparators();

                if (!isEnclosed)
                {
                    TryAdvanceParenthesis();

                    AdvanceSeparators();

                    if (_sqlSourceCode.TryAdvanceText("FROM"))
                    {
                        // UPDATE u SET Value.WRITE('SQL ', 6, 0) FROM Test u WHERE Id = 1;
                        ParseTableMemberExpressionOrTableMemberExpressionAsAlias();
                        return;
                    }
                }

                _sqlSourceCode.AdvanceUntil('=');
                _sqlSourceCode.Advance();

                AdvanceSeparators();

                while (_sqlSourceCode.HasCurrent())
                {
                    switch (_sqlSourceCode.Current)
                    {
                        case '(':
                            ParseParenthesisExpression();
                            AdvanceSeparators();
                            break;
                        case '\'':
                            ParseEnclosedExpression('\'');
                            AdvanceSeparators();
                            break;

                        default:
                            if (CanBeIdentifier())
                            {
                                var lastIdentifier = ParseIdentifierOrMemberExpression(out var cannotBeFunc);

                                if (!cannotBeFunc)
                                {
                                    var lastIdentifierSpan = lastIdentifier.Span;

                                    if (SqlServerReservedKeywords.IsKeyword(lastIdentifier.Span))
                                    {
                                        if (lastIdentifierSpan.Equals("FROM", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ParseTableMemberExpressionOrTableMemberExpressionAsAlias();
                                            return;
                                        }
                                        else if (lastIdentifierSpan.Equals("OUTPUT", StringComparison.OrdinalIgnoreCase))
                                        {
                                            AdvanceSeparators();
                                            ParseOutputClause();
                                            AdvanceSeparators();

                                            if (_sqlSourceCode.TryAdvanceText("FROM"))
                                            {
                                                ParseTableMemberExpressionOrTableMemberExpressionAsAlias();
                                            }

                                            return;
                                        }

                                    }

                                    AdvanceSeparators();
                                    TryAdvanceParenthesis();
                                }
                            }
                            else
                            {
                                _sqlSourceCode.AdvanceUntil(static c => c == ',' || IsSeparator(c));
                            }

                            AdvanceSeparators();
                            break;
                    }

                    if (!_sqlSourceCode.HasCurrent())
                    {
                        return;
                    }

                    if (_sqlSourceCode.AdvanceIf(','))
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
                if (_sqlSourceCode.Current == '(')
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

            var identifier = ParseIdentifier(out var enclosed);
            _tablesIdentifiers.Add(identifier);
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

            if (!_sqlSourceCode.HasCurrent())
            {
                return;
            }

            if (CanBeIdentifier())
            {
                ParseTableMemberExpressionOrTableMemberExpressionAsAlias();
            }
        }

        private bool CanBeIdentifier()
        {
            var current = _sqlSourceCode.Current;
            var isIdentifier = current switch
            {
                '[' => true,
                '"' => true,
                _ => IsIdentifierFirstCharacter(current),
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

                        return identifier;
                    }
                case '"':
                    {
                        // https://learn.microsoft.com/en-sg/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#double-quote-delimited-identifiers

                        var identifier = ParseDoubleQuoteEnclosedIdentifier();

                        enclosed = true;

                        return identifier;
                    }

                default:
                    {
                        // https://learn.microsoft.com/en-sg/sql/relational-databases/databases/database-identifiers?view=sql-server-ver17#rules-for-regular-identifiers

                        var startIndex = _sqlSourceCode.Index;
                        _sqlSourceCode.Expect(IsIdentifierFirstCharacter);
                        _sqlSourceCode.AdvanceUntil(static c => !IsIdentifierPartCharacter(c));

                        var identifier = _sqlSourceCode.Sql.AsMemory(startIndex, _sqlSourceCode.Index - startIndex);

                        // may be a reserved Keyword
                        enclosed = false;

                        return identifier;
                    }
            }

            ReadOnlyMemory<char> ParseBracketEnclosedIdentifier()
            {
                const char closingCharacter = ']';

                var startIndex = _sqlSourceCode.Index;

                _sqlSourceCode.Expect('[');

                var hasEscaped = false;

                while (_sqlSourceCode.HasCurrent())
                {
                    _sqlSourceCode.AdvanceUntil(closingCharacter);
                    _sqlSourceCode.Advance();

                    if (_sqlSourceCode.AdvanceIf(closingCharacter))
                    {
                        hasEscaped = true;
                    }
                    else
                    {
                        break;
                    }
                }

                var finalStartIndex = startIndex + 1;
                var endIndex = _sqlSourceCode.Index - finalStartIndex - 1;

                if (!hasEscaped)
                {
                    var enclosingIdentifier = _sqlSourceCode.Sql.AsMemory(finalStartIndex, endIndex);
                    return enclosingIdentifier;
                }
                else
                {
                    // [really]]bad]]identifier] is a valid identifier -> really]bad]identifier

                    var enclosingIdentifier = _sqlSourceCode.Sql.Substring(finalStartIndex, endIndex);

                    enclosingIdentifier = enclosingIdentifier.Replace(new string(closingCharacter, 2), new string(closingCharacter, 1));

                    return enclosingIdentifier.AsMemory();
                }
            }

            ReadOnlyMemory<char> ParseDoubleQuoteEnclosedIdentifier()
            {
                const char closingCharacter = '"';

                var startIndex = _sqlSourceCode.Index;

                var doubleQuotesCount = 1;
                _sqlSourceCode.Expect(closingCharacter);

                while (_sqlSourceCode.HasCurrent())
                {
                    _sqlSourceCode.AdvanceUntil(closingCharacter);
                    _sqlSourceCode.Advance();
                    ++doubleQuotesCount;

                    if (_sqlSourceCode.AdvanceIf(closingCharacter))
                    {
                        ++doubleQuotesCount;
                    }
                    else if ((doubleQuotesCount & 1) == 0)
                    {
                        break;
                    }
                }

                var finalStartIndex = startIndex + 1;
                var endIndex = _sqlSourceCode.Index - finalStartIndex - 1;

                if (doubleQuotesCount == 2)
                {
                    var enclosingIdentifier = _sqlSourceCode.Sql.AsMemory(finalStartIndex, endIndex);
                    return enclosingIdentifier;
                }
                else
                {
                    // "really""bad""identifier" is a valid identifier -> really"bad"identifier

                    var enclosingIdentifier = _sqlSourceCode.Sql.Substring(finalStartIndex, endIndex);

                    enclosingIdentifier = enclosingIdentifier.Replace(new string(closingCharacter, 2), new string(closingCharacter, 1));

                    return enclosingIdentifier.AsMemory();
                }
            }
        }

        /// <returns>Last identifier</returns>
        private ReadOnlyMemory<char> ParseIdentifierOrMemberExpression(out bool lastIsEnclosed)
        {
            var lastIdentifier = ParseIdentifier(out lastIsEnclosed);
    
            while (_sqlSourceCode.HasCurrent() && _sqlSourceCode.AdvanceIf('.'))
            {
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

        private void ParseOutputClause(Func<ReadOnlySpan<char>, bool> isEnd)
        {
            while (_sqlSourceCode.HasCurrent())
            {
                var current = _sqlSourceCode.Current;

                switch (current)
                {
                    case '(':
                        ParseParenthesisExpression();
                        break;
                    case '\'':
                        ParseEnclosedExpression('\'');
                        break;

                    default:
                        if (CanBeIdentifier())
                        {
                            var lastIndex = _sqlSourceCode.Index;
                            var lastIdentifier = ParseIdentifierOrMemberExpression(out var cannotBeKeyword);

                            if (!cannotBeKeyword)
                            {
                                var lastIdentifierSpan = lastIdentifier.Span;

                                if (SqlServerReservedKeywords.IsKeyword(lastIdentifier.Span))
                                {
                                    if (lastIdentifierSpan.Equals("INTO", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
                                        _tablesIdentifiers.Add(identifier);
                                        return;
                                    }
                                    else if (isEnd(lastIdentifierSpan))
                                    {
                                        _sqlSourceCode.Index = lastIndex;
                                        return;
                                    }
                                }
                            }
                        }
                        else if (char.IsDigit(current))
                        {
                            _sqlSourceCode.AdvanceUntil(static c => !char.IsDigit(c));
                        }
                        else
                        {
                            _sqlSourceCode.Advance();
                        }

                        break;
                }

                if (!_sqlSourceCode.HasCurrent())
                {
                    return;
                }

                AdvanceSeparators();
            }
        }

        private void TryParseTopExpressionPercent()
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
            }
        }

        private void ParseParenthesisExpression()
            => ParseEnclosedExpression('(', ')');

        private void ParseEnclosedExpression(char open, char close)
        {
            _sqlSourceCode.Expect(open);

            var openingCount = 1;
            while (_sqlSourceCode.HasCurrent() && openingCount > 0)
            {
                if (_sqlSourceCode.AdvanceIf(open))
                {
                    ++openingCount;
                }
                else if (_sqlSourceCode.AdvanceIf(close))
                {
                    --openingCount;
                }
                else
                {
                    _sqlSourceCode.Advance();
                }
            }
        }

        private void ParseEnclosedExpression(char character)
        {
            _sqlSourceCode.Expect(character);

            var characterCount = 1;

            while (_sqlSourceCode.HasCurrent())
            {
                _sqlSourceCode.AdvanceUntil(character);
                _sqlSourceCode.Advance();
                ++characterCount;

                if (!_sqlSourceCode.HasCurrent())
                {
                    return;
                }

                if (_sqlSourceCode.AdvanceIf(character))
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
            var identifier = ParseIdentifierOrMemberExpression(out var enclosed);
            AdvanceSeparators();

            if (enclosed || !SqlServerReservedKeywords.IsKeyword(identifier.Span))
            {
                _tablesIdentifiers.Add(identifier);
            }
            else
            {
                return;
            }

            _sqlSourceCode.TryAdvanceText("AS", StringComparison.OrdinalIgnoreCase);

            if (!_sqlSourceCode.HasCurrent())
            {
                return;
            }

            AdvanceSeparators();

            if (CanBeIdentifier())
            {
                var alias = ParseIdentifier(out var _);
                AdvanceSeparators();

                AddTableAlias(ref alias, ref identifier);
            }
        }

        private void AddTableAlias(ref ReadOnlyMemory<char> alias, ref ReadOnlyMemory<char> identifier)
        {
            ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_tablesAliases, alias, out var exists);
            if (!exists)
            {
                list = new List<ReadOnlyMemory<char>>(2);
            }
            list!.Add(identifier);

            _tablesIdentifiers.Add(alias);
        }

        private sealed class ReadOnlyMemoryCharComparer : IEqualityComparer<ReadOnlyMemory<char>>
        {
            public static readonly ReadOnlyMemoryCharComparer OrdinalIgnoreCase =
                new(StringComparison.OrdinalIgnoreCase);

            private readonly StringComparison _comparison;

            public ReadOnlyMemoryCharComparer(StringComparison comparison)
            {
                _comparison = comparison;
            }

            public bool Equals(
                ReadOnlyMemory<char> x,
                ReadOnlyMemory<char> y)
            {
                return x.Span.Equals(y.Span, _comparison);
            }

            public int GetHashCode(ReadOnlyMemory<char> obj)
            {
                return string.GetHashCode(obj.Span, _comparison);
            }
        }

        private static class SqlServerReservedKeywords
        {
            public static bool IsKeyword(scoped in ReadOnlySpan<char> identifier)
                => KeywordsSpanLookup.Contains(identifier);

            public static readonly FrozenSet<string> Keywords = new[] {
            "ABSOLUTE", "EXEC", "OVERLAPS", "ACTION", "EXECUTE", "PAD", "ADA", "EXISTS", "PARTIAL", "ADD",
            "EXTERNAL", "PASCAL", "ALL", "EXTRACT", "POSITION", "ALLOCATE", "FALSE", "PRECISION", "ALTER",
            "FETCH", "PREPARE", "AND", "FIRST", "PRESERVE", "ANY", "FLOAT", "PRIMARY", "ARE", "FOR", "PRIOR",
            "AS", "FOREIGN", "PRIVILEGES", "ASC", "FORTRAN", "PROCEDURE", "ASSERTION", "FOUND", "PUBLIC", "AT",
            "FROM", "READ", "AUTHORIZATION", "FULL", "REAL", "AVG", "GET", "REFERENCES", "BEGIN", "GLOBAL",
            "RELATIVE", "BETWEEN", "GO", "RESTRICT", "BIT", "GOTO", "REVOKE", "BIT_LENGTH", "GRANT", "RIGHT",
            "BOTH", "GROUP", "ROLLBACK", "BY", "HAVING", "ROWS", "CASCADE", "HOUR", "SCHEMA", "CASCADED", "IDENTITY",
            "SCROLL", "CASE", "IMMEDIATE", "SECOND", "CAST", "IN", "SECTION", "CATALOG", "INCLUDE", "SELECT",
            "CHAR", "INDEX", "SESSION", "CHAR_LENGTH", "INDICATOR", "SESSION_USER", "CHARACTER", "INITIALLY",
            "SET", "CHARACTER_LENGTH", "INNER", "SIZE", "CHECK", "INPUT", "SMALLINT", "CLOSE", "INSENSITIVE",
            "SOME", "COALESCE", "INSERT", "SPACE", "COLLATE", "INT", "SQL", "COLLATION", "INTEGER", "SQLCA",
            "COLUMN", "INTERSECT", "SQLCODE", "COMMIT", "INTERVAL", "SQLERROR", "CONNECT", "INTO", "SQLSTATE",
            "CONNECTION", "IS", "SQLWARNING", "CONSTRAINT", "ISOLATION", "SUBSTRING", "CONSTRAINTS", "JOIN",
            "SUM", "CONTINUE", "KEY", "SYSTEM_USER", "CONVERT", "LANGUAGE", "TABLE", "CORRESPONDING", "LAST",
            "TEMPORARY", "COUNT", "LEADING", "THEN", "CREATE", "LEFT", "TIME", "CROSS", "LEVEL", "TIMESTAMP",
            "CURRENT", "LIKE", "TIMEZONE_HOUR", "CURRENT_DATE", "LOCAL", "TIMEZONE_MINUTE", "CURRENT_TIME",
            "LOWER", "TO", "CURRENT_TIMESTAMP", "MATCH", "TRAILING", "CURRENT_USER", "MAX", "TRANSACTION", "CURSOR",
            "MIN", "TRANSLATE", "DATE", "MINUTE", "TRANSLATION", "DAY", "MODULE", "TRIM", "DEALLOCATE", "MONTH",
            "TRUE", "DEC", "NAMES", "UNION", "DECIMAL", "NATIONAL", "UNIQUE", "DECLARE", "NATURAL", "UNKNOWN",
            "DEFAULT", "NCHAR", "UPDATE", "DEFERRABLE", "NEXT", "UPPER", "DEFERRED", "NO", "USAGE", "DELETE",
            "NONE", "USER", "DESC", "NOT", "USING", "DESCRIBE", "NULL", "VALUE", "DESCRIPTOR", "NULLIF", "VALUES",
            "DIAGNOSTICS", "NUMERIC", "VARCHAR", "DISCONNECT", "OCTET_LENGTH", "VARYING", "DISTINCT", "OF", "VIEW",
            "DOMAIN", "ON", "WHEN", "DOUBLE", "ONLY", "WHENEVER", "DROP", "OPEN", "WHERE", "ELSE", "OPTION", "WITH",
            "END", "OR", "WORK", "END-EXEC", "ORDER", "WRITE", "ESCAPE", "OUTER", "YEAR", "EXCEPT", "OUTPUT", "ZONE",
            "EXCEPTION", "ADD", "EXTERNAL", "PROCEDURE", "ALL", "FETCH", "PUBLIC", "ALTER", "FILE", "RAISERROR",
            "AND", "FILLFACTOR", "READ", "ANY", "FOR", "READTEXT", "AS", "FOREIGN", "RECONFIGURE", "ASC", "FREETEXT",
            "REFERENCES", "AUTHORIZATION", "FREETEXTTABLE", "REPLICATION", "BACKUP", "FROM", "RESTORE", "BEGIN",
            "FULL", "RESTRICT", "BETWEEN", "FUNCTION", "RETURN", "BREAK", "GOTO", "REVERT", "BROWSE", "GRANT",
            "REVOKE", "BULK", "GROUP", "RIGHT", "BY", "HAVING", "ROLLBACK", "CASCADE", "HOLDLOCK", "ROWCOUNT",
            "CASE", "IDENTITY", "ROWGUIDCOL", "CHECK", "IDENTITY_INSERT", "RULE", "CHECKPOINT", "IDENTITYCOL",
            "SAVE", "CLOSE", "IF", "SCHEMA", "CLUSTERED", "IN", "SECURITYAUDIT", "COALESCE", "INDEX", "SELECT",
            "COLLATE", "INNER", "SEMANTICKEYPHRASETABLE", "COLUMN", "INSERT", "SEMANTICSIMILARITYDETAILSTABLE",
            "COMMIT", "INTERSECT", "SEMANTICSIMILARITYTABLE", "COMPUTE", "INTO", "SESSION_USER", "CONSTRAINT",
            "IS", "SET", "CONTAINS", "JOIN", "SETUSER", "CONTAINSTABLE", "KEY", "SHUTDOWN", "CONTINUE", "KILL",
            "SOME", "CONVERT", "LEFT", "STATISTICS", "CREATE", "LIKE", "SYSTEM_USER", "CROSS", "LINENO", "TABLE",
            "CURRENT", "LOAD", "TABLESAMPLE", "CURRENT_DATE", "MERGE", "TEXTSIZE", "CURRENT_TIME", "NATIONAL",
            "THEN", "CURRENT_TIMESTAMP", "NOCHECK", "TO", "CURRENT_USER", "NONCLUSTERED", "TOP", "CURSOR", "NOT",
            "TRAN", "DATABASE", "NULL", "TRANSACTION", "DBCC", "NULLIF", "TRIGGER", "DEALLOCATE", "OF", "TRUNCATE",
            "DECLARE", "OFF", "TRY_CONVERT", "DEFAULT", "OFFSETS", "TSEQUAL", "DELETE", "ON", "UNION", "DENY",
            "OPEN", "UNIQUE", "DESC", "OPENDATASOURCE", "UNPIVOT", "DISK", "OPENQUERY", "UPDATE", "DISTINCT",
            "OPENROWSET", "UPDATETEXT", "DISTRIBUTED", "OPENXML", "USE", "DOUBLE", "OPTION", "USER", "DROP", "OR",
            "VALUES", "DUMP", "ORDER", "VARYING", "ELSE", "OUTER", "VIEW", "END", "OVER", "WAITFOR", "ERRLVL",
            "PERCENT", "WHEN", "ESCAPE", "PIVOT", "WHERE", "EXCEPT", "PLAN", "WHILE", "EXEC", "PRECISION", "WITH",
            "EXECUTE", "PRIMARY", "WITHIN GROUP", "EXISTS", "PRINT", "WRITETEXT", "EXIT", "PROC"}.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            public static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> KeywordsSpanLookup = Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }
}
