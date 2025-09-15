using System.Linq.Expressions;

namespace CachedEfCore.KeyGeneration.EvalTypeChecker
{
    public interface IExpressionEvalTypeChecker
    {
        public bool WillEvalTypes(Expression? expression);
    }
}
