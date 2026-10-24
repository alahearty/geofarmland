using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Features.Enterprise;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.IoT;

public sealed record IotDeviceDto(
    int Id,
    string DeviceId,
    string Name,
    int? PlotId,
    string? PlotName,
    int? LandParcelId,
    string? LandParcelName,
    string? FarmName,
    int ReadingCount,
    DateTime? LastReadingAt,
    double? LastSoilMoisture,
    double? LastRainfall,
    bool ReportingLast24h,
    DateTime CreatedAt);

public sealed record IotReadingDto(
    int Id,
    string DeviceId,
    string? DeviceName,
    string? LandParcelName,
    string? FarmName,
    double Latitude,
    double Longitude,
    double Rainfall,
    double SoilMoisture,
    DateTime Timestamp);

public sealed record IotMetricsDto(
    int Devices,
    int ReportingLast24Hours,
    int ReadingsLast24Hours,
    double? AverageSoilMoisture,
    double RainfallLast24Hours);

public sealed record IotOverviewResponse(
    bool CanManage,
    IotMetricsDto Metrics,
    IReadOnlyList<IotDeviceDto> Devices,
    IReadOnlyList<IotReadingDto> RecentReadings);

public class GetIotOverviewEndpoint : EndpointWithoutRequest<IotOverviewResponse>
{
    private readonly ApplicationDbContext _db;

    public GetIotOverviewEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/iot/overview");
        Policies("Authenticated");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var access = await OrganizationAccess.ResolveAsync(_db, User, ParseOrganizationId(), ct);
        if (access == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var memberIds = await OrganizationAccess.GetMemberUserIdsAsync(_db, access.Organization.Id, ct);
        if (memberIds.Count == 0)
        {
            await SendAsync(new IotOverviewResponse(
                access.CanManageParcels,
                new IotMetricsDto(0, 0, 0, null, 0),
                [],
                []), cancellation: ct);
            return;
        }

        var devices = await _db.Devices
            .AsNoTracking()
            .Include(device => device.Plot)
            .Include(device => device.LandParcel)
            .ThenInclude(parcel => parcel!.Farm)
            .Where(device => device.OwnerId != null && memberIds.Contains(device.OwnerId))
            .OrderBy(device => device.Name)
            .ToListAsync(ct);

        var since = DateTime.UtcNow.AddHours(-24);
        var recentReadings = await _db.SensorData
            .AsNoTracking()
            .Where(reading => reading.OwnerId != null && memberIds.Contains(reading.OwnerId))
            .OrderByDescending(reading => reading.Timestamp)
            .Take(200)
            .ToListAsync(ct);

        var readingsLast24Hours = recentReadings
            .Where(reading => reading.Timestamp >= since)
            .ToList();
        var readingStats = await _db.SensorData
            .AsNoTracking()
            .Where(reading => reading.OwnerId != null && memberIds.Contains(reading.OwnerId))
            .GroupBy(reading => new { reading.OwnerId, reading.DeviceId })
            .Select(group => new
            {
                group.Key.OwnerId,
                group.Key.DeviceId,
                Count = group.Count(),
                LastReadingAt = group.Max(reading => reading.Timestamp)
            })
            .ToListAsync(ct);

        var latestByKey = recentReadings
            .GroupBy(reading => $"{reading.OwnerId}|{reading.DeviceId}")
            .ToDictionary(group => group.Key, group => group.First());
        var devicesByKey = devices
            .Where(device => device.OwnerId != null)
            .GroupBy(device => $"{device.OwnerId}|{device.DeviceId}")
            .ToDictionary(group => group.Key, group => group.First());

        var deviceDtos = devices
            .Select(device =>
            {
                var key = $"{device.OwnerId}|{device.DeviceId}";
                var stats = readingStats.FirstOrDefault(item =>
                    item.OwnerId == device.OwnerId && item.DeviceId == device.DeviceId);
                var last = latestByKey.GetValueOrDefault(key);
                return new IotDeviceDto(
                    device.Id,
                    device.DeviceId,
                    device.Name,
                    device.PlotId,
                    device.Plot?.Name,
                    device.LandParcelId,
                    device.LandParcel?.Name,
                    device.LandParcel?.Farm?.Name,
                    stats?.Count ?? 0,
                    stats?.LastReadingAt,
                    last?.SoilMoisture,
                    last?.Rainfall,
                    stats?.LastReadingAt >= since,
                    device.CreatedAt);
            })
            .ToList();

        var recentDtos = recentReadings
            .Take(20)
            .Select(reading =>
            {
                var key = $"{reading.OwnerId}|{reading.DeviceId}";
                devicesByKey.TryGetValue(key, out var device);
                return new IotReadingDto(
                    reading.Id,
                    reading.DeviceId,
                    device?.Name,
                    device?.LandParcel?.Name,
                    device?.LandParcel?.Farm?.Name,
                    reading.Latitude,
                    reading.Longitude,
                    reading.Rainfall,
                    reading.SoilMoisture,
                    reading.Timestamp);
            })
            .ToList();

        var averageMoisture = readingsLast24Hours.Count == 0
            ? (double?)null
            : Math.Round(readingsLast24Hours.Average(reading => reading.SoilMoisture), 2);
        var rainfall = Math.Round(readingsLast24Hours.Sum(reading => reading.Rainfall), 2);

        await SendAsync(new IotOverviewResponse(
            access.CanManageParcels,
            new IotMetricsDto(
                devices.Count,
                deviceDtos.Count(device => device.ReportingLast24h),
                readingsLast24Hours.Count,
                averageMoisture,
                rainfall),
            deviceDtos,
            recentDtos), cancellation: ct);
    }

    private int? ParseOrganizationId()
    {
        var raw = HttpContext.Request.Query["organizationId"].FirstOrDefault();
        return int.TryParse(raw, out var organizationId) ? organizationId : null;
    }
}
