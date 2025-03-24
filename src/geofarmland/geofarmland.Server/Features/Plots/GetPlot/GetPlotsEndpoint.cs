using FastEndpoints;
using geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace geofarmland.Server.Features.Plots.GetPlot
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
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var plots = await _dbContext.Plots
                .Select(p => new GetPlotsResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Coordinates = p.Geometry.Coordinates.Select(c => new List<double> { c.X, c.Y }).ToList()
                })
                .ToListAsync(ct);

            await SendAsync(plots, cancellation: ct);
        }
    }
}
