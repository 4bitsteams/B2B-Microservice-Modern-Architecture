using System.Linq.Expressions;

namespace B2B.Shared.Core.Specifications;

/// <summary>
/// Encapsulates a reusable, composable query filter.
///
/// Benefits over ad-hoc LINQ in handlers:
///   • Same filter used across multiple repositories without duplication.
///   • Specifications compose (AND / OR) via <see cref="AndSpecification{T}"/> /
///     <see cref="OrSpecification{T}"/>.
///   • Unit-testable without a database: call <see cref="ToExpression"/> and
///     compile it to a delegate.
///   • Eager-loading and ordering hints are co-located with the filter.
///
/// Usage in a repository:
/// <code>
/// var spec = new ActiveProductsSpec(tenantId)
///     .And(new LowStockSpec())
///     .OrderBy(p => p.CreatedAt, descending: true);
/// var products = await repo.FindAsync(spec, ct);
/// </code>
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();

    /// <summary>Optional eager-loading includes.</summary>
    IReadOnlyList<Expression<Func<T, object?>>>? Includes { get; }

    /// <summary>Optional ordering (null = no ordering).</summary>
    (Expression<Func<T, object?>> KeySelector, bool Descending)? Order { get; }

    bool IsSatisfiedBy(T entity);
}
