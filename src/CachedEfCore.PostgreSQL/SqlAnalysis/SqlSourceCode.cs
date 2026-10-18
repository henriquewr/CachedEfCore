using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CachedEfCore.PostgreSQL.SqlAnalysis
{
    [DebuggerDisplay("{Index} -> {Remaining}")]
    internal sealed class SqlSourceCode : IEnumerable<char>, IEnumerator<char>
    {
        public string Sql { get; }

        public SqlSourceCode(string sql)
        {
            Sql = sql;
        }

        public ReadOnlySpan<char> Remaining => Sql.AsSpan(Index);

        public int Index { get; set; }

        public bool TryGetNext(out char next)
        {
            var index = Index + 1;

            if (index < Sql.Length)
            {
                next = Sql[index];
                return true;
            }

            next = default;
            return false;
        }
        public bool HasCurrent() => Index < Sql.Length;

        public bool MoveBack()
        {
            return --Index >= 0;
        }

        public char PeekNext()
        {
            return Sql[Index + 1];
        }

        public bool TryAdvanceText(scoped in ReadOnlySpan<char> text, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (Remaining.StartsWith(text, stringComparison))
            {
                Index += text.Length;
                return true;
            }

            return false;
        }

        public void Expect(scoped in ReadOnlySpan<char> text, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (!TryAdvanceText(text, stringComparison))
            {
                throw new InvalidOperationException($"Expected '{text}' at position {Index}.");
            }
        }

        public void Expect(char character)
        {
            if (!AdvanceIf(character))
            {
                throw new InvalidOperationException($"Expected '{character}' at position {Index}.");
            }
        }

        public void Expect(Func<char, bool> predicate)
        {
            if (!AdvanceIf(predicate))
            {
                throw new InvalidOperationException($"Expected character matching predicate at position {Index}.");
            }
        }

        public char Advance()
            => Sql[Index++];

        public bool AdvanceIf(char character)
        {
            var equals = Current == character;

            if (equals)
            {
                ++Index;
            }

            return equals;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AdvanceIf(Func<char, bool> predicate)
        {
            var equals = predicate(Current);

            if (equals)
            {
                ++Index;
            }

            return equals;
        }

        public ReadOnlySpan<char> AdvanceUntil(char character)
        {
            int startIndex = Index;

            int indexOfChar = Remaining.IndexOf(character);

            if (indexOfChar >= 0)
            {
                Index += indexOfChar;
            }
            else
            {
                Index = Sql.Length;
            }

            var result = Sql.AsSpan(startIndex, Index - startIndex);

            return result;
        }

        public void AdvanceUntilPast(scoped in ReadOnlySpan<char> text)
        {
            int indexOfChar = Remaining.IndexOf(text);

            if (indexOfChar >= 0)
            {
                Index += indexOfChar + text.Length;
            }
            else
            {
                Index = Sql.Length;
            }
        }

        public void AdvanceUntilPast(char character)
        {
            int indexOfChar = Remaining.IndexOf(character);

            if (indexOfChar >= 0)
            {
                Index += indexOfChar + 1;
            }
            else
            {
                Index = Sql.Length;
            }
        }

        public ReadOnlySpan<char> AdvanceUntil(Func<char, bool> predicate)
        {
            int startIndex = Index;

            while (HasCurrent() && !predicate(Sql[Index]))
            {
                Index++;
            }

            return Sql.AsSpan(startIndex, Index - startIndex);
        }

        public ReadOnlySpan<char> Slice(int start, int length)
            => Remaining.Slice(start, length);

        public char Current => Sql[Index];
        object IEnumerator.Current => Current;

        public IEnumerator<char> GetEnumerator()
            => this;

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public bool MoveNext()
        {
            return ++Index < Sql.Length;
        }

        public void Reset()
        {
            Index = -1;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
