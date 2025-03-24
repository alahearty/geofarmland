using FastEndpoints;
using geofarmland.Server.Infrastructure.Persistence;
using NetTopologySuite.Geometries;
using System;

namespace geofarmland.Server.Features.Plots.UpdatePlot
{
    public class UpdatePlotEndpoint : Endpoint<UpdatePlotRequest>
    {
        private readonly ApplicationDbContext _dbContext;

        public UpdatePlotEndpoint(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override void Configure()
        {
            Put("/plots/{id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(UpdatePlotRequest req, CancellationToken ct)
        {
            var id = Route<int>("id");
            var plot = await _dbContext.Plots.FindAsync([id], ct);
            if (plot == null)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            var coordinates = req.Coordinates.FirstOrDefault()?.Select(c => new Coordinate(c[0], c[1])).ToArray();
            if (coordinates == null || coordinates.Length == 0)
            {
                await SendErrorsAsync(400, ct);
                return;
            }

            plot.Name = req.Name;
            plot.Geometry = new Polygon(new LinearRing(coordinates));
            await _dbContext.SaveChangesAsync(ct);

            await SendAsync(new { Message = "Plot updated successfully", plot.Id }, cancellation: ct);
        }
    }
}
