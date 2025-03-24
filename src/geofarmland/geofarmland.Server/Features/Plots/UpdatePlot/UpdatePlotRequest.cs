namespace Geofarmland.Server.Features.Plots.UpdatePlot
{
    public class UpdatePlotRequest
    {
        public required string Name { get; set; }
        public required List<List<List<double>>> Coordinates { get; init; }
    }

}
