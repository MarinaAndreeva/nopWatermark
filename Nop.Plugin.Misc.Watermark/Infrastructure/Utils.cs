using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;

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

        public static byte[] ConvertImageToByteArray(Image image, ImageFormat imageFormat, int jpegQuality)
        {
            using (var ms = new MemoryStream())
            {
                if (imageFormat.Equals(ImageFormat.Jpeg))
                {
                    ImageCodecInfo ici = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                    EncoderParameters ep = new EncoderParameters();
                    ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);
                    image.Save(ms, ici, ep);
                }
                else
                {
                    image.Save(ms, imageFormat);
                }
                return ms.ToArray();
            }
        }

        public static ImageFormat GetImageFormat(string imageExtension)
        {
            switch (imageExtension.ToLower())
            {
                case "bmp":
                    return ImageFormat.Bmp;

                case "gif":
                    return ImageFormat.Gif;

                case "ico":
                    return ImageFormat.Icon;

                case "jpg":
                case "jpeg":
                    return ImageFormat.Jpeg;

                case "png":
                    return ImageFormat.Png;

                case "tif":
                case "tiff":
                    return ImageFormat.Tiff;

                case "wmf":
                    return ImageFormat.Wmf;

                default:
                    return ImageFormat.Png;
            }
        }
    }
}
