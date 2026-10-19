using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using Repository.Repositories;
using Repository.Repositories.Interfaces;

namespace Repository
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRepositoryLayer(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            // Generic repository for every entity without specific queries.
            services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));

            services.AddScoped<IAirportRepository, AirportRepository>();
            services.AddScoped<IAircraftRepository, AircraftRepository>();
            services.AddScoped<IRouteRepository, RouteRepository>();
            services.AddScoped<IFlightScheduleRepository, FlightScheduleRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ISavedPassengerRepository, SavedPassengerRepository>();
            services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
            services.AddScoped<IBaggageOptionRepository, BaggageOptionRepository>();
            services.AddScoped<IMealOptionRepository, MealOptionRepository>();
            services.AddScoped<ICancellationRuleRepository, CancellationRuleRepository>();
            services.AddScoped<IFlightRepository, FlightRepository>();
            services.AddScoped<IFlightSeatRepository, FlightSeatRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IBookingPassengerRepository, BookingPassengerRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();

            return services;
        }
    }
}
