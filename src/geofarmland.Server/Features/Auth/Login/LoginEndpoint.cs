using System.Security.Claims;
using FastEndpoints;
using FastEndpoints.Security;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Auth.Login;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public LoginEndpoint(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext db,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _config = config;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user == null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = CreateAccessToken(user, roles);
        var refreshToken = await CreateRefreshTokenEntityAsync(user.Id, ct);

        await SendOkAsync(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
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

    private async Task<Domain.Entities.RefreshToken> CreateRefreshTokenEntityAsync(string userId, CancellationToken ct)
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
