namespace Playr.Application.Storage;

public sealed record SavedFile(string RelativeUrl, string AbsolutePath);

public interface IFileStorageService
{
    Task<SavedFile> SaveAsync(Stream content, string fileExtension, string subFolder, CancellationToken cancellationToken);
    void Delete(string relativeUrl);
}
