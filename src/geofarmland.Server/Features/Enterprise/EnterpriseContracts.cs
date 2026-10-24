namespace Geofarmland.Server.Features.Enterprise;

public sealed record EnterpriseOrganizationDto(
    int Id,
    string Name,
    string Slug,
    string Country,
    string CurrencyCode,
    string TimeZone,
    string Role);

public sealed record EnterpriseContextResponse(
    string UserId,
    string Email,
    string? FullName,
    bool IsPlatformAdmin,
    EnterpriseOrganizationDto? ActiveOrganization,
    IReadOnlyList<EnterpriseOrganizationDto> Organizations,
    bool CanManageFarms,
    bool CanManageParcels);

public sealed record EnterpriseMetricsDto(
    int Farms,
    int Parcels,
    decimal Hectares,
    int Devices,
    int ReadingsToday,
    int WeatherObservations);

public sealed record FarmOverviewDto(
    int Id,
    string Name,
    string? Code,
    string? Region,
    int ParcelCount,
    decimal Hectares);

public sealed record LandUseBreakdownDto(
    string LandUse,
    int Parcels,
    decimal Hectares);

public sealed record RecentParcelDto(
    int Id,
    string Name,
    string? Code,
    string FarmName,
    string LandUse,
    string Status,
    decimal Hectares,
    List<List<double>> Coordinates);

public sealed record RecentReadingDto(
    int Id,
    string DeviceId,
    double SoilMoisture,
    double Rainfall,
    DateTime Timestamp);

public sealed record WeatherOverviewDto(
    double TemperatureC,
    double FeelsLikeC,
    double Humidity,
    double WindSpeed,
    string Condition,
    string Description,
    string Location,
    DateTime FetchedAtUtc);

public sealed record EnterpriseOverviewResponse(
    EnterpriseOrganizationDto Organization,
    bool CanManageFarms,
    bool CanManageParcels,
    EnterpriseMetricsDto Metrics,
    IReadOnlyList<FarmOverviewDto> Farms,
    IReadOnlyList<LandUseBreakdownDto> LandUse,
    IReadOnlyList<RecentParcelDto> RecentParcels,
    IReadOnlyList<RecentReadingDto> RecentReadings,
    WeatherOverviewDto? LatestWeather);
