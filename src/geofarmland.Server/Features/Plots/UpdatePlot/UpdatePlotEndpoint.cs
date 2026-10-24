using FastEndpoints;
using Geofarmland.Server.Shared;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Plots.UpdatePlot
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
            Policies("Authenticated");
        }

        public override async Task HandleAsync(UpdatePlotRequest req, CancellationToken ct)
        {
            var id = Route<int>("id");
            var plot = await _dbContext.Plots.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (plot == null || !CurrentUser.CanAccess(plot.OwnerId, User))
            {
                await SendNotFoundAsync(ct);
                return;
            }

            var name = req.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > 120)
            {
                ThrowError("A plot name between 1 and 120 characters is required.");
                return;
            }

            if (!PlotGeometry.TryCreate(req.Coordinates, out var polygon, out var geometryError))
            {
                ThrowError(geometryError);
                return;
            }

            plot.Name = name;
            plot.Geometry = polygon!;
            await _dbContext.SaveChangesAsync(ct);

            await SendAsync(new { Message = "Plot updated successfully", plot.Id }, cancellation: ct);
        }
    }
}

