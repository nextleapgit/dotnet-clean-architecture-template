using CleanArchitecture.SharedKernel;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CleanArchitecture.Infrastructure.Database;

internal sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(id => id.Value, value => new TenantId(value));
