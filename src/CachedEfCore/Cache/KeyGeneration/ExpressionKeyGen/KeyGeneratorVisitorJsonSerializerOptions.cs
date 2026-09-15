using System.Text.Json;

namespace CachedEfCore.Cache.KeyGeneration.ExpressionKeyGen
{
    internal class KeyGeneratorVisitorJsonSerializerOptions
    {
        public required JsonSerializerOptions Options { get; set; }
    }
}
