using System.ComponentModel.DataAnnotations;

namespace CleanArchitecture.Infrastructure.Links;

/// <summary>The client application that emailed links (invitations, password resets) point to.</summary>
internal sealed class ClientAppOptions
{
    public const string SectionName = "ClientApp";

    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;
}
