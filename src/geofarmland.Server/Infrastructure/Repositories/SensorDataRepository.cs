using Geofarmland.Server.Domain.Entities;
using Geofarmland.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Geofarmland.Server.Infrastructure.Repositories
{
    public class SensorDataRepository
    {
        private readonly ApplicationDbContext _context;

        public SensorDataRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SensorData>> GetAllAsync()
        {
            return await _context.SensorData.ToListAsync();
        }

        public async Task<SensorData?> GetByIdAsync(int id)
        {
            return await _context.SensorData.FindAsync(id);
        }

        public async Task AddAsync(SensorData sensorData)
        {
            await _context.SensorData.AddAsync(sensorData);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SensorData sensorData)
        {
            _context.SensorData.Update(sensorData);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var sensorData = await _context.SensorData.FindAsync(id);
            if (sensorData != null)
            {
                _context.SensorData.Remove(sensorData);
                await _context.SaveChangesAsync();
            }
        }
    }
}
