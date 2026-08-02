using ReturnLoad.Application.Abstractions.Persistence;
using ReturnLoad.Domain.Identity;
using ReturnLoad.Domain.ValueObjects;
using ReturnLoad.Shared.Results;

namespace ReturnLoad.Application.UseCases.Profiles;

/// <summary>The details a person supplies to create their own platform profile.</summary>
public sealed record CreateUserProfileRequest(string FullName, string Mobile, string? Email);

/// <summary>A person's platform profile, as returned to the owning account.</summary>
public sealed record UserProfileView(Guid Id, string? FullName, string Mobile, string? Email);

/// <summary>
/// A person's own <see cref="UserProfile"/> — the identity every marketplace action hangs off.
/// Drivers get one as a side effect of <c>drivers/register</c>; load owners had no equivalent,
/// so a registered shipper could never satisfy the "Complete your profile" gate on posting a
/// load. This service is that missing path, and is role-agnostic: it creates the profile for
/// whoever is signed in.
/// </summary>
public interface IUserProfileService
{
    /// <summary>The signed-in account's profile, or NotFound when they have not created one.</summary>
    Task<Result<UserProfileView>> GetMineAsync(Guid authUserId, CancellationToken cancellationToken = default);

    /// <summary>Creates the signed-in account's profile. One per account.</summary>
    Task<Result<Guid>> CreateAsync(
        Guid authUserId, CreateUserProfileRequest request, CancellationToken cancellationToken = default);
}

internal sealed class UserProfileService : IUserProfileService
{
    private readonly IRepository<UserProfile> _users;
    private readonly IUnitOfWork _uow;

    public UserProfileService(IRepository<UserProfile> users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<UserProfileView>> GetMineAsync(
        Guid authUserId, CancellationToken cancellationToken = default)
    {
        UserProfile? profile = await FindAsync(authUserId, cancellationToken);
        return profile is null
            ? Error.NotFound("This account does not have a profile yet.")
            : Result<UserProfileView>.Success(Map(profile));
    }

    public async Task<Result<Guid>> CreateAsync(
        Guid authUserId, CreateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        UserProfile? existing = await FindAsync(authUserId, cancellationToken);
        if (existing is not null)
        {
            return Error.Conflict("This account already has a profile.");
        }

        UserProfile profile = UserProfile.Create(
            authUserId,
            request.FullName,
            MobileNumber.Create(request.Mobile),
            string.IsNullOrWhiteSpace(request.Email) ? null : EmailAddress.Create(request.Email));

        await _users.AddAsync(profile, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(profile.Id);
    }

    private async Task<UserProfile?> FindAsync(Guid authUserId, CancellationToken cancellationToken) =>
        (await _users.ListAsync(u => u.AuthUserId == authUserId, cancellationToken)).FirstOrDefault();

    private static UserProfileView Map(UserProfile profile) =>
        new(profile.Id, profile.FullName, profile.Mobile.Value, profile.Email?.Value);
}
