using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Playr.Application.Email;
using Playr.Domain.Identity;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Email;
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

        // Required by the identity token providers used for email confirmation links.
        // Keys persist to ~/.aspnet/DataProtection-Keys so tokens survive a restart.
        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PlayrDbContext>()
            .AddDefaultTokenProviders();

        services.AddEmailSending(configuration);

        services.AddScoped<Playr.Application.Auth.JwtTokenGenerator>();
        services.AddScoped<Playr.Application.Auth.IAuthService, Playr.Infrastructure.Auth.AuthService>();
        services.AddScoped<Playr.Application.Profiles.IProfileService, Playr.Infrastructure.Profiles.ProfileService>();
        services.AddScoped<Playr.Application.Games.IGameService, Playr.Infrastructure.Games.GameService>();
        services.AddScoped<Playr.Application.Posts.IPostService, Playr.Infrastructure.Posts.PostService>();
        services.AddScoped<Playr.Application.Comments.ICommentService, Playr.Infrastructure.Comments.CommentService>();
        services.AddScoped<Playr.Application.Invitations.IInvitationService, Playr.Infrastructure.Invitations.InvitationService>();
        services.AddScoped<Playr.Application.Friends.IFriendService, Playr.Infrastructure.Friends.FriendService>();
        services.AddScoped<Playr.Application.Friends.IFriendRequestService, Playr.Infrastructure.Friends.FriendRequestService>();
        services.AddScoped<Playr.Application.Chat.IChatService, Playr.Infrastructure.Chat.ChatService>();
        services.AddScoped<Playr.Application.Notifications.INotificationPreferencesService, Playr.Infrastructure.Notifications.NotificationPreferencesService>();
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

    /// <summary>
    /// Registers the SMTP sender when <c>Email:Host</c> is configured, otherwise falls back
    /// to a sender that logs the message so development works without SMTP credentials.
    /// </summary>
    public static IServiceCollection AddEmailSending(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();

        if (emailOptions.IsConfigured)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
