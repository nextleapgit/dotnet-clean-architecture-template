namespace CleanArchitecture.BuildingBlocks.Tenancy;

public sealed class TenantContextUnavailableException()
    : InvalidOperationException("The tenant context is not available for this request.");
