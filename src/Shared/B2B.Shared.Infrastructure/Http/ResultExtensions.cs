using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Http;

/// <summary>
/// Extension methods that convert <see cref="Result"/> and <see cref="Result{TValue}"/>
/// to the appropriate <see cref="IActionResult"/> using the same error-to-HTTP mapping
/// as <see cref="ApiControllerBase"/>.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result{TValue}"/> to an <see cref="IActionResult"/>.
    /// Returns <c>200 OK</c> with the value on success, or the appropriate problem
    /// response on failure.
    /// </summary>
    public static IActionResult ToActionResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : ToProblemResult(result.Error);

    /// <summary>
    /// Converts a non-generic <see cref="Result"/> to an <see cref="IActionResult"/>.
    /// Returns <c>204 No Content</c> on success, or the appropriate problem response on failure.
    /// </summary>
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new NoContentResult()
            : ToProblemResult(result.Error);

    private static IActionResult ToProblemResult(Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.NotFound     => (StatusCodes.Status404NotFound,            "Not Found"),
            ErrorType.Validation   => (StatusCodes.Status400BadRequest,          "Validation Error"),
            ErrorType.Conflict     => (StatusCodes.Status409Conflict,            "Conflict"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized,        "Unauthorized"),
            ErrorType.Forbidden    => (StatusCodes.Status403Forbidden,           "Forbidden"),
            _                      => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        return new ObjectResult(new ProblemDetails
        {
            Title = title,
            Detail = error.Description,
            Status = status
        })
        { StatusCode = status };
    }
}
