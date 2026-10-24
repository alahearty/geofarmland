using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Geofarmland.Server.Features.Auth.Register;

public class RegisterEndpoint : Endpoint<RegisterRequest>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterEndpoint(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
        Throttle(10, 60);
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            EmailConfirmed = false,
            FullName = req.FullName ?? req.Email
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            ThrowError(errors, 400);
            return;
        }

        await SendOkAsync(new { Message = "Registration successful. You can now log in.", Email = user.Email }, ct);
    }
}
