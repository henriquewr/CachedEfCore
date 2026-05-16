using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace CachedEfCore.SqlAnalysis
{
    [DebuggerDisplay("{Index} -> {Remaining}")]
    public class SqlSourceCode : IEnumerable<char>, IEnumerator<char>
    {
        public readonly string Sql;

        private int _index = 0;
        public SqlSourceCode(string sql)
        {
            Sql = sql;
        }

        private ReadOnlySpan<char> Remaining => Sql.AsSpan(_index);

        public int Index { get => _index; set => _index = value; }

        public bool HasNext() => _index + 1 < Sql.Length;
        public bool HasCurrent() => _index < Sql.Length;

        public bool MoveBack()
        {
            return --_index >= 0;
        }

        public char PeekNext()
        {
            return Sql[_index + 1];
        }

        public bool TryAdvanceText(scoped in ReadOnlySpan<char> text, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (Remaining.StartsWith(text, stringComparison))
            {
                _index += text.Length;
                return true;
            }

            return false;
        }

        public void Expect(scoped in ReadOnlySpan<char> text, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (!TryAdvanceText(text, stringComparison))
            {
                throw new InvalidOperationException($"Expected '{text}' at position {_index}.");
            }
        }

        public void Expect(char character)
        {
            if (!AdvanceIf(character))
            {
                throw new InvalidOperationException($"Expected '{character}' at position {_index}.");
            }
        }

        public void Expect(Func<char, bool> predicate)
        {
            if (!AdvanceIf(predicate))
            {
                throw new InvalidOperationException($"Expected character matching predicate at position {_index}.");
            }
        }

        public char Advance()
            => Sql[_index++];

        public bool AdvanceIf(char character)
        {
            var equals = Current == character;

            if (equals)
            {
                ++_index;
            }

            return equals;
        }
        public bool AdvanceIf(Func<char, bool> predicate)
        {
            var equals = predicate(Current);

            if (equals)
            {
                ++_index;
            }

            return equals;
        }

        public ReadOnlySpan<char> PeekText(int length)
        {
            return Sql.AsSpan(_index, length);
        }

        public ReadOnlySpan<char> AdvanceUntil(char character)
        {
            int startIndex = _index;

            int indexOfChar = Remaining.IndexOf(character);

            if (indexOfChar >= 0)
            {
                _index += indexOfChar;
            }
            else
            {
                _index = Sql.Length;
            }

            var result = Sql.AsSpan(startIndex, _index - startIndex);

            return result;
        }

        public ReadOnlySpan<char> AdvanceUntil(Func<char, bool> predicate)
        {
            int startIndex = _index;

            while (HasCurrent() && !predicate(Sql[_index]))
            {
                _index++;
            }

            return Sql.AsSpan(startIndex, _index - startIndex);
        }

        public ReadOnlySpan<char> Slice(int start, int length)
            => Remaining.Slice(start, length);

        public char Current => Sql[_index];
        object IEnumerator.Current => Current;

        public IEnumerator<char> GetEnumerator()
            => this;

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public bool MoveNext()
        {
            return ++_index < Sql.Length;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
