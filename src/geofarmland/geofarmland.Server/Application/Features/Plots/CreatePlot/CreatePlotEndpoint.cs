using FastEndpoints;
using geofarmland.Server.Application.Contracts;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace geofarmland.Server.Application.Features.Plots.CreatePlot
{
    public class CreatePlotEndpoint : Endpoint<CreatePlotRequest>
    {
        private readonly IPlotRepository _repo;
        private readonly GeometryFactory _geometryFactory;

        public CreatePlotEndpoint(IPlotRepository repo)
        {
            _repo = repo;
            _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        }

        public override void Configure()
        {
            Post("/plots");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CreatePlotRequest req, CancellationToken ct)
        {
            var polygon = _geometryFactory.CreatePolygon(req.Coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray());
            var plot = new Plot { Id = Guid.NewGuid(), Name = req.Name, Geometry = polygon };

            await _repo.AddAsync(plot, ct);
            await SendAsync(new { Message = "Plot Created", PlotId = plot.Id });
        }
    }
}
