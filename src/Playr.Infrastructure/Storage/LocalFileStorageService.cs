using Playr.Application.Storage;

namespace Playr.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;

    public LocalFileStorageService()
    {
        _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<SavedFile> SaveAsync(Stream content, string fileExtension, string subFolder, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(_webRootPath, "uploads", subFolder);
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
        var absolutePath = Path.Combine(_webRootPath, relativePath);

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);
    }
}
