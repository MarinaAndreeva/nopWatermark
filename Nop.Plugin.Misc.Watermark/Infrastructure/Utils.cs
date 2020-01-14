using SixLabors.ImageSharp.PixelFormats;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public static class Utils
    {
        public static string ToRgb24Hex(this Rgba32 color) => color.ToHex().Substring(0, 6);
    }
}
