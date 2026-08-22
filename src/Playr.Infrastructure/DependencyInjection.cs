using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Playr.Domain.Identity;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson(jsonbClrTypes: new[] { typeof(Dictionary<string, string>) });
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<PlayrDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PlayrDbContext>();

        services.AddScoped<Playr.Application.Auth.JwtTokenGenerator>();
        services.AddScoped<Playr.Application.Auth.IAuthService, Playr.Infrastructure.Auth.AuthService>();
        services.AddScoped<Playr.Application.Profiles.IProfileService, Playr.Infrastructure.Profiles.ProfileService>();
        services.AddScoped<Playr.Application.Games.IGameService, Playr.Infrastructure.Games.GameService>();
        services.AddScoped<Playr.Application.Posts.IPostService, Playr.Infrastructure.Posts.PostService>();

        return services;
    }
}
