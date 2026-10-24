using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.RealTimeMonitoring.GetRealTimeMonitoring;

public sealed record WeatherHistoryItem(
    int Id,
    int? PlotId,
    double TemperatureC,
    double FeelsLikeC,
    double Humidity,
    double Pressure,
    double WindSpeed,
    string Condition,
    string Description,
    string Location,
    DateTime FetchedAtUtc);

public class GetWeatherHistory : EndpointWithoutRequest<List<WeatherHistoryItem>>
{
    private readonly ApplicationDbContext _db;

    public GetWeatherHistory(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/weather/history");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ownerId = CurrentUser.Id(User);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var query = _db.WeatherSnapshots.AsNoTracking().AsQueryable();
        if (!CurrentUser.IsAdmin(User))
            query = query.Where(snapshot => snapshot.OwnerId == ownerId);

        var plotId = Query<int?>("plotId", isRequired: false);
        if (plotId.HasValue)
            query = query.Where(snapshot => snapshot.PlotId == plotId.Value);

        var take = Math.Clamp(Query<int?>("take", isRequired: false) ?? 20, 1, 50);
        var history = await query
            .OrderByDescending(snapshot => snapshot.FetchedAtUtc)
            .Take(take)
            .Select(snapshot => new WeatherHistoryItem(
                snapshot.Id,
                snapshot.PlotId,
                snapshot.TemperatureC,
                snapshot.FeelsLikeC,
                snapshot.Humidity,
                snapshot.Pressure,
                snapshot.WindSpeed,
                snapshot.Condition,
                snapshot.Description,
                snapshot.Location,
                snapshot.FetchedAtUtc))
            .ToListAsync(ct);

        await SendAsync(history, cancellation: ct);
    }
}
