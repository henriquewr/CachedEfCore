using System.Linq.Expressions;

namespace CachedEfCore.Cache.KeyGeneration.ExpressionEvaluation.EvalTypeChecker
{
    public interface IExpressionEvalTypeChecker
    {
        public bool WillEvalTypes(Expression? expression);
    }
}
