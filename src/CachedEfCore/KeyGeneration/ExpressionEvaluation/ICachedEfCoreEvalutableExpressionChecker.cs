using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace CachedEfCore.KeyGeneration.ExpressionEvaluation
{
    public interface ICachedEfCoreEvalutableExpressionChecker
    {
        bool IsEvaluatableExpression(Expression expression, IModel? model);

        bool IsEvaluatableConstant(ConstantExpression expression);
        bool IsEvaluatableMethodCall(MethodCallExpression expression, IModel? model);
        bool IsEvaluatableMemberExpression(MemberExpression expression, IModel? model);
    }
}
