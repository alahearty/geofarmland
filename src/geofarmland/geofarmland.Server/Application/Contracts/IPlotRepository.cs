using geofarmland.Server.Domain.Entities;

namespace geofarmland.Server.Application.Contracts
{
    public interface IPlotRepository
    {
        Task AddAsync(Plot plot, CancellationToken ct);
        Task<List<Plot>> GetAllAsync(CancellationToken ct);
    }
}
