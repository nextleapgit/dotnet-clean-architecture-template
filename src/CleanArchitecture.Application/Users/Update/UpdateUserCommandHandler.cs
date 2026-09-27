using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.BuildingBlocks.Persistence;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Application.Users.Update;

internal sealed class UpdateUserCommandHandler(IUnitOfWork unitOfWork, UserManagement userManagement)
    : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        // Renaming cannot lock anyone out, so managers may edit their own name too.
        Result<User> user = await userManagement.FindManageableAsync(
            command.TenantId,
            command.UserId,
            allowSelf: true,
            cancellationToken);

        if (user.IsFailure)
        {
            return user;
        }

        user.Value.UpdateName(command.FirstName, command.LastName);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
