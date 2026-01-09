using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Infrastructure.Repositories
{
    public class HydrologyRepository
    {
        private readonly ApplicationDbContext _context;

        public HydrologyRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Hydrology>> GetAllHydrologyDataAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Hydrologys.ToListAsync(cancellationToken);
        }

        public async Task<Hydrology?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Hydrologys.FindAsync([id], cancellationToken);
        }

        public async Task AddAsync(Hydrology hydrology, CancellationToken cancellationToken = default)
        {
            _context.Hydrologys.Add(hydrology);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Hydrologys.FindAsync([id], cancellationToken);
            if (entity != null)
            {
                _context.Hydrologys.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
