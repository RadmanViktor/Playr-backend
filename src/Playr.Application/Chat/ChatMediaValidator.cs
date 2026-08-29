using Playr.Domain.Chat;

namespace Playr.Application.Chat;

public static class ChatMediaValidator
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly Dictionary<string, ChatMediaType> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ChatMediaType.Image,
        [".jpeg"] = ChatMediaType.Image,
        [".png"] = ChatMediaType.Image,
        [".webp"] = ChatMediaType.Image,
        [".gif"] = ChatMediaType.Image,
    };

    private static readonly Dictionary<string, ChatMediaType> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = ChatMediaType.Video,
        [".webm"] = ChatMediaType.Video,
        [".mov"] = ChatMediaType.Video,
    };

    public static (ChatMediaType MediaType, string Extension) Validate(ChatMediaInput media)
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
