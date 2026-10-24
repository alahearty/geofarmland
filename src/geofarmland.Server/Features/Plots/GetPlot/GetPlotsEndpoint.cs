using FastEndpoints;
using Geofarmland.Server.Shared;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Features.Plots.GetPlot
{
    public class GetPlotsEndpoint : EndpointWithoutRequest<List<GetPlotsResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetPlotsEndpoint(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public override void Configure()
        {
            Get("/plots");
            Policies("Authenticated");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var userId = CurrentUser.Id(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                await SendUnauthorizedAsync(ct);
                return;
            }

            var query = _dbContext.Plots.AsNoTracking();
            if (!CurrentUser.IsAdmin(User))
                query = query.Where(plot => plot.OwnerId == userId);

            var plots = await query.ToListAsync(ct);

            var response = plots
                .Select(plot => new GetPlotsResponse
                {
                    Id = plot.Id,
                    Name = plot.Name,
                    Coordinates = plot.Geometry.Coordinates
                        .Select(coordinate => new List<double> { coordinate.X, coordinate.Y })
                        .ToList()
                })
                .ToList();

            await SendAsync(response, cancellation: ct);
        }
    }
}

