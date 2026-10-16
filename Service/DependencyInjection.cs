using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;

namespace Service
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServiceLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PricingSettings>(configuration.GetSection(PricingSettings.SectionName));
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.Configure<BookingSettings>(configuration.GetSection(BookingSettings.SectionName));
            services.Configure<SeatLayoutSettings>(configuration.GetSection(SeatLayoutSettings.SectionName));

            services.TryAddSingleton(TimeProvider.System);
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddScoped<IPricingService, PricingService>();
            services.AddScoped<IPromoCodeService, PromoCodeService>();
            services.AddScoped<ICancellationPolicyService, CancellationPolicyService>();

            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAirportService, AirportService>();
            services.AddScoped<IFlightSearchService, FlightSearchService>();
            services.AddScoped<ISeatService, SeatService>();
            services.AddScoped<IBookingExpiryService, BookingExpiryService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<ICancellationService, CancellationService>();

            services.AddScoped<IAdminAircraftService, AdminAircraftService>();
            services.AddScoped<IAdminRouteService, AdminRouteService>();
            services.AddScoped<IAdminScheduleService, AdminScheduleService>();
            services.AddHostedService<BookingExpiryWorker>();

            return services;
        }
    }
}
