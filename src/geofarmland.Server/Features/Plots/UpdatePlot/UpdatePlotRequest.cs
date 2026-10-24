namespace Geofarmland.Server.Features.Plots.UpdatePlot
{
    public class UpdatePlotRequest
    {
        public required string Name { get; set; }
        public required double[][] Coordinates { get; set; }
    }
}
