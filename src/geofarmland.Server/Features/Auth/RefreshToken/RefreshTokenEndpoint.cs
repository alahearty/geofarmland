using FastEndpoints;
using FastEndpoints.Security;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Auth.Login;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Geofarmland.Server.Features.Auth.RefreshToken;

public class RefreshTokenEndpoint : Endpoint<RefreshTokenRequest, LoginResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public RefreshTokenEndpoint(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _config = config;
    }

    public override void Configure()
    {
        Post("/api/auth/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        var token = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == req.RefreshToken && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow, ct);

        if (token == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        token.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        var user = token.User;
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = CreateAccessToken(user, roles);
        var newRefreshToken = await CreateRefreshTokenAsync(user.Id, ct);

        await SendOkAsync(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToList()
        }, ct);
    }

    private string CreateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
        var issuer = _config["Jwt:Issuer"] ?? "Geofarmland.Server";
        var audience = _config["Jwt:Audience"] ?? "Geofarmland.Client";
        var expireMinutes = GetAccessTokenExpirationMinutes();

        return JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = key;
                o.Issuer = issuer;
                o.Audience = audience;
                o.ExpireAt = DateTime.UtcNow.AddMinutes(expireMinutes);
                o.User.Claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
                o.User.Claims.Add(new Claim(ClaimTypes.Email, user.Email ?? ""));
                o.User.Claims.Add(new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? ""));
                foreach (var role in roles)
                    o.User.Roles.Add(role);
            });
    }

    private async Task<Domain.Entities.RefreshToken> CreateRefreshTokenAsync(string userId, CancellationToken ct)
    {
        var days = int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "7");
        var token = new Domain.Entities.RefreshToken
        {
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(days),
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return token;
    }

    private int GetAccessTokenExpirationMinutes() =>
        int.TryParse(_config["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 60;
}
