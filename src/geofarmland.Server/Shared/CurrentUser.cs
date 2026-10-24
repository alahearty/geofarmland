using System.Security.Claims;
using Geofarmland.Server.Infrastructure.Persistence;

namespace Geofarmland.Server.Shared;

public static class CurrentUser
{
    public static string? Id(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole(DataSeeder.AdminRole)
        || user.Claims.Any(claim =>
            (claim.Type == ClaimTypes.Role || claim.Type == "role")
            && string.Equals(claim.Value, DataSeeder.AdminRole, StringComparison.OrdinalIgnoreCase));

    public static bool CanAccess(string? ownerId, ClaimsPrincipal user) =>
        IsAdmin(user) || (!string.IsNullOrWhiteSpace(ownerId) && string.Equals(ownerId, Id(user), StringComparison.Ordinal));
}
