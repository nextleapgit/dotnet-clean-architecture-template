using CleanArchitecture.Application.Abstractions.Authentication;
using CleanArchitecture.BuildingBlocks.Auditing;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.BuildingBlocks.Tenancy;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Logout;

internal sealed class LogoutUserCommandHandler(
    IUnitOfWork unitOfWork,
    IRefreshTokenStore refreshTokenStore,
    ITokenProvider tokenProvider,
    ICurrentTenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IAuditLog auditLog) : ICommandHandler<LogoutUserCommand>
{
    public async Task<Result> HandleAsync(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        RefreshToken? refreshToken = await refreshTokenStore.FindByHashAsync(
            tokenProvider.HashRefreshToken(command.RefreshToken),
            cancellationToken);

        // Another user's token is reported exactly like an unknown one.
        if (refreshToken is null || refreshToken.UserId != tenantContext.CurrentUserId)
        {
            return Result.Failure(UserErrors.InvalidRefreshToken);
        }

        DateTime utcNow = dateTimeProvider.UtcNow;

        IReadOnlyList<RefreshToken> family = await refreshTokenStore.GetActiveFamilyAsync(
            refreshToken.FamilyId,
            utcNow,
            cancellationToken);

        foreach (RefreshToken token in family)
        {
            token.Revoke(utcNow);
        }

        auditLog.Record(new AuditRecord(UserAuditActions.LoggedOut, nameof(User), refreshToken.UserId.ToString()));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
