namespace TmrOverlay.App.Overlays.GarageCover;

internal static class GarageCoverImageStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".gif"
    };

    public static string ImportImage(string sourcePath, string settingsRoot)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected garage cover image does not exist.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Garage cover images must be PNG, JPG, BMP, or GIF files.");
        }

        var directory = ImageDirectory(settingsRoot);
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"cover{extension.ToLowerInvariant()}");
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            return destination;
        }

        var temporaryDestination = Path.Combine(directory, $"cover-import{extension.ToLowerInvariant()}");
        File.Copy(sourcePath, temporaryDestination, overwrite: true);
        foreach (var existing in Directory.EnumerateFiles(directory, "cover.*"))
        {
            File.Delete(existing);
        }

        File.Move(temporaryDestination, destination, overwrite: true);
        return destination;
    }

    public static bool IsSupportedImagePath(string? path)
    {
        return GetSupportedImageInfo(path) is not null;
    }

    public static void ClearImportedImages(string settingsRoot)
    {
        var directory = ImageDirectory(settingsRoot);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var existing in Directory.EnumerateFiles(directory, "cover.*"))
        {
            File.Delete(existing);
        }
    }

    public static GarageCoverImageInfo? GetSupportedImageInfo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !AllowedExtensions.Contains(Path.GetExtension(path))
            || !File.Exists(path))
        {
            return null;
        }

        var info = new FileInfo(path);
        return new GarageCoverImageInfo(info.LastWriteTimeUtc, info.Length);
    }

    private static string ImageDirectory(string settingsRoot)
    {
        return Path.Combine(settingsRoot, "garage-cover");
    }
}

internal sealed record GarageCoverImageInfo(DateTimeOffset LastWriteTimeUtc, long Length);
