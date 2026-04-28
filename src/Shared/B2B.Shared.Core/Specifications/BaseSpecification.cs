using System.Linq.Expressions;

namespace B2B.Shared.Core.Specifications;

/// <summary>
/// Base class for concrete specifications. Subclasses supply the predicate
/// via the constructor; includes and ordering are configured via the fluent API.
/// </summary>
public abstract class BaseSpecification<T>(Expression<Func<T, bool>> expression) : ISpecification<T>
{
    private readonly List<Expression<Func<T, object?>>> _includes = [];

    public Expression<Func<T, bool>> ToExpression() => expression;

    public IReadOnlyList<Expression<Func<T, object?>>>? Includes =>
        _includes.Count > 0 ? _includes : null;

    public (Expression<Func<T, object?>> KeySelector, bool Descending)? Order { get; private set; }

    public bool IsSatisfiedBy(T entity) => expression.Compile()(entity);

    // ── Fluent builder methods ─────────────────────────────────────────────────

    protected BaseSpecification<T> AddInclude(Expression<Func<T, object?>> include)
    {
        _includes.Add(include);
        return this;
    }

    protected BaseSpecification<T> ApplyOrderBy(Expression<Func<T, object?>> keySelector, bool descending = false)
    {
        Order = (keySelector, descending);
        return this;
    }

    // ── Composition ────────────────────────────────────────────────────────────

    public ISpecification<T> And(ISpecification<T> other) => new AndSpecification<T>(this, other);
    public ISpecification<T> Or(ISpecification<T> other) => new OrSpecification<T>(this, other);
    public ISpecification<T> Not() => new NotSpecification<T>(this);
}
