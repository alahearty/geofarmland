using FastEndpoints;
using Geofarmland.Server.Features.Auth.RefreshToken;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Auth.Logout;

public class LogoutEndpoint : Endpoint<RefreshTokenRequest>
{
    private readonly ApplicationDbContext _db;

    public LogoutEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/auth/logout");
        AllowAnonymous();
        Throttle(20, 60);
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RefreshToken))
        {
            var tokenHash = RefreshTokenHasher.Hash(req.RefreshToken);
            await _db.RefreshTokens
                .Where(rt => rt.Token == tokenHash && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true), ct);
        }

        // Logout is intentionally idempotent.
        await SendOkAsync(new { Message = "Signed out." }, ct);
    }
}
