using System.Security.Claims;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Enterprise;

public sealed record OrganizationAccessInfo(Organization Organization, OrganizationRole Role, bool IsPlatformAdmin)
{
    public bool CanManageFarms =>
        IsPlatformAdmin
        || Role is OrganizationRole.OrganizationAdministrator or OrganizationRole.FarmManager;

    public bool CanManageParcels =>
        CanManageFarms
        || Role is OrganizationRole.Agronomist or OrganizationRole.FieldOfficer;
}

public static class OrganizationAccess
{
    public static async Task<List<OrganizationAccessInfo>> GetAccessibleAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = CurrentUser.Id(user);
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var memberships = await db.OrganizationMembers
            .AsNoTracking()
            .Include(member => member.Organization)
            .Where(member => member.UserId == userId
                && member.IsActive
                && member.Organization != null
                && member.Organization.IsActive)
            .OrderBy(member => member.JoinedAt)
            .ToListAsync(ct);

        var platformAdmin = CurrentUser.IsAdmin(user);
        var result = memberships
            .Where(member => member.Organization != null)
            .Select(member => new OrganizationAccessInfo(
                member.Organization!,
                platformAdmin ? OrganizationRole.OrganizationAdministrator : member.Role,
                platformAdmin))
            .ToList();

        if (!platformAdmin)
            return result;

        var knownIds = result.Select(access => access.Organization.Id).ToHashSet();
        var otherOrganizations = await db.Organizations
            .AsNoTracking()
            .Where(organization => organization.IsActive && !knownIds.Contains(organization.Id))
            .OrderBy(organization => organization.Name)
            .ToListAsync(ct);

        result.AddRange(otherOrganizations.Select(organization =>
            new OrganizationAccessInfo(organization, OrganizationRole.OrganizationAdministrator, true)));

        return result;
    }

    public static async Task<OrganizationAccessInfo?> ResolveAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        int? organizationId,
        CancellationToken ct)
    {
        var accessible = await GetAccessibleAsync(db, user, ct);
        if (organizationId.HasValue)
            return accessible.FirstOrDefault(access => access.Organization.Id == organizationId.Value);

        if (accessible.Count > 0)
            return accessible[0];

        if (CurrentUser.IsAdmin(user))
            return null;

        return await CreatePersonalWorkspaceAsync(db, user, ct);
    }

    public static async Task<List<string>> GetMemberUserIdsAsync(
        ApplicationDbContext db,
        int organizationId,
        CancellationToken ct)
    {
        return await db.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.IsActive)
            .Select(member => member.UserId)
            .ToListAsync(ct);
    }

    private static async Task<OrganizationAccessInfo> CreatePersonalWorkspaceAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var userId = CurrentUser.Id(user) ?? throw new InvalidOperationException("The current user has no identifier.");
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? "New user";
        var organization = new Organization
        {
            Name = $"{email.Split('@')[0]}'s workspace",
            Slug = $"personal-{userId[..Math.Min(userId.Length, 8)]}-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        db.Organizations.Add(organization);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            Organization = organization,
            UserId = userId,
            Role = OrganizationRole.OrganizationAdministrator,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new OrganizationAccessInfo(organization, OrganizationRole.OrganizationAdministrator, false);
    }
}
