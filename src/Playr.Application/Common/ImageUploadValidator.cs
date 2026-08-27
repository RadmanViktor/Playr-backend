namespace Playr.Application.Common;

public sealed record FileUploadInput(Stream Content, string FileName, string ContentType, long Length);

public static class ImageUploadValidator
{
    private const long MaxImageBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
    };

    public static string Validate(FileUploadInput file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !ImageExtensions.Contains(extension))
            throw new InvalidOperationException("Unsupported file type. Allowed: jpg, jpeg, png, webp, gif.");

        if (file.Length > MaxImageBytes)
            throw new InvalidOperationException("Images cannot be larger than 10 MB.");

        return extension.ToLowerInvariant();
    }
}
