using FastEndpoints;
using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Shared;
using Geofarmland.Server.Infrastructure.Repositories;

namespace Geofarmland.Server.Features.Plots.CreatePlot
{
    public class CreatePlotEndpoint : Endpoint<CreatePlotRequest>
    {
        private readonly PlotRepository _repo;

        public CreatePlotEndpoint(PlotRepository repo)
        {
            _repo = repo;
        }

        public override void Configure()
        {
            Post("/plots");
            Policies("Authenticated");
        }

        public override async Task HandleAsync(CreatePlotRequest req, CancellationToken ct)
        {
            var ownerId = CurrentUser.Id(User);
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                await SendUnauthorizedAsync(ct);
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

            var plot = new Plot
            {
                Name = name,
                Geometry = polygon!,
                OwnerId = ownerId
            };

            await _repo.AddAsync(plot, ct);
            await SendAsync(new { Message = "Plot Created", PlotId = plot.Id }, cancellation: ct);
        }
    }
}

