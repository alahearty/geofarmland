using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Auth.Profile;

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordEndpoint : Endpoint<ChangePasswordRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public ChangePasswordEndpoint(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/auth/change-password");
        Policies("Authenticated");
        Throttle(10, 60);
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
        {
            ThrowError("Current and new passwords are required.");
            return;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
        {
            ThrowError(string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        // Invalidate every existing refresh session after a password change.
        await _db.RefreshTokens
            .Where(token => token.UserId == userId && !token.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true), ct);

        await SendOkAsync(new { Message = "Password changed. Sign in again on other devices." }, ct);
    }
}
