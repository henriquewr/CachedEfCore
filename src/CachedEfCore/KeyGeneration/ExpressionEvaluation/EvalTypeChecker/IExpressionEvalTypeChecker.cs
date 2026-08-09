using System.Linq.Expressions;

namespace CachedEfCore.KeyGeneration.ExpressionEvaluation.EvalTypeChecker
{
    public interface IExpressionEvalTypeChecker
    {
        public bool WillEvalTypes(Expression? expression);
    }
}
