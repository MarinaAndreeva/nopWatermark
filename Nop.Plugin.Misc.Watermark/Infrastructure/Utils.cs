using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public static class Utils
    {
        public static string ToRgb24Hex(this SKColor color) => color.ToString()[3..];
    }
}
