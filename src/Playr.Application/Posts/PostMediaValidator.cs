using Playr.Domain.Posts;

namespace Playr.Application.Posts;

public static class PostMediaValidator
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly Dictionary<string, PostMediaType> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = PostMediaType.Image,
        [".jpeg"] = PostMediaType.Image,
        [".png"] = PostMediaType.Image,
        [".webp"] = PostMediaType.Image,
        [".gif"] = PostMediaType.Image,
    };

    private static readonly Dictionary<string, PostMediaType> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = PostMediaType.Video,
        [".webm"] = PostMediaType.Video,
        [".mov"] = PostMediaType.Video,
    };

    public static (PostMediaType MediaType, string Extension) Validate(PostMediaInput media)
    {
        var extension = Path.GetExtension(media.FileName);
        if (string.IsNullOrEmpty(extension))
            throw new InvalidOperationException("Uploaded file must have a valid file extension.");

        if (ImageExtensions.TryGetValue(extension, out var imageType))
        {
            if (media.Length > MaxImageBytes)
                throw new InvalidOperationException("Images cannot be larger than 10 MB.");
            return (imageType, extension.ToLowerInvariant());
        }

        if (VideoExtensions.TryGetValue(extension, out var videoType))
        {
            if (media.Length > MaxVideoBytes)
                throw new InvalidOperationException("Videos cannot be larger than 100 MB.");
            return (videoType, extension.ToLowerInvariant());
        }

        throw new InvalidOperationException("Unsupported file type. Allowed: jpg, jpeg, png, webp, gif, mp4, webm, mov.");
    }
}
