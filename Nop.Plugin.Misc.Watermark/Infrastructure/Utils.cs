using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Nop.Core;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public static class Utils
    {
        public static void ClearThumbsDirectory()
        {
            string defaultThumbsPath = EngineContext.Current.Resolve<IWebHelper>().MapPath("~/content/images/thumbs");
            var imageDirectoryInfo = new DirectoryInfo(defaultThumbsPath);
            foreach (var fileInfo in imageDirectoryInfo.GetFiles())
                fileInfo.Delete();
        }

        public static byte[] ConvertImageToByteArray(Image image, ImageFormat imageFormat)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, imageFormat);
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
