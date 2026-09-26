using System.Security.Claims;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Infrastructure.Authentication;
using CleanArchitecture.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace CleanArchitecture.Infrastructure.Tenancy;

/// <summary>Resolves the tenant and user from the authenticated principal of the current request.</summary>
internal sealed class CurrentTenantContext(IHttpContextAccessor httpContextAccessor) : ICurrentTenantContext
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } principal ? principal : null;

    public bool IsAvailable => Principal.TryGetTenantId(out _) && Principal.TryGetUserId(out _);

    public TenantId CurrentTenantId =>
        Principal.TryGetTenantId(out TenantId tenantId) ? tenantId : throw new TenantContextUnavailableException();

    public Guid CurrentUserId =>
        Principal.TryGetUserId(out Guid userId) ? userId : throw new TenantContextUnavailableException();

    public IReadOnlyCollection<TenantId> AccessibleTenantIds => IsAvailable ? [CurrentTenantId] : [];
}
