using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CleanArchitecture.Infrastructure.Links;

/// <summary>The client application that emailed links (invitations, password resets) point to.</summary>
internal sealed class ClientAppOptions
{
    public const string SectionName = "ClientApp";

    public string BaseUrl { get; init; } = string.Empty;
}

/// <summary>The links carry single-use secrets, so outside Development they must use HTTPS.</summary>
internal sealed class ClientAppOptionsValidator(IHostEnvironment environment) : IValidateOptions<ClientAppOptions>
{
    public ValidateOptionsResult Validate(string? name, ClientAppOptions options)
    {
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? baseUrl)
            || baseUrl.Scheme is not ("https" or "http"))
        {
            return ValidateOptionsResult.Fail("ClientApp:BaseUrl must be an absolute http(s) URL.");
        }

        if (baseUrl.Scheme != Uri.UriSchemeHttps && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail("ClientApp:BaseUrl must use HTTPS outside Development.");
        }

        return ValidateOptionsResult.Success;
    }
}
