using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Domain.Enums;
using Geofarmland.Server.Shared;

namespace Geofarmland.Server.Features.LandParcels;

public class LandParcelRequest
{
    public int? FarmId { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? LandUse { get; set; }
    public string? TenureType { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public double[][]? Coordinates { get; set; }
}

public sealed record LandParcelDto(
    int Id,
    int OrganizationId,
    int FarmId,
    string FarmName,
    string Name,
    string? Code,
    string LandUse,
    string TenureType,
    string Status,
    decimal AreaHectares,
    string? Notes,
    List<List<double>> Coordinates,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record LandParcelListResponse(
    IReadOnlyList<LandParcelDto> Items,
    int Total,
    int Page,
    int PageSize);

public static class LandParcelMapping
{
    public static LandParcelDto ToDto(LandParcel parcel) => new(
        parcel.Id,
        parcel.OrganizationId,
        parcel.FarmId,
        parcel.Farm?.Name ?? "Unassigned",
        parcel.Name,
        parcel.Code,
        parcel.LandUse.ToString(),
        parcel.TenureType.ToString(),
        parcel.Status.ToString(),
        parcel.AreaHectares,
        parcel.Notes,
        GeoMeasurement.ToCoordinateList(parcel.Geometry),
        parcel.CreatedAt,
        parcel.UpdatedAt);
}

internal readonly record struct LandParcelValues(
    string Name,
    string? Code,
    LandUseType LandUse,
    TenureType TenureType,
    LandParcelStatus Status,
    string? Notes);

internal static class LandParcelValidation
{
    public static bool TryNormalize(
        LandParcelRequest req,
        bool isUpdate,
        LandParcel? existing,
        out LandParcelValues values,
        out string error)
    {
        values = default;
        error = string.Empty;

        var name = req.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120)
        {
            error = "A land parcel name between 2 and 120 characters is required.";
            return false;
        }

        var code = string.IsNullOrWhiteSpace(req.Code) ? null : req.Code.Trim().ToUpperInvariant();
        if (code is { Length: > 40 })
        {
            error = "A land parcel code cannot exceed 40 characters.";
            return false;
        }

        var notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        if (notes is { Length: > 1000 })
        {
            error = "Land parcel notes cannot exceed 1000 characters.";
            return false;
        }

        var landUse = existing?.LandUse ?? LandUseType.Cropland;
        if (!string.IsNullOrWhiteSpace(req.LandUse)
            && !Enum.TryParse(req.LandUse, true, out landUse))
        {
            error = "Select a valid land use.";
            return false;
        }

        var tenureType = existing?.TenureType ?? TenureType.Owned;
        if (!string.IsNullOrWhiteSpace(req.TenureType)
            && !Enum.TryParse(req.TenureType, true, out tenureType))
        {
            error = "Select a valid tenure type.";
            return false;
        }

        var status = existing?.Status ?? LandParcelStatus.Active;
        if (!string.IsNullOrWhiteSpace(req.Status)
            && !Enum.TryParse(req.Status, true, out status))
        {
            error = "Select a valid land parcel status.";
            return false;
        }

        if (isUpdate && existing == null)
        {
            error = "The land parcel could not be loaded.";
            return false;
        }

        values = new LandParcelValues(name, code, landUse, tenureType, status, notes);
        return true;
    }
}
