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
            services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.SectionName));
            services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
            services.Configure<BookingSettings>(configuration.GetSection(BookingSettings.SectionName));
            services.Configure<SeatLayoutSettings>(configuration.GetSection(SeatLayoutSettings.SectionName));
            services.Configure<FlightGenerationSettings>(configuration.GetSection(FlightGenerationSettings.SectionName));
            services.Configure<TravelDocumentSettings>(configuration.GetSection(TravelDocumentSettings.SectionName));
            services.Configure<BookingLifecycleSettings>(configuration.GetSection(BookingLifecycleSettings.SectionName));
            services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));

            services.TryAddSingleton(TimeProvider.System);
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddScoped<IPricingService, PricingService>();
            services.AddScoped<IPromoCodeService, PromoCodeService>();
            services.AddScoped<ICancellationPolicyService, CancellationPolicyService>();

            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
            services.AddSingleton<IUserStateCache, UserStateCache>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddScoped<IAccountEmailService, AccountEmailService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProfileService, ProfileService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAirportService, AirportService>();
            services.AddScoped<IFlightSearchService, FlightSearchService>();
            services.AddScoped<ISeatService, SeatService>();
            services.AddScoped<IExtraServiceOptionsService, ExtraServiceOptionsService>();
            services.AddScoped<IBookingExpiryService, BookingExpiryService>();
            services.AddSingleton<ITravelDocumentPolicy, TravelDocumentPolicy>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<ICancellationService, CancellationService>();
            services.AddScoped<IRefundProcessingService, RefundProcessingService>();
            services.AddScoped<ITripCompletionService, TripCompletionService>();
            services.AddScoped<ICheckInReminderService, CheckInReminderService>();

            services.AddScoped<IAdminAirportService, AdminAirportService>();
            services.AddScoped<IAdminPromoCodeService, AdminPromoCodeService>();
            services.AddScoped<IAdminBookingOptionService, AdminBookingOptionService>();
            services.AddScoped<IAdminCancellationRuleService, AdminCancellationRuleService>();
            services.AddScoped<IAdminAircraftService, AdminAircraftService>();
            services.AddScoped<IAdminRouteService, AdminRouteService>();
            services.AddScoped<IAdminScheduleService, AdminScheduleService>();
            services.AddScoped<IAdminFlightService, AdminFlightService>();
            services.AddScoped<IFlightGenerationService, FlightGenerationService>();
            services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
            services.AddScoped<IDevelopmentAdminSeeder, DevelopmentAdminSeeder>();
            services.AddScoped<IAdminBookingService, AdminBookingService>();
            services.AddScoped<IAdminPassengerService, AdminPassengerService>();
            services.AddScoped<IAdminDashboardService, AdminDashboardService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<IAdminUserService, AdminUserService>();
            services.AddHostedService<BookingExpiryWorker>();
            services.AddHostedService<FlightGenerationWorker>();
            services.AddHostedService<BookingLifecycleWorker>();

            return services;
        }
    }
}
