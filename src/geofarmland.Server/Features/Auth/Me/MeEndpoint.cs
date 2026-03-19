using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Geofarmland.Server.Features.Auth.Me;

public class MeEndpoint : EndpointWithoutRequest<MeResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public MeEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Get("/api/auth/me");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var roles = await _userManager.GetRolesAsync(user);
        await SendOkAsync(new MeResponse
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Roles = roles.ToList()
        }, ct);
    }
}
