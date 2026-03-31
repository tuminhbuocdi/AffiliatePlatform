using Management.Application.Auth;
using Management.Infrastructure.Db;
using Management.Infrastructure.Repositories;
using Management.Infrastructure.External;

namespace Management.Api.Extensions;

public static class ServiceExtension
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<JwtService>();
        services.AddSingleton<PasswordHasher>();

        services.AddScoped<UserRepository>();
        services.AddScoped<PaymentTransactionRepository>();
        
        // Affiliate repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ProductAffiliateLinkRepository>();
        services.AddScoped<ProductSocialLinkRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IAffiliateClickRepository, AffiliateClickRepository>();
        services.AddScoped<MusicRepository>();
        services.AddScoped<ScheduledPostRepository>();
        services.AddScoped<AssTemplateRepository>();
        services.AddScoped<GeneratedSubtitleRepository>();
        
        // External API services
        services.AddHttpClient<IAccessTradeApiService, AccessTradeApiService>();

        // YouTube services
        services.AddScoped<YoutubeChannelRepository>();
        services.AddHttpClient<YoutubeOAuthService>();
        services.AddHttpClient<YoutubeApiClient>();

        // Facebook services
        services.AddScoped<FacebookPageRepository>();
        services.AddHttpClient<FacebookOAuthService>();
        services.AddHttpClient<FacebookApiClient>(c =>
        {
            c.Timeout = TimeSpan.FromMinutes(2);
        });
        
        // Application services
    }
}
