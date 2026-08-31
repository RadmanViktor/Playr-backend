using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Playr.Application.Notifications;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;
using Playr.Infrastructure;

namespace Playr.IntegrationTests;

/// <summary>
/// Builds a service collection that mirrors what the web host provides before
/// <c>AddInfrastructure</c> runs. Without <see cref="IConfiguration"/> and
/// <see cref="IHostEnvironment"/> the container cannot construct
/// <c>LocalFileStorageService</c>, which several services depend on.
/// </summary>
internal static class InfrastructureTestServices
{
    public const string TestConnectionString =
        "Host=localhost;Database=playr;Username=playr;Password=playr_dev_password";

    public static ServiceCollection CreateWithInfrastructure()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddInfrastructure(configuration);
        // Notifiers live in the Api project (they need IHubContext<ChatHub>), so they
        // aren't registered by AddInfrastructure. IPostService/ICommentService now depend
        // on INotificationFeedService -> INotificationNotifier, so a no-op stand-in is
        // needed here for the container to resolve them at all.
        services.AddScoped<INotificationNotifier, NoOpNotificationNotifier>();
        // ProfileService now depends on IProfilePresenceNotifier -> IHubContext<ChatHub>, which
        // also lives in the Api project, so a no-op stand-in is needed here too.
        services.AddScoped<IProfilePresenceNotifier, NoOpProfilePresenceNotifier>();

        return services;
    }

    private sealed class NoOpNotificationNotifier : INotificationNotifier
    {
        public Task NotifyNotificationCreatedAsync(NotificationDto notification, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpProfilePresenceNotifier : IProfilePresenceNotifier
    {
        public Task NotifyStatusChangedAsync(Guid userId, ProfileStatus status, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Playr.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
