using FastEndpoints;
using Geofarmland.Server.Infrastructure.Persistence;
using Geofarmland.Server.Shared;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Enterprise;

public class GetEnterpriseOverviewEndpoint : EndpointWithoutRequest<EnterpriseOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetEnterpriseOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/enterprise/overview");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var requestedOrganizationId = ParseOrganizationId();
        var access = await OrganizationAccess.ResolveAsync(_db, User, requestedOrganizationId, ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var organizationId = access.Organization.Id;
        var farms = await _db.Farms
            .AsNoTracking()
            .Where(farm => farm.OrganizationId == organizationId)
            .OrderBy(farm => farm.Name)
            .Select(farm => new FarmOverviewDto(
                farm.Id,
                farm.Name,
                farm.Code,
                farm.Region,
                farm.LandParcels.Count,
                farm.LandParcels.Sum(parcel => (decimal?)parcel.AreaHectares) ?? 0m))
            .ToListAsync(ct);

        var landUseRows = await _db.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.OrganizationId == organizationId)
            .GroupBy(parcel => parcel.LandUse)
            .Select(group => new
            {
                LandUse = group.Key,
                Parcels = group.Count(),
                Hectares = group.Sum(parcel => (decimal?)parcel.AreaHectares) ?? 0m
            })
            .OrderByDescending(item => item.Hectares)
            .ToListAsync(ct);
        var landUse = landUseRows
            .Select(item => new LandUseBreakdownDto(item.LandUse.ToString(), item.Parcels, item.Hectares))
            .ToList();

        var parcelRows = await _db.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.OrganizationId == organizationId)
            .OrderByDescending(parcel => parcel.CreatedAt)
            .Take(6)
            .Select(parcel => new
            {
                parcel.Id,
                parcel.Name,
                parcel.Code,
                FarmName = parcel.Farm != null ? parcel.Farm.Name : "Unassigned",
                parcel.LandUse,
                parcel.Status,
                parcel.AreaHectares,
                parcel.Geometry
            })
            .ToListAsync(ct);

        var recentParcels = parcelRows
            .Select(parcel => new RecentParcelDto(
                parcel.Id,
                parcel.Name,
                parcel.Code,
                parcel.FarmName,
                parcel.LandUse.ToString(),
                parcel.Status.ToString(),
                parcel.AreaHectares,
                GeoMeasurement.ToCoordinateList(parcel.Geometry)))
            .ToList();

        var memberIds = await OrganizationAccess.GetMemberUserIdsAsync(_db, organizationId, ct);
        var deviceCount = memberIds.Count == 0
            ? 0
            : await _db.Devices.CountAsync(device => device.OwnerId != null && memberIds.Contains(device.OwnerId), ct);
        var today = DateTime.UtcNow.Date;
        var readingsToday = memberIds.Count == 0
            ? 0
            : await _db.SensorData.CountAsync(reading => reading.OwnerId != null
                && memberIds.Contains(reading.OwnerId)
                && reading.Timestamp >= today, ct);

        var recentReadings = memberIds.Count == 0
            ? new List<RecentReadingDto>()
            : await _db.SensorData
                .AsNoTracking()
                .Where(reading => reading.OwnerId != null && memberIds.Contains(reading.OwnerId))
                .OrderByDescending(reading => reading.Timestamp)
                .Take(5)
                .Select(reading => new RecentReadingDto(
                    reading.Id,
                    reading.DeviceId,
                    reading.SoilMoisture,
                    reading.Rainfall,
                    reading.Timestamp))
                .ToListAsync(ct);

        var legacyPlotIds = await _db.LandParcels
            .AsNoTracking()
            .Where(parcel => parcel.OrganizationId == organizationId && parcel.LegacyPlotId != null)
            .Select(parcel => parcel.LegacyPlotId!.Value)
            .ToListAsync(ct);

        var weatherQuery = _db.WeatherSnapshots.AsNoTracking().AsQueryable();
        if (memberIds.Count > 0 && legacyPlotIds.Count > 0)
        {
            weatherQuery = weatherQuery.Where(weather =>
                (weather.OwnerId != null && memberIds.Contains(weather.OwnerId))
                || (weather.PlotId != null && legacyPlotIds.Contains(weather.PlotId.Value)));
        }
        else if (memberIds.Count > 0)
        {
            weatherQuery = weatherQuery.Where(weather => weather.OwnerId != null && memberIds.Contains(weather.OwnerId));
        }
        else if (legacyPlotIds.Count > 0)
        {
            weatherQuery = weatherQuery.Where(weather => weather.PlotId != null && legacyPlotIds.Contains(weather.PlotId.Value));
        }
        else
        {
            weatherQuery = weatherQuery.Where(_ => false);
        }

        var weatherObservations = await weatherQuery.CountAsync(ct);
        var latestWeather = await weatherQuery
            .OrderByDescending(weather => weather.FetchedAtUtc)
            .Select(weather => new WeatherOverviewDto(
                weather.TemperatureC,
                weather.FeelsLikeC,
                weather.Humidity,
                weather.WindSpeed,
                weather.Condition,
                weather.Description,
                weather.Location,
                weather.FetchedAtUtc))
            .FirstOrDefaultAsync(ct);

        var totalHectares = farms.Sum(farm => farm.Hectares);
        await SendAsync(new EnterpriseOverviewResponse(
            access.ToDto(),
            access.CanManageFarms,
            access.CanManageParcels,
            new EnterpriseMetricsDto(
                farms.Count,
                landUse.Sum(item => item.Parcels),
                totalHectares,
                deviceCount,
                readingsToday,
                weatherObservations),
            farms,
            landUse,
            recentParcels,
            recentReadings,
            latestWeather), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
