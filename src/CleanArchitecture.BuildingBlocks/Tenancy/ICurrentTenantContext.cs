using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.BuildingBlocks.Tenancy;

/// <summary>
/// The tenant and user the current request acts for. Resolved per request — never cache it
/// in static or singleton state.
/// </summary>
public interface ICurrentTenantContext
{
    /// <summary>False for anonymous requests and background work without a tenant.</summary>
    bool IsAvailable { get; }

    /// <exception cref="TenantContextUnavailableException">When <see cref="IsAvailable"/> is false.</exception>
    TenantId CurrentTenantId { get; }

    /// <exception cref="TenantContextUnavailableException">When <see cref="IsAvailable"/> is false.</exception>
    Guid CurrentUserId { get; }

    /// <summary>
    /// The session (refresh-token family) the access token was issued for, so a use case can end
    /// every session but the current one. Null when unavailable.
    /// </summary>
    Guid? CurrentSessionId { get; }

    /// <summary>
    /// Tenants the current request may read. The template grants only the current tenant;
    /// extend the resolver when a tenant hierarchy is introduced.
    /// </summary>
    IReadOnlyCollection<TenantId> AccessibleTenantIds { get; }
}
