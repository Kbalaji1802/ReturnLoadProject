using Microsoft.AspNetCore.Identity;

namespace ReturnLoad.Infrastructure.Identity;

/// <summary>A platform role (the nine of `03_TECHNICAL_BIBLE.md` §13), keyed by Guid.</summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
