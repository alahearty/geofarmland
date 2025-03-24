namespace geofarmland.Server.Application.Features.Plots.GetPlot
{
    public record GetPlotsResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required List<List<double>> Coordinates { get; set; }
    }
}