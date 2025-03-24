namespace geofarmland.Server.Application.Features.Plots.CreatePlot
{
    public class CreatePlotRequest
    {
        public string Name { get; set; }
        public double[][] Coordinates { get; set; } // GeoJSON Polygon
    }

}
