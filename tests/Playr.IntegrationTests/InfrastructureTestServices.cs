using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

        return services;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Playr.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
