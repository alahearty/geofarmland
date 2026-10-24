namespace Geofarmland.Server.Features.Devices;

public class DeviceRequest
{
    public string? DeviceId { get; set; }
    public string? Name { get; set; }
    public int? PlotId { get; set; }
    public int? LandParcelId { get; set; }
}

public sealed record DeviceResponse(
    int Id,
    string DeviceId,
    string Name,
    int? PlotId,
    string? PlotName,
    int? LandParcelId,
    string? LandParcelName,
    string? FarmName,
    DateTime CreatedAt);
