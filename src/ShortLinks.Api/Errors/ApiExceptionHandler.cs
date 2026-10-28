using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShortLinks.Application.Links;

namespace ShortLinks.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ApiExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, retryAfterSeconds) = exception switch
        {
            LinkServiceException serviceException =>
                (serviceException.StatusCode, serviceException.Title, serviceException.Detail,
                    serviceException.RetryAfterSeconds),
            BadHttpRequestException =>
                (400, "Requisição inválida", "O corpo ou os parâmetros da requisição são inválidos.", null),
            _ => HandleUnexpected(exception)
        };

        if (retryAfterSeconds is not null)
        {
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        httpContext.Response.StatusCode = statusCode;
        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{statusCode}",
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });

        return true;
    }

    private (int StatusCode, string Title, string Detail, int? RetryAfterSeconds)
        HandleUnexpected(Exception exception)
    {
        _logger.LogError(exception, "Unhandled API error.");
        return (500, "Erro interno", "Ocorreu um erro interno. Tente novamente mais tarde.", null);
    }
}