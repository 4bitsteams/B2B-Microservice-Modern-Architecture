using System.Linq.Expressions;

namespace B2B.Shared.Core.Specifications;

/// <summary>Combines two specifications with a logical AND.</summary>
public sealed class AndSpecification<T>(ISpecification<T> left, ISpecification<T> right) : ISpecification<T>
{
    public Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = left.ToExpression();
        var rightExpr = right.ToExpression();
        var param = Expression.Parameter(typeof(T));
        var body = Expression.AndAlso(
            Expression.Invoke(leftExpr, param),
            Expression.Invoke(rightExpr, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    public IReadOnlyList<Expression<Func<T, object?>>>? Includes => null;
    public (Expression<Func<T, object?>> KeySelector, bool Descending)? Order => null;
    public bool IsSatisfiedBy(T entity) => left.IsSatisfiedBy(entity) && right.IsSatisfiedBy(entity);
}

/// <summary>Combines two specifications with a logical OR.</summary>
public sealed class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right) : ISpecification<T>
{
    public Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = left.ToExpression();
        var rightExpr = right.ToExpression();
        var param = Expression.Parameter(typeof(T));
        var body = Expression.OrElse(
            Expression.Invoke(leftExpr, param),
            Expression.Invoke(rightExpr, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    public IReadOnlyList<Expression<Func<T, object?>>>? Includes => null;
    public (Expression<Func<T, object?>> KeySelector, bool Descending)? Order => null;
    public bool IsSatisfiedBy(T entity) => left.IsSatisfiedBy(entity) || right.IsSatisfiedBy(entity);
}

/// <summary>Negates a specification.</summary>
public sealed class NotSpecification<T>(ISpecification<T> inner) : ISpecification<T>
{
    public Expression<Func<T, bool>> ToExpression()
    {
        var innerExpr = inner.ToExpression();
        var param = Expression.Parameter(typeof(T));
        var body = Expression.Not(Expression.Invoke(innerExpr, param));
        return Expression.Lambda<Func<T, bool>>(body, param);
    }

    public IReadOnlyList<Expression<Func<T, object?>>>? Includes => null;
    public (Expression<Func<T, object?>> KeySelector, bool Descending)? Order => null;
    public bool IsSatisfiedBy(T entity) => !inner.IsSatisfiedBy(entity);
}
