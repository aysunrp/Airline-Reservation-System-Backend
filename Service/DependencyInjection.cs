using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service.Helpers.Settings;
using Service.Services;
using Service.Services.Interfaces;
using Service.Services.VoiceSearch;

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
            services.Configure<TravelAssistantSettings>(configuration.GetSection(TravelAssistantSettings.SectionName));
            services.Configure<VoiceSearchSettings>(configuration.GetSection(VoiceSearchSettings.SectionName));
            services.Configure<NewsletterSettings>(configuration.GetSection(NewsletterSettings.SectionName));
            services.Configure<AdminNotificationSettings>(configuration.GetSection(AdminNotificationSettings.SectionName));
            services.Configure<BookingSettings>(configuration.GetSection(BookingSettings.SectionName));
            services.Configure<SeatLayoutSettings>(configuration.GetSection(SeatLayoutSettings.SectionName));
            services.Configure<FlightGenerationSettings>(configuration.GetSection(FlightGenerationSettings.SectionName));
            services.Configure<TravelDocumentSettings>(configuration.GetSection(TravelDocumentSettings.SectionName));
            services.Configure<BookingLifecycleSettings>(configuration.GetSection(BookingLifecycleSettings.SectionName));
            services.Configure<SeedSettings>(configuration.GetSection(SeedSettings.SectionName));
            services.Configure<BootstrapSettings>(configuration.GetSection(BootstrapSettings.SectionName));
            services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName));
            services.Configure<PaymentSettings>(configuration.GetSection(PaymentSettings.SectionName));

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
            services.AddScoped<INotificationEmailService, NotificationEmailService>();
            services.AddScoped<IAirportService, AirportService>();
            services.AddScoped<IFlightSearchService, FlightSearchService>();
            services.AddScoped<ISeatService, SeatService>();
            services.AddScoped<IExtraServiceOptionsService, ExtraServiceOptionsService>();
            services.AddScoped<IBookingExpiryService, BookingExpiryService>();
            services.AddSingleton<ITravelDocumentPolicy, TravelDocumentPolicy>();
            services.AddScoped<IBookingEmailConfirmationService, BookingEmailConfirmationService>();
            // Ask AEROVA: the provider is one HttpClient, so the way it is reached (and its timeouts) is set here and nowhere else.
            services.AddHttpClient<IAssistantProvider, GeminiAssistantProvider>(client => client.Timeout = TimeSpan.FromMinutes(2));
            services.AddSingleton<AssistantFactsCache>();
            services.AddScoped<IAssistantKnowledge, AssistantKnowledge>();
            services.AddScoped<IAssistantService, AssistantService>();
            services.AddScoped<IVoiceSearchService, VoiceSearchService>();
            services.AddScoped<INewsletterService, NewsletterService>();
            services.AddScoped<IAdminAlertService, AdminAlertService>();
            services.AddScoped<ICampaignService, CampaignService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IBookingConfirmationService, BookingConfirmationService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddSingleton<IStripeCheckoutGateway, StripeCheckoutGateway>();
            services.AddScoped<ICheckoutService, CheckoutService>();
            services.AddScoped<IStripeWebhookService, StripeWebhookService>();
            services.AddScoped<ICancellationService, CancellationService>();
            services.AddScoped<ICancellationLinkService, CancellationLinkService>();
            services.AddScoped<IRefundProcessingService, RefundProcessingService>();
            services.AddSingleton<IStripeRefundGateway, StripeRefundGateway>();
            services.AddScoped<IRefundService, RefundService>();
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
            services.AddScoped<ISuperAdminProvisioningService, SuperAdminProvisioningService>();
            services.AddHostedService<BookingExpiryWorker>();
            services.AddHostedService<FlightGenerationWorker>();
            services.AddHostedService<BookingLifecycleWorker>();
            services.AddHostedService<NotificationEmailWorker>();
            services.AddHostedService<EmailCampaignWorker>();

            return services;
        }
    }
}
