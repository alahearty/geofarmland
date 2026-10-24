using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Admin;

public sealed record AdminUserDto(string Id, string Email, string? FullName, IReadOnlyList<string> Roles, DateTime CreatedAt);

public sealed record AdminOverviewResponse(
    int Users,
    int Admins,
    int Plots,
    int Devices,
    int Readings,
    IReadOnlyList<AdminUserDto> RecentUsers);

public class AdminOverviewEndpoint : EndpointWithoutRequest<AdminOverviewResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminOverviewEndpoint(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override void Configure()
    {
        Get("/api/admin/overview");
        Policies("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        var allUsers = await _userManager.Users.AsNoTracking().ToListAsync(ct);
        var admins = await _userManager.GetUsersInRoleAsync(DataSeeder.AdminRole);

        var recentUsers = new List<AdminUserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            recentUsers.Add(new AdminUserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                roles.ToList(),
                user.CreatedAt));
        }

        await SendAsync(new AdminOverviewResponse(
            allUsers.Count,
            admins.Count,
            await _db.Plots.CountAsync(ct),
            await _db.Devices.CountAsync(ct),
            await _db.SensorData.CountAsync(ct),
            recentUsers), cancellation: ct);
    }
}
