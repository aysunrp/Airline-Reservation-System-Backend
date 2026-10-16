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

            services.TryAddSingleton(TimeProvider.System);
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            services.AddScoped<IPricingService, PricingService>();
            services.AddScoped<IPromoCodeService, PromoCodeService>();
            services.AddScoped<ICancellationPolicyService, CancellationPolicyService>();

            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProfileService, ProfileService>();

            return services;
        }
    }
}
