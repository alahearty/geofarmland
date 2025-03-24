using geofarmland.Server.Application.Contracts;
using geofarmland.Server.Domain.Entities;
using geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace geofarmland.Server.Infrastructure.Repositories
{
    public class PlotRepository : IPlotRepository
    {
        private readonly ApplicationDbContext _db;

        public PlotRepository(ApplicationDbContext db) => _db = db;

        public async Task AddAsync(Plot plot, CancellationToken ct)
        {
            _db.Plots.Add(plot);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<Plot>> GetAllAsync(CancellationToken ct)
        {
            return await _db.Plots.ToListAsync(ct);
        }
    }
}
