namespace Geofarmland.Server.Features.Plots.CreatePlot
{
    public class CreatePlotRequest
    {
        public required string Name { get; set; }
        public required double[][] Coordinates { get; set; } // GeoJSON Polygon
    }

}
