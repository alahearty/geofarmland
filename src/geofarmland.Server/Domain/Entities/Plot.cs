using NetTopologySuite.Geometries;

namespace Geofarmland.Server.Domain.Entities
{
    public class Plot : BaseEntity
    {
        public required string Name { get; set; }
        public required Geometry Geometry { get; set; } // GeoJSON data
    }
}
