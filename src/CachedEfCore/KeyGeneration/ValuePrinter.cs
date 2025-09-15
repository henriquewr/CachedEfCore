using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CachedEfCore.KeyGeneration
{
    public class ValuePrinter : IDisposable, IAsyncDisposable
    {
        private readonly MemoryStream _memoryStream;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        public bool IsDisposed { get; private set; }

        public ValuePrinter(JsonSerializerOptions jsonSerializerOptions, MemoryStream memoryStream)
        {
            _memoryStream = memoryStream;
            _jsonSerializerOptions = jsonSerializerOptions;
        }

        public ValuePrinter(JsonSerializerOptions jsonSerializerOptions) : this(jsonSerializerOptions, new MemoryStream())
        {

        }

        public ValuePrinter() : this(new JsonSerializerOptions { IncludeFields = true }, new MemoryStream())
        {

        }

        public void ResetState()
        {
            _memoryStream.Position = 0;
            _memoryStream.SetLength(0);
        }

        public void Print(object? value)
        {
            JsonSerializer.Serialize(_memoryStream!, value, _jsonSerializerOptions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? GetResult()
        {
            var l_memoryStream = _memoryStream!;
            l_memoryStream.Flush();
            var result = l_memoryStream.Length == 0 ? null : Encoding.UTF8.GetString(l_memoryStream.GetBuffer(), 0, (int)l_memoryStream.Length);

            return result;
        }

        public void Dispose()
        {
            IsDisposed = true;
            _memoryStream.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return _memoryStream.DisposeAsync();
        }
    }
}