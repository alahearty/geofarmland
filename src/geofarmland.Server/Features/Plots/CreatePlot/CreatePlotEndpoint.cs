using FastEndpoints;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using Geofarmland.Server.Infrastructure.Repositories;
using Geofarmland.Server.Domain.Entities;

namespace Geofarmland.Server.Features.Plots.CreatePlot
{
    public class CreatePlotEndpoint : Endpoint<CreatePlotRequest>
    {
        private readonly PlotRepository _repo;
        private readonly GeometryFactory _geometryFactory;

        public CreatePlotEndpoint(PlotRepository repo)
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
            var plot = new Plot { Id = 0, Name = req.Name, Geometry = polygon };

            await _repo.AddAsync(plot, ct);
            await SendAsync(new { Message = "Plot Created", PlotId = plot.Id }, cancellation: ct);
        }
    }
}
