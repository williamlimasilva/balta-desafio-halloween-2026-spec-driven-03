using System.Net;
using ShortLinks.Api.Contracts;
using ShortLinks.Application.Links;
using ShortLinks.Application.Security;

namespace ShortLinks.Api.Endpoints;

public static class LinksEndpoints
{
    public static IEndpointRouteBuilder MapLinksEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/links", CreateLinkAsync);
        endpoints.MapGet("/{code}", RedirectAsync);
        endpoints.MapGet("/api/links/{code}/stats", GetStatsAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateLinkAsync(
        CreateLinkRequest request,
        HttpContext httpContext,
        LinkService linkService,
        ClientIpFingerprintService fingerprintService,
        CancellationToken cancellationToken)
    {
        var remoteAddress = httpContext.Connection.RemoteIpAddress;
        if (remoteAddress is null)
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "Origem inválida",
                "Não foi possível identificar o endereço IP de origem da solicitação.");
        }

        var result = await linkService.CreateAsync(
            new CreateLinkCommand(
                request.DestinationUrl ?? string.Empty,
                request.CustomCode,
                request.ExpiresAtUtc?.UtcDateTime,
                fingerprintService.Fingerprint(remoteAddress)),
            cancellationToken);

        return Results.Created(
            result.ShortUrl,
            new CreateLinkResponse(
                result.Code,
                result.ShortUrl,
                result.ManagementKey,
                result.CreatedAtUtc,
                result.ExpiresAtUtc));
    }

    private static async Task<IResult> RedirectAsync(
        string code,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        ValidateCode(code);
        var result = await linkService.RedirectAsync(code, cancellationToken);
        return Results.Redirect(result.DestinationUrl);
    }

    private static async Task<IResult> GetStatsAsync(
        string code,
        HttpRequest request,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        ValidateCode(code);
        var managementKey = request.Headers["X-Management-Key"].FirstOrDefault();
        var result = await linkService.GetStatsAsync(code, managementKey, cancellationToken);
        return Results.Ok(result);
    }

    private static void ValidateCode(string code)
    {
        if (!ShortCodeGenerator.IsValidCustomCode(code))
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "Código inválido",
                "O código deve ter de 4 a 20 caracteres do alfabeto permitido.");
        }
    }
}