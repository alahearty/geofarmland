namespace geofarmland.Server.Application.Features.Plots.UpdatePlot
{
    public class UpdatePlotRequest
    {
        public string Name { get; set; }
        public List<List<List<double>>> Coordinates { get; set; }
    }

}
