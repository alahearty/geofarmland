using NetTopologySuite.Geometries;

namespace Geofarmland.Server.Shared;

public static class GeoMeasurement
{
    private const double EarthRadiusMetres = 6_378_137d;
    private const double SquareMetresPerHectare = 10_000d;

    public static decimal AreaHectares(Polygon polygon)
    {
        if (polygon.IsEmpty)
            return 0m;

        var squareMetres = Math.Abs(RingArea(polygon.ExteriorRing.Coordinates));
        for (var i = 0; i < polygon.NumInteriorRings; i++)
            squareMetres -= Math.Abs(RingArea(polygon.GetInteriorRingN(i).Coordinates));

        return Math.Round((decimal)(Math.Max(0d, squareMetres) / SquareMetresPerHectare), 4);
    }

    public static List<List<double>> ToCoordinateList(Geometry geometry)
    {
        var coordinates = geometry switch
        {
            Polygon polygon => polygon.ExteriorRing.Coordinates,
            LineString line => line.Coordinates,
            _ => geometry.Coordinates
        };

        return coordinates
            .Select(coordinate => new List<double> { coordinate.X, coordinate.Y })
            .ToList();
    }

    private static double RingArea(Coordinate[] coordinates)
    {
        if (coordinates.Length < 3)
            return 0d;

        var area = 0d;
        for (var i = 0; i < coordinates.Length; i++)
        {
            var current = coordinates[i];
            var next = coordinates[(i + 1) % coordinates.Length];
            area += ToRadians(next.X - current.X) *
                (2d + Math.Sin(ToRadians(current.Y)) + Math.Sin(ToRadians(next.Y)));
        }

        return (area * EarthRadiusMetres * EarthRadiusMetres) / 2d;
    }

    private static double ToRadians(double value) => value * Math.PI / 180d;
}
