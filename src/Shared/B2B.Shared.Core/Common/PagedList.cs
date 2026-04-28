namespace B2B.Shared.Core.Common;

/// <summary>
/// Immutable container for a single page of query results, including the
/// metadata needed for cursor-less offset pagination (page number, page size,
/// total count, and derived navigation flags).
///
/// Use <see cref="Create(IEnumerable{T}, int, int)"/> when the full in-memory
/// collection is already available (unit tests, in-memory repositories).
/// Use <see cref="Create(IReadOnlyList{T}, int, int, int)"/> when executing
/// an EF Core query that already applied <c>Skip</c>/<c>Take</c> at the
/// database level and you have a separate <c>COUNT(*)</c> total.
/// </summary>
/// <typeparam name="T">The element type of the page.</typeparam>
public sealed class PagedList<T>
{
    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>1-based current page index.</summary>
    public int Page { get; }

    /// <summary>Maximum number of items per page.</summary>
    public int PageSize { get; }

    /// <summary>Total number of items across all pages.</summary>
    public int TotalCount { get; }

    /// <summary>Total number of pages, rounded up.</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary><see langword="true"/> when a previous page exists.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary><see langword="true"/> when a next page exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    private PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Creates a paged list by slicing an in-memory <paramref name="source"/> collection.
    /// The total count is derived from the full source before slicing.
    /// </summary>
    /// <param name="source">The full unsliced sequence.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum items per page.</param>
    public static PagedList<T> Create(IEnumerable<T> source, int page, int pageSize)
    {
        var list = source.ToList();
        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedList<T>(items, page, pageSize, total);
    }

    /// <summary>
    /// Creates a paged list from an already-sliced <paramref name="items"/> collection
    /// and an externally computed <paramref name="totalCount"/>.
    /// Use this overload with EF Core queries that apply <c>Skip</c>/<c>Take</c>
    /// at the database level.
    /// </summary>
    /// <param name="items">The pre-sliced page of items.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum items per page.</param>
    /// <param name="totalCount">Total matching items across all pages.</param>
    public static PagedList<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount) =>
        new(items, page, pageSize, totalCount);

    /// <summary>
    /// Projects each item on the current page to a new type while preserving
    /// all pagination metadata.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="mapper">Projection function applied to each item.</param>
    public PagedList<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new(Items.Select(mapper).ToList(), Page, PageSize, TotalCount);
}

/// <summary>
/// Convenience base record for query objects that request a specific page of results.
/// Extend or include this record in query commands rather than repeating pagination
/// parameters on every query.
/// </summary>
/// <param name="Page">1-based page number. Defaults to <c>1</c>.</param>
/// <param name="PageSize">Maximum items per page. Defaults to <c>20</c>.</param>
/// <param name="SortBy">Optional field name to sort by.</param>
/// <param name="Descending">Sort direction. <see langword="true"/> for descending.</param>
public sealed record PagedQuery(int Page = 1, int PageSize = 20, string? SortBy = null, bool Descending = false)
{
    /// <summary>Number of items to skip before the current page (0-based offset).</summary>
    public int Skip => (Page - 1) * PageSize;
}
