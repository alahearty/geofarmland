using FastEndpoints;
using Geofarmland.Server.Shared;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Plots.DeletePlot
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
            Policies("Authenticated");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<int>("id");
            var plot = await _dbContext.Plots.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (plot == null || !CurrentUser.CanAccess(plot.OwnerId, User))
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

