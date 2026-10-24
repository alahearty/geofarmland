using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Enterprise;

public class GetEnterpriseContextEndpoint : EndpointWithoutRequest<EnterpriseContextResponse>
{
    private readonly ApplicationDbContext _db;

    public GetEnterpriseContextEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/enterprise/context");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var requestedOrganizationId = ParseOrganizationId();
        var active = await OrganizationAccess.ResolveAsync(_db, User, requestedOrganizationId, ct);
        var accessible = await OrganizationAccess.GetAccessibleAsync(_db, User, ct);
        var profile = await _db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.FullName })
            .FirstOrDefaultAsync(ct);

        if (profile == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        await SendAsync(new EnterpriseContextResponse(
            userId,
            profile.Email ?? string.Empty,
            profile.FullName,
            CurrentUser.IsAdmin(User),
            active?.ToDto(),
            accessible.Select(access => access.ToDto()).ToList(),
            active?.CanManageFarms ?? false,
            active?.CanManageParcels ?? false), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
