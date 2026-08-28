using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Playr.Application.Storage;

namespace Playr.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration, IHostEnvironment environment)
    {
        // FileStorage:RootPath lets deployments point uploads at a persistent directory
        // outside the publish/build output (which gets wiped on every `dotnet publish`).
        // Falls back to <content root>/wwwroot for local development, matching prior behavior.
        var configuredRoot = configuration["FileStorage:RootPath"];
        _rootPath = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : configuredRoot;
    }

    public async Task<SavedFile> SaveAsync(Stream content, string fileExtension, string subFolder, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_rootPath, "uploads", subFolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{fileExtension}";
        var absolutePath = Path.Combine(folder, fileName);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var relativeUrl = $"/uploads/{subFolder}/{fileName}".Replace('\\', '/');
        return new SavedFile(relativeUrl, absolutePath);
    }

    public void Delete(string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
            return;

        var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(_rootPath, relativePath);

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }
}
