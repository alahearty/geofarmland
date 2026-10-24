using FastEndpoints;
using Geofarmland.Server.Shared;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Plots.GetPlot
{
    public class GetPlotEndpoint : EndpointWithoutRequest<GetPlotsResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetPlotEndpoint(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override void Configure()
        {
            Get("/plots/{id}");
            Policies("Authenticated");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<int>("id");
            var plot = await _dbContext.Plots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (plot == null || !CurrentUser.CanAccess(plot.OwnerId, User))
            {
                await SendNotFoundAsync(ct);
                return;
            }

            await SendAsync(new GetPlotsResponse
            {
                Id = plot.Id,
                Name = plot.Name,
                Coordinates = plot.Geometry.Coordinates
                    .Select(coordinate => new List<double> { coordinate.X, coordinate.Y })
                    .ToList()
            }, cancellation: ct);
        }
    }
}

