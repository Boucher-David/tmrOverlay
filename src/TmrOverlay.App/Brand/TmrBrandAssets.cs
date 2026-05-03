using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TmrOverlay.App.Brand;

internal static class TmrBrandAssets
{
    private const string LogoFileName = "TMRLogo.png";
    private const int IconSize = 256;

    public static Icon LoadIcon()
    {
        try
        {
            var logoPath = ResolveLogoPath();
            if (logoPath is null)
            {
                return (Icon)SystemIcons.Application.Clone();
            }

            using var source = Image.FromFile(logoPath);
            using var bitmap = new Bitmap(IconSize, IconSize);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var scale = Math.Min(IconSize / (double)source.Width, IconSize / (double)source.Height);
                var width = (int)Math.Round(source.Width * scale);
                var height = (int)Math.Round(source.Height * scale);
                var x = (IconSize - width) / 2;
                var y = (IconSize - height) / 2;
                graphics.DrawImage(source, new Rectangle(x, y, width, height));
            }

            var handle = bitmap.GetHicon();
            try
            {
                using var icon = Icon.FromHandle(handle);
                return (Icon)icon.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    private static string? ResolveLogoPath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Assets", LogoFileName);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        var repositoryPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "assets",
            "brand",
            LogoFileName));
        return File.Exists(repositoryPath) ? repositoryPath : null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
