using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShortLinks.Api.Errors;
using ShortLinks.Api.Endpoints;
using ShortLinks.Api.Options;
using ShortLinks.Application.Links;
using ShortLinks.Infrastructure.Persistence;
using ApiLinkOptions = ShortLinks.Api.Options.LinkOptions;

var builder = WebApplication.CreateBuilder(args);

var linkOptions = builder.Configuration
	.GetSection(ApiLinkOptions.SectionName)
	.Get<ApiLinkOptions>() ?? new ApiLinkOptions();

if (!Uri.TryCreate(linkOptions.PublicBaseUrl, UriKind.Absolute, out var publicBaseUri)
	|| (publicBaseUri.Scheme != Uri.UriSchemeHttp && publicBaseUri.Scheme != Uri.UriSchemeHttps))
{
	throw new InvalidOperationException(
		"Links:PublicBaseUrl must be an absolute HTTP or HTTPS URL.");
}

linkOptions.PublicBaseUrl = linkOptions.PublicBaseUrl.TrimEnd('/');

var linkSecurityOptions = builder.Configuration
	.GetSection(LinkSecurityOptions.SectionName)
	.Get<LinkSecurityOptions>() ?? new LinkSecurityOptions();

var validatedFingerprintKey = linkSecurityOptions.GetValidatedClientIpFingerprintKey();
var clientIpFingerprintService = new ShortLinks.Application.Security.ClientIpFingerprintService(validatedFingerprintKey);
System.Security.Cryptography.CryptographicOperations.ZeroMemory(validatedFingerprintKey);

var databasePath = Path.IsPathRooted(linkOptions.DatabasePath)
	? linkOptions.DatabasePath
	: Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, linkOptions.DatabasePath));

var databaseDirectory = Path.GetDirectoryName(databasePath);
if (!string.IsNullOrEmpty(databaseDirectory))
{
	Directory.CreateDirectory(databaseDirectory);
}

var connectionString = new SqliteConnectionStringBuilder
{
	DataSource = databasePath
}.ToString();

builder.Services.AddSingleton(linkOptions);
builder.Services.AddSingleton(clientIpFingerprintService);
builder.Services.AddSingleton<ShortLinks.Application.Security.ManagementKeyService>();
builder.Services.AddSingleton<ShortLinks.Application.Links.ShortCodeGenerator>();
builder.Services.AddSingleton<ShortLinks.Application.Links.AsyncKeyedLock>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<LinkDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ILinkRepository, EfLinkRepository>();
builder.Services.AddScoped<LinkService>(services => new LinkService(
	services.GetRequiredService<ILinkRepository>(),
	services.GetRequiredService<ShortLinks.Application.Links.ShortCodeGenerator>(),
	services.GetRequiredService<ShortLinks.Application.Security.ManagementKeyService>(),
	services.GetRequiredService<ShortLinks.Application.Links.AsyncKeyedLock>(),
	services.GetRequiredService<TimeProvider>(),
	services.GetRequiredService<ApiLinkOptions>().PublicBaseUrl));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<LinkDbContext>();
	dbContext.Database.Migrate();
}

app.MapLinksEndpoints();

app.Run();

public partial class Program
{
}
