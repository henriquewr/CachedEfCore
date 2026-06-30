using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CachedEfCore.SqlAnalysis
{
    [DebuggerDisplay("{Index} -> {Remaining}")]
    public class SqlSourceCode : IEnumerable<char>, IEnumerator<char>
    {
        public string Sql { get; }

        public SqlSourceCode(string sql)
        {
            Sql = sql;
        }

        public ReadOnlySpan<char> Remaining => Sql.AsSpan(Index);

        public int Index { get; set; }

        public bool HasNext() => Index + 1 < Sql.Length;
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

        public ReadOnlySpan<char> PeekText(int length)
        {
            return Sql.AsSpan(Index, length);
        }

        public bool TryPeekLast(out char lastCharacter)
        {
            if (Index > 0)
            {
                lastCharacter = Sql[Index - 1];
                return true;
            }

            lastCharacter = default;
            return false;
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
