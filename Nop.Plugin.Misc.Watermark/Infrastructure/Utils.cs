using System.IO;
using Microsoft.AspNetCore.Hosting;
using Nop.Core.Infrastructure;
using SixLabors.ImageSharp.PixelFormats;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public static class Utils
    {
        public static void ClearThumbsDirectory()
        {
            string defaultThumbsPath = Path.Combine(EngineContext.Current.Resolve<IHostingEnvironment>().
                WebRootPath, "images\\thumbs");
            var imageDirectoryInfo = new DirectoryInfo(defaultThumbsPath);
            foreach (var fileInfo in imageDirectoryInfo.GetFiles())
                fileInfo.Delete();
        }

        public static string ToRgb24Hex(this Rgba32 color) => color.ToHex().Substring(0, 6);
    }
}
