using System.Security.Claims;

namespace Crm.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
        => int.TryParse(principal.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Invalid token: missing subject claim");
}
