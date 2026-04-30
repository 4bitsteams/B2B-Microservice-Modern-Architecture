using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using B2B.Shared.Core.Common;

namespace B2B.Shared.Infrastructure.Http;

/// <summary>
/// Base controller that maps <see cref="Error"/> to the correct HTTP status code.
///
/// Centralises the error → HTTP mapping so individual controllers stay thin and
/// do not duplicate the same switch expression.  All API controllers inherit this
/// class instead of <see cref="ControllerBase"/> directly.
///
/// Usage:
/// <code>
/// return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
/// </code>
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Problem(Error error) => error.Type switch
    {
        ErrorType.NotFound     => NotFound(new ProblemDetails     { Title = "Not Found",             Detail = error.Description }),
        ErrorType.Validation   => BadRequest(new ProblemDetails   { Title = "Validation Error",      Detail = error.Description }),
        ErrorType.Conflict     => Conflict(new ProblemDetails     { Title = "Conflict",              Detail = error.Description }),
        ErrorType.Unauthorized => Unauthorized(new ProblemDetails { Title = "Unauthorized",          Detail = error.Description }),
        ErrorType.Forbidden          => StatusCode(StatusCodes.Status403Forbidden,            new ProblemDetails { Title = "Forbidden",             Detail = error.Description }),
        ErrorType.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,   new ProblemDetails { Title = "Service Unavailable",   Detail = error.Description }),
        _                            => StatusCode(StatusCodes.Status500InternalServerError,   new ProblemDetails { Title = "Internal Server Error", Detail = error.Description })
    };
}
