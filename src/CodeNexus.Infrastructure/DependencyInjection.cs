using CloudinaryDotNet;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Persistence;
using CodeNexus.Infrastructure.Services;
using CodeNexus.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CodeNexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("MyCnn")));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<AppDbContext>());

        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IOTPService, OTPService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.Configure<GoogleAuthSettings>(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings?.Issuer,
                ValidAudience = jwtSettings?.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.SecretKey ?? string.Empty))
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.Configure<CloudinarySettings>(configuration.GetSection(CloudinarySettings.SectionName));
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IPdfProcessingService, PdfProcessingService>();

        var redisConnectionString = configuration["Redis__ConnectionString"];

        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
        {
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                try
                {
                    var configOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString);
                    configOptions.AbortOnConnectFail = false;
                    configOptions.ConnectTimeout = 10000;
                    configOptions.SyncTimeout = 5000;
                    configOptions.ConnectRetry = 3;

                    return StackExchange.Redis.ConnectionMultiplexer.Connect(configOptions);
                }
                catch
                {
                    var fallbackOptions = StackExchange.Redis.ConfigurationOptions.Parse("localhost:6379");
                    fallbackOptions.AbortOnConnectFail = false;
                    return StackExchange.Redis.ConnectionMultiplexer.Connect(fallbackOptions);
                }
            }
            else
            {
                var fallbackOptions = StackExchange.Redis.ConfigurationOptions.Parse("localhost:6379");
                fallbackOptions.AbortOnConnectFail = false;
                return StackExchange.Redis.ConnectionMultiplexer.Connect(fallbackOptions);
            }
        });


        services.AddScoped<IAIConfigCacheService, AIConfigCacheService>();
        services.AddScoped<IOTPCacheService, OTPCacheService>();

        services.AddHttpClient<GroqServiceWithCache>();
        services.AddScoped<IAIGeneratorService, GroqServiceWithCache>();

        services.AddScoped<IEncryptionService, EncryptionService>();

        services.AddMemoryCache();

        return services;
    }
}
