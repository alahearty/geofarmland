using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Auth.Me;
using Geofarmland.Server.Shared;
using Microsoft.AspNetCore.Identity;

namespace Geofarmland.Server.Features.Auth.Profile;

public class UpdateMeRequest
{
    public string? FullName { get; set; }
}

public class UpdateMeEndpoint : Endpoint<UpdateMeRequest, MeResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateMeEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Put("/api/auth/me");
        Policies("Authenticated");
        Throttle(20, 60);
    }

    public override async Task HandleAsync(UpdateMeRequest req, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var name = req.FullName?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            ThrowError("Full name must be between 2 and 120 characters.");
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        user.FullName = name;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ThrowError(string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        var roles = await _userManager.GetRolesAsync(user);
        await SendOkAsync(new MeResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList()
        }, ct);
    }
}
