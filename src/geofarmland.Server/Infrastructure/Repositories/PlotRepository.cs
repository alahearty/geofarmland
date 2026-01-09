using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Infrastructure.Repositories
{
    public class PlotRepository
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
