using System.Net;
using ShortLinks.Application.Security;

namespace ShortLinks.Application.Links;

public sealed class LinkService
{
    private const int MaximumCreationsPerWindow = 60;
    private const int MaximumGeneratedCodeAttempts = 10;
    private static readonly TimeSpan CreationWindow = TimeSpan.FromMinutes(60);

    private readonly ILinkRepository _repository;
    private readonly ShortCodeGenerator _shortCodeGenerator;
    private readonly ManagementKeyService _managementKeyService;
    private readonly AsyncKeyedLock _creationLocks;
    private readonly TimeProvider _timeProvider;
    private readonly string _publicBaseUrl;

    public LinkService(
        ILinkRepository repository,
        ShortCodeGenerator shortCodeGenerator,
        ManagementKeyService managementKeyService,
        AsyncKeyedLock creationLocks,
        TimeProvider timeProvider,
        string publicBaseUrl)
    {
        _repository = repository;
        _shortCodeGenerator = shortCodeGenerator;
        _managementKeyService = managementKeyService;
        _creationLocks = creationLocks;
        _timeProvider = timeProvider;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task<CreateLinkResult> CreateAsync(
        CreateLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            throw validationError;
        }

        using var creationLock = await _creationLocks
            .AcquireAsync(command.CreatorIpFingerprint, cancellationToken)
            .ConfigureAwait(false);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (command.ExpiresAtUtc is not null && command.ExpiresAtUtc <= nowUtc)
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "Data de expiração inválida",
                "A data de expiração deve ser posterior ao momento da criação.");
        }

        var windowStartUtc = nowUtc.Subtract(CreationWindow);
        var creationCount = await _repository.CountCreationsAsync(
            command.CreatorIpFingerprint,
            windowStartUtc,
            nowUtc,
            cancellationToken).ConfigureAwait(false);

        if (creationCount >= MaximumCreationsPerWindow)
        {
            var oldestCreationUtc = await _repository.GetOldestCreationAsync(
                command.CreatorIpFingerprint,
                windowStartUtc,
                nowUtc,
                cancellationToken).ConfigureAwait(false);

            var retryAfterSeconds = oldestCreationUtc is null
                ? (int)CreationWindow.TotalSeconds
                : Math.Max(1, (int)Math.Ceiling(
                    (oldestCreationUtc.Value.Add(CreationWindow).Subtract(nowUtc).TotalSeconds)) + 1);

            throw new LinkServiceException(
                (int)HttpStatusCode.TooManyRequests,
                "Limite de criações excedido",
                "O limite de 60 links por endereço IP a cada 60 minutos foi atingido.",
                retryAfterSeconds);
        }

        var customCode = command.CustomCode;

        for (var attempt = 0; attempt < MaximumGeneratedCodeAttempts; attempt++)
        {
            var code = customCode ?? _shortCodeGenerator.Generate();

            if (await _repository.CodeExistsAsync(code, cancellationToken).ConfigureAwait(false))
            {
                if (customCode is not null)
                {
                    throw CodeConflict();
                }

                continue;
            }

            var generatedKey = _managementKeyService.Generate();
            var newLink = new NewLinkRecord(
                code,
                command.DestinationUrl,
                nowUtc,
                command.ExpiresAtUtc,
                generatedKey.Hash,
                command.CreatorIpFingerprint);

            if (!await _repository.TryCreateAsync(newLink, cancellationToken).ConfigureAwait(false))
            {
                if (customCode is not null)
                {
                    throw CodeConflict();
                }

                continue;
            }

            return new CreateLinkResult(
                code,
                $"{_publicBaseUrl}/{code}",
                generatedKey.PlainText,
                nowUtc,
                command.ExpiresAtUtc);
        }

        throw new LinkServiceException(
            (int)HttpStatusCode.ServiceUnavailable,
            "Não foi possível criar o link",
            "Não foi possível reservar um código exclusivo. Tente novamente.");
    }

    public async Task<RedirectResult> RedirectAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var link = await _repository.FindByCodeAsync(code, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.NotFound,
                "Link não encontrado",
                "O código informado não corresponde a um link existente.");
        }

        var accessedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (link.ExpiresAtUtc is not null && link.ExpiresAtUtc <= accessedAtUtc)
        {
            throw ExpiredLink();
        }

        var incremented = await _repository.TryIncrementAccessCountAsync(
            code,
            accessedAtUtc,
            cancellationToken).ConfigureAwait(false);

        if (!incremented)
        {
            var latestLink = await _repository.FindByCodeAsync(code, cancellationToken)
                .ConfigureAwait(false);

            if (latestLink is null)
            {
                throw new LinkServiceException(
                    (int)HttpStatusCode.NotFound,
                    "Link não encontrado",
                    "O código informado não corresponde a um link existente.");
            }

            if (latestLink.ExpiresAtUtc is not null
                && latestLink.ExpiresAtUtc <= accessedAtUtc)
            {
                throw ExpiredLink();
            }

            throw new LinkServiceException(
                (int)HttpStatusCode.ServiceUnavailable,
                "Não foi possível registrar o acesso",
                "O acesso não foi registrado. Tente novamente.");
        }

        return new RedirectResult(link.DestinationUrl);
    }

    public async Task<LinkStatsResult> GetStatsAsync(
        string code,
        string? managementKey,
        CancellationToken cancellationToken = default)
    {
        var link = await _repository.FindByCodeAsync(code, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.NotFound,
                "Link não encontrado",
                "O código informado não corresponde a um link existente.");
        }

        if (!_managementKeyService.Verify(managementKey ?? string.Empty, link.ManagementKeyHash))
        {
            throw new LinkServiceException(
                (int)HttpStatusCode.Unauthorized,
                "Chave de gerenciamento inválida",
                "Informe a chave de gerenciamento válida para consultar os acessos.");
        }

        return new LinkStatsResult(link.Code, link.AccessCount);
    }

    private static LinkServiceException? Validate(CreateLinkCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.DestinationUrl)
            || command.DestinationUrl.Length > 2048
            || !Uri.TryCreate(command.DestinationUrl, UriKind.Absolute, out var destinationUri)
            || string.IsNullOrWhiteSpace(destinationUri.Host)
            || (destinationUri.Scheme != Uri.UriSchemeHttp
                && destinationUri.Scheme != Uri.UriSchemeHttps))
        {
            return new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "URL de destino inválida",
                "Informe uma URL absoluta HTTP ou HTTPS com host e no máximo 2048 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(command.CreatorIpFingerprint))
        {
            return new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "Origem inválida",
                "Não foi possível identificar o endereço IP de origem da solicitação.");
        }

        if (command.CustomCode is not null
            && !ShortCodeGenerator.IsValidCustomCode(command.CustomCode))
        {
            return new LinkServiceException(
                (int)HttpStatusCode.BadRequest,
                "Código personalizado inválido",
                "O código deve ter de 4 a 20 caracteres do alfabeto permitido.");
        }

        return null;
    }

    private static LinkServiceException CodeConflict()
    {
        return new LinkServiceException(
            (int)HttpStatusCode.Conflict,
            "Código já utilizado",
            "O código solicitado já está em uso.");
    }

    private static LinkServiceException ExpiredLink()
    {
        return new LinkServiceException(
            (int)HttpStatusCode.Gone,
            "Link expirado",
            "Este link expirou e não pode mais redirecionar.");
    }
}