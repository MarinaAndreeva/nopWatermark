using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using ImageResizer;
using Microsoft.AspNetCore.Hosting;
using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Plugins;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkPictureService : Nop.Services.Media.PictureService, IDisposable
    {
        private readonly IRepository<ProductPicture> _productPictureRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Manufacturer> _manufacturerRepository;
        private readonly MediaSettings _mediaSettings;
        private readonly IPluginFinder _pluginFinder;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly Lazy<Bitmap> _watermarkBitmap;

        private bool IsPluginInstalled
        {
            get { return _pluginFinder.GetPluginDescriptorBySystemName("Misc.Watermark") != null; }
        }

        public MiscWatermarkPictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository, 
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
            ILogger logger,
            IDbContext dbContext,
            IEventPublisher eventPublisher,
            MediaSettings mediaSettings,
            IDataProvider dataProvider,
            IStoreContext storeContext,
            IPluginFinder pluginFinder,
            IHostingEnvironment hostingEnvironment)
            : base(pictureRepository,
                productPictureRepository,
                settingService,
                webHelper,
                logger,
                dbContext,
                eventPublisher,
                mediaSettings,
                dataProvider,
                hostingEnvironment)
        {
            _categoryRepository = categoryRepository;
            _manufacturerRepository = manufacturerRepository;
            _productPictureRepository = productPictureRepository;
            _settingService = settingService;
            _logger = logger;
            _mediaSettings = mediaSettings;

            _storeContext = storeContext;
            _pluginFinder = pluginFinder;

            _watermarkBitmap = new Lazy<Bitmap>(() =>
            {
                if (IsPluginInstalled)
                {
                    int watermarkPictureId = GetSettings().PictureId;
                    if (watermarkPictureId != 0)
                    {
                        Picture picture = base.GetPictureById(watermarkPictureId);
                        using (MemoryStream ms = new MemoryStream(picture.PictureBinary))
                        {
                            return new Bitmap(ms);
                        }
                    }
                }
                return null;
            });
        }

        public override string GetPictureUrl(Picture picture,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            if (!IsPluginInstalled)
            {
                return base.GetPictureUrl(picture, targetSize, showDefaultPicture, storeLocation, defaultPictureType);
            }

            string url = string.Empty;
            byte[] pictureBinary = null;
            if (picture != null)
                pictureBinary = LoadPictureBinary(picture);
            if (picture == null || pictureBinary == null || pictureBinary.Length == 0)
            {
                if (showDefaultPicture)
                {
                    url = GetDefaultPictureUrl(targetSize, defaultPictureType, storeLocation);
                }
                return url;
            }

            if (picture.IsNew)
            {
                DeletePictureThumbs(picture);

                //we do not validate picture binary here to ensure that no exception ("Parameter is not valid") will be thrown
                picture = UpdatePicture(picture.Id,
                    pictureBinary,
                    picture.MimeType,
                    picture.SeoFilename,
                    picture.AltAttribute,
                    picture.TitleAttribute,
                    false,
                    false);
            }

            var seoFileName = picture.SeoFilename;

            int storeId = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Core.IStoreContext>().CurrentStore
                .Id;
            string lastPart = GetFileExtensionFromMimeType(picture.MimeType);
            string thumbFileName;
            if (storeId == 0)
            {
                if (targetSize == 0)
                {
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}.{lastPart}"
                        : $"{picture.Id:0000000}.{lastPart}";
                }
                else
                {
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}.{lastPart}";
                }
            }
            else
            {
                if (targetSize == 0)
                {
                    thumbFileName = !String.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{storeId}.{lastPart}";
                }
                else
                {
                    thumbFileName = !String.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}_{storeId}.{lastPart}";
                }
            }
            
            string thumbFilePath = GetThumbLocalPath(thumbFileName);

            //the named mutex helps to avoid creating the same files in different threads,
            //and does not decrease performance significantly, because the code is blocked only for the specific file.
            using (var mutex = new Mutex(false, thumbFileName))
            {
                if (!GeneratedThumbExists(thumbFilePath, thumbFileName))
                {
                    mutex.WaitOne();

                    //check, if the file was created, while we were waiting for the release of the mutex.
                    if (!GeneratedThumbExists(thumbFilePath, thumbFileName))
                    {
                        byte[] pictureBinaryResized;

                        //resizing required
                        if (targetSize != 0)
                        {
                            using (var stream = new MemoryStream(pictureBinary))
                            {
                                Bitmap b = null;
                                try
                                {
                                    //try-catch to ensure that picture binary is really OK. Otherwise, we can get "Parameter is not valid" exception if binary is corrupted for some reasons
                                    b = new Bitmap(stream);
                                }
                                catch (ArgumentException exc)
                                {
                                    _logger.Error(string.Format("Error generating picture thumb. ID={0}", picture.Id),
                                        exc);
                                }

                                if (b == null)
                                {
                                    //bitmap could not be loaded for some reasons
                                    return url;
                                }
                                
                                var newSize = CalculateDimensions(b.Size, targetSize);
                                ImageFormat sourceImageFormat = b.RawFormat;
                                Bitmap resizedBitmap = ImageBuilder.Current.Build(b, new ResizeSettings
                                {
                                    Width = newSize.Width,
                                    Height = newSize.Height,
                                    Scale = ScaleMode.Both
                                });
                                Image image = MakeImageWatermark(resizedBitmap, picture.Id);
                                pictureBinaryResized = Utils.ConvertImageToByteArray(image, sourceImageFormat, _mediaSettings.DefaultImageQuality);
                                b.Dispose();
                                resizedBitmap.Dispose();
                                image.Dispose();
                            }
                        }
                        else
                        {
                            using (var stream = new MemoryStream(pictureBinary))
                            {
                                Image sourceImage = Image.FromStream(stream);
                                ImageFormat sourceImageFormat = sourceImage.RawFormat;
                                Image image = MakeImageWatermark(sourceImage, picture.Id);
                                pictureBinaryResized = Utils.ConvertImageToByteArray(image, sourceImageFormat, _mediaSettings.DefaultImageQuality);
                                image.Dispose();
                            }
                        }

                        SaveThumb(thumbFilePath, thumbFileName, picture.MimeType, pictureBinaryResized);
                    }

                    mutex.ReleaseMutex();
                }
            }
            url = GetThumbUrl(thumbFileName, storeLocation);
            return url;
        }

        private Image MakeImageWatermark(Image sourceImage, int pictureId)
        {
            WatermarkSettings currentSettings = GetSettings();
            bool applyWatermark = IsWaterkmarkRequired(pictureId, currentSettings);

            if (applyWatermark &&
                ((sourceImage.Height > currentSettings.MinimumImageHeightForWatermark) ||
                 (sourceImage.Width > currentSettings.MinimumImageWidthForWatermark)))
            {
                Bitmap destBitmap = CreateBitmap(sourceImage);

                if (currentSettings.WatermarkTextEnable && !String.IsNullOrEmpty(currentSettings.WatermarkText))
                {
                    PlaceTextWatermark(destBitmap, currentSettings);
                }
                
                if (currentSettings.WatermarkPictureEnable && _watermarkBitmap.Value != null)
                {
                    PlaceImageWatermark(destBitmap, _watermarkBitmap.Value, currentSettings);
                }

                sourceImage.Dispose();
                return destBitmap;
            }
            return sourceImage;
        }

        private static void PlaceImageWatermark(Bitmap destBitmap, Bitmap watermarkBitmap, WatermarkSettings currentSettings)
        {
            using (Graphics g = Graphics.FromImage(destBitmap))
            {
                double watermarkSizeInPercent = (double) currentSettings.PictureSettings.Size / 100;

                Size boundingBoxSize = new Size((int) (destBitmap.Width * watermarkSizeInPercent),
                    (int) (destBitmap.Height * watermarkSizeInPercent));
                Size calculatedWatermarkSize = ScaleRectangleToFitBounds(boundingBoxSize, watermarkBitmap.Size);

                if (calculatedWatermarkSize.Width == 0 || calculatedWatermarkSize.Height == 0)
                {
                    return;
                }

                Bitmap scaledWatermarkBitmap =
                    new Bitmap(calculatedWatermarkSize.Width, calculatedWatermarkSize.Height);
                using (Graphics watermarkGraphics = Graphics.FromImage(scaledWatermarkBitmap))
                {
                    ColorMatrix opacityMatrix = new ColorMatrix
                    {
                        Matrix33 = (float) currentSettings.PictureSettings.Opacity
                    };
                    ImageAttributes attrs = new ImageAttributes();
                    attrs.SetColorMatrix(opacityMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    watermarkGraphics.DrawImage(watermarkBitmap,
                        new Rectangle(0, 0, scaledWatermarkBitmap.Width, scaledWatermarkBitmap.Height),
                        0, 0, watermarkBitmap.Width, watermarkBitmap.Height,
                        GraphicsUnit.Pixel, attrs);
                    attrs.Dispose();
                }
                
                foreach (var position in currentSettings.PictureSettings.PositionList)
                {
                    Point watermarkPosition = CalculateWatermarkPosition(position,
                        destBitmap.Size, calculatedWatermarkSize);

                    g.DrawImage(scaledWatermarkBitmap,
                        new Rectangle(watermarkPosition, calculatedWatermarkSize),
                        0, 0, calculatedWatermarkSize.Width, calculatedWatermarkSize.Height, GraphicsUnit.Pixel);
                }
                scaledWatermarkBitmap.Dispose();
            }
        }

        private void PlaceTextWatermark(Bitmap sourceBitmap, WatermarkSettings currentSettings)
        {
            using (Graphics g = Graphics.FromImage(sourceBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                string text = currentSettings.WatermarkText;

                int textAngle = currentSettings.TextRotatedDegree;
                double sizeFactor = (double)currentSettings.TextSettings.Size / 100;
                Size maxTextSize = new Size(
                    (int)(sourceBitmap.Width * sizeFactor),
                    (int)(sourceBitmap.Height * sizeFactor));
                
                int fontSize = ComputeMaxFontSize(text, textAngle, currentSettings.WatermarkFont, maxTextSize, g);
                
                Font font = new Font(currentSettings.WatermarkFont, (float)fontSize, FontStyle.Bold);
                SizeF originalTextSize = g.MeasureString(text, font);
                SizeF rotatedTextSize = CalculateRotatedRectSize(originalTextSize, textAngle);
                
                Bitmap textBitmap = new Bitmap((int)rotatedTextSize.Width, (int)rotatedTextSize.Height,
                    PixelFormat.Format32bppArgb);
                using (Graphics textG = Graphics.FromImage(textBitmap))
                {
                    Color color = Color.FromArgb((int)(currentSettings.TextSettings.Opacity * 255), currentSettings.TextColor);
                    SolidBrush brush = new SolidBrush(color);

                    textG.TranslateTransform(rotatedTextSize.Width / 2, rotatedTextSize.Height / 2);
                    textG.RotateTransform((float)textAngle);
                    textG.DrawString(text, font, brush, -originalTextSize.Width / 2,
                        -originalTextSize.Height / 2);
                    textG.ResetTransform();

                    brush.Dispose();
                }

                foreach (var position in currentSettings.TextSettings.PositionList)
                {
                    Point textPosition = CalculateWatermarkPosition(position,
                        sourceBitmap.Size, rotatedTextSize.ToSize());
                    g.DrawImage(textBitmap, textPosition);
                }
                textBitmap.Dispose();
                font.Dispose();
            }
        }

        private int ComputeMaxFontSize(string text, int angle, string fontName, Size maxTextSize, Graphics g)
        {
            for (int fontSize = 2; ; fontSize++)
            {
                using (Font tmpFont = new Font(fontName, fontSize, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(text, tmpFont);
                    SizeF rotatedTextSize = CalculateRotatedRectSize(textSize, angle);
                    if (((int)rotatedTextSize.Width > maxTextSize.Width) ||
                        ((int)rotatedTextSize.Height > maxTextSize.Height))
                    {
                        return fontSize - 1;
                    }
                }
            }
        }

        private bool IsWaterkmarkRequired(int pictureId, WatermarkSettings settings)
        {
            if (settings.ApplyOnProductPictures && _productPictureRepository.Table.Any(product => product.PictureId == pictureId))
            {
                return true;
            }
            if (settings.ApplyOnCategoryPictures && _categoryRepository.Table.Any(category => category.PictureId == pictureId))
            {
                return true;
            }
            if (settings.ApplyOnManufacturerPictures && _manufacturerRepository.Table.Any(manufacturer => manufacturer.PictureId == pictureId))
            {
                return true;
            }
            return false;
        }

        private WatermarkSettings GetSettings()
        {
            return _settingService.LoadSetting<WatermarkSettings>(_storeContext.CurrentStore.Id);
        }
        
        private static Bitmap CreateBitmap(Image sourceImage)
        {
            Bitmap destBitmap = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
            destBitmap.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);
            using (Graphics g = Graphics.FromImage(destBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(sourceImage, 0, 0);
            }
            return destBitmap;
        }

        private static Size ScaleRectangleToFitBounds(Size bounds, Size rect)
        {
            if (rect.Width < bounds.Width && rect.Height < bounds.Height)
            {
                return rect;
            }

            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return new Size(0, 0);
            }

            double scaleFactorWidth = (double)rect.Width / bounds.Width;
            double scaleFactorHeight = (double)rect.Height / bounds.Height;

            double scaleFactor = Math.Max(scaleFactorWidth, scaleFactorHeight);
            return new Size()
            {
                Width = (int)(rect.Width / scaleFactor),
                Height = (int)(rect.Height / scaleFactor)
            };
        }

        private static SizeF CalculateRotatedRectSize(SizeF rectSize, double angleDeg)
        {
            double angleRad = angleDeg * Math.PI / 180;
            double width = rectSize.Height * Math.Abs(Math.Sin(angleRad)) + 
                rectSize.Width * Math.Abs(Math.Cos(angleRad));
            double height = rectSize.Height * Math.Abs(Math.Cos(angleRad)) +
                rectSize.Width * Math.Abs(Math.Sin(angleRad));
            return new SizeF((float) width, (float) height);
        }

        private static Point CalculateWatermarkPosition(WatermarkPosition watermarkPosition, Size imageSize, Size watermarkSize)
        {
            Point position = new Point();
            switch (watermarkPosition)
            {
                case WatermarkPosition.TopLeftCorner:
                    position.X = 0;
                    position.Y = 0;
                    break;
                case WatermarkPosition.TopCenter:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = 0;
                    break;
                case WatermarkPosition.TopRightCorner:
                    position.X = imageSize.Width - watermarkSize.Width;
                    position.Y = 0;
                    break;
                case WatermarkPosition.CenterLeft:
                    position.X = 0;
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.Center:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.CenterRight:
                    position.X = imageSize.Width - watermarkSize.Width;
                    position.Y = (imageSize.Height / 2) - (watermarkSize.Height / 2);
                    break;
                case WatermarkPosition.BottomLeftCorner:
                    position.X = 0;
                    position.Y = imageSize.Height - watermarkSize.Height;
                    break;
                case WatermarkPosition.BottomCenter:
                    position.X = (imageSize.Width / 2) - (watermarkSize.Width / 2);
                    position.Y = imageSize.Height - watermarkSize.Height;
                    break;
                case WatermarkPosition.BottomRightCorner:
                    position.X = imageSize.Width - watermarkSize.Width;
                    position.Y = imageSize.Height - watermarkSize.Height;
                    break;
            }
            return position;
        }

        #region IDisposable

        private void ReleaseUnmanagedResources()
        {
            if (_watermarkBitmap.IsValueCreated)
            {
                _watermarkBitmap.Value.Dispose();
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~MiscWatermarkPictureService()
        {
            ReleaseUnmanagedResources();
        }

        #endregion
    }
}