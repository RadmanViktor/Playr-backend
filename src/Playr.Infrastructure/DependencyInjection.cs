using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Playr.Domain.Identity;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Steam;

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
        services.AddScoped<Playr.Application.Comments.ICommentService, Playr.Infrastructure.Comments.CommentService>();
        services.AddScoped<Playr.Application.Invitations.IInvitationService, Playr.Infrastructure.Invitations.InvitationService>();
        services.AddScoped<Playr.Application.Friends.IFriendService, Playr.Infrastructure.Friends.FriendService>();
        services.AddScoped<Playr.Application.Chat.IChatService, Playr.Infrastructure.Chat.ChatService>();
        services.AddSingleton<Playr.Application.Storage.IFileStorageService, Playr.Infrastructure.Storage.LocalFileStorageService>();

        services.Configure<SteamOptions>(configuration.GetSection(SteamOptions.SectionName));
        services.AddHttpClient(nameof(SteamOpenIdService));
        services.AddHttpClient<SteamApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com");
        });
        services.AddScoped<SteamOpenIdService>();
        services.AddScoped<Playr.Application.Steam.ISteamService, SteamService>();
        services.AddHostedService<SteamSyncBackgroundService>();

        return services;
    }
}
