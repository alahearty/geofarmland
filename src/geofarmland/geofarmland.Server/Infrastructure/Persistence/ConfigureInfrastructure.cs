using Microsoft.EntityFrameworkCore;

namespace geofarmland.Server.Infrastructure.Persistence
{
    public static class ConfigureInfrastructure
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(config.GetConnectionString("Postgres"), o => o.UseNetTopologySuite()));

            return services;
        }
    }
}
