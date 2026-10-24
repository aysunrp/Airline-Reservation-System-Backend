using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.Exceptions;

namespace AirlineReservationSystem.Middlewares;

/// <summary>
/// Turns exceptions from the service layer into RFC 7807 problem responses:
/// 400 validation, 401 unauthorized, 404 not found, 409 conflict, 422 business rule, 429 too many sign-in attempts, 500 anything else.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },
            UnauthorizedException => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            TooManyAttemptsException => Problem(StatusCodes.Status429TooManyRequests, "Too many attempts", exception.Message),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            BusinessRuleException => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violation", exception.Message),
            // Internal details are only shown while developing.
            _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.",
                _environment.IsDevelopment() ? exception.Message : null)
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (exception is TooManyAttemptsException tooMany)
        {
            httpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(tooMany.RetryAfter.TotalSeconds)).ToString();
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static ProblemDetails Problem(int status, string title, string? detail)
        => new() { Status = status, Title = title, Detail = detail };
}
