using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Geofarmland.Server.Features.Plots;

/// <summary>
/// Shared polygon construction and validation for plot endpoints.
/// Every plot is created through the same WGS84 factory so updates cannot
/// silently reset the geometry SRID.
/// </summary>
public static class PlotGeometry
{
    private const int Srid = 4326;
    private const int MaxPoints = 10_000;
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: Srid);

    public static bool TryCreate(double[][]? rawCoordinates, out Polygon? polygon, out string error)
    {
        polygon = null;
        error = string.Empty;

        if (rawCoordinates is null || rawCoordinates.Length < 3)
        {
            error = "A polygon must have at least three points.";
            return false;
        }
        if (rawCoordinates.Length > MaxPoints)
        {
            error = $"A polygon cannot contain more than {MaxPoints} points.";
            return false;
        }

        var points = new List<Coordinate>(rawCoordinates.Length);
        foreach (var coordinate in rawCoordinates)
        {
            if (coordinate is null || coordinate.Length < 2)
            {
                error = "Each coordinate must contain longitude and latitude.";
                return false;
            }

            var longitude = coordinate[0];
            var latitude = coordinate[1];
            if (!double.IsFinite(longitude) || !double.IsFinite(latitude))
            {
                error = "Coordinates must be finite numbers.";
                return false;
            }
            if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
            {
                error = "Coordinates must use longitude [-180, 180] and latitude [-90, 90].";
                return false;
            }

            var point = new Coordinate(longitude, latitude);
            if (points.Count == 0 || !points[^1].Equals2D(point))
                points.Add(point);
        }

        if (points.Count > 1 && points[0].Equals2D(points[^1]))
            points.RemoveAt(points.Count - 1);

        if (points.Count < 3)
        {
            error = "A polygon must have at least three distinct points.";
            return false;
        }

        points.Add(new Coordinate(points[0].X, points[0].Y));

        try
        {
            var ring = Factory.CreateLinearRing(points.ToArray());
            var candidate = Factory.CreatePolygon(ring);
            if (!candidate.IsValid)
            {
                error = "The polygon is invalid. Check for self-intersections or overlapping edges.";
                return false;
            }
            if (candidate.Area <= 1e-12)
            {
                error = "The polygon must enclose an area greater than zero.";
                return false;
            }

            polygon = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            error = "The polygon could not be constructed from those coordinates.";
            return false;
        }
    }
}
