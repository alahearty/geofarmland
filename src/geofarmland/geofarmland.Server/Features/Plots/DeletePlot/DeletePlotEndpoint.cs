using FastEndpoints;
using geofarmland.Server.Infrastructure.Persistence;
using System;

namespace geofarmland.Server.Features.Plots.DeletePlot
{
    public class DeletePlotEndpoint : EndpointWithoutRequest
    {
        private readonly ApplicationDbContext _dbContext;

        public DeletePlotEndpoint(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override void Configure()
        {
            Delete("/plots/{id}");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<int>("id");
            var plot = await _dbContext.Plots.FindAsync([id], ct);
            if (plot == null)
            {
                await SendNotFoundAsync(ct);
                return;
            }

            _dbContext.Plots.Remove(plot);
            await _dbContext.SaveChangesAsync(ct);

            await SendAsync(new { Message = "Plot deleted successfully" }, cancellation: ct);
        }
    }
}
