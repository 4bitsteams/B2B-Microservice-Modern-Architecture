namespace B2B.Web.Models.Common;

public sealed record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record ApiError(string Code, string Message);
