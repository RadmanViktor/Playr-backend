using Playr.Application.Storage;

namespace Playr.Application.Tests.Posts;

public sealed class NoOpFileStorageService : IFileStorageService
{
    public Task<SavedFile> SaveAsync(Stream content, string fileExtension, string subFolder, CancellationToken cancellationToken)
        => Task.FromResult(new SavedFile($"/uploads/{subFolder}/fake{fileExtension}", $"fake{fileExtension}"));

    public void Delete(string relativeUrl)
    {
    }
}
