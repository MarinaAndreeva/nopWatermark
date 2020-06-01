﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Caching;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Plugins;
using Nop.Services.Seo;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using FontStyle = SixLabors.Fonts.FontStyle;
using PointF = SixLabors.ImageSharp.PointF;
using Size = SixLabors.ImageSharp.Size;
using SizeF = SixLabors.ImageSharp.SizeF;
using SystemFonts = SixLabors.Fonts.SystemFonts;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkPictureService : Nop.Services.Media.PictureService, IDisposable
    {
        private readonly IRepository<ProductPicture> _productPictureRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Manufacturer> _manufacturerRepository;
        private readonly IPluginService _pluginService;
        private readonly INopFileProvider _fileProvider;
        private readonly CustomFonts _customFonts;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly Lazy<Image<Rgba32>> _watermarkImage;
        

        private bool IsPluginInstalled => _pluginService.GetPluginDescriptorBySystemName<WatermarkPlugin>("Misc.Watermark", LoadPluginsMode.InstalledOnly) != null;

        public MiscWatermarkPictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
            ICacheKeyService cacheKeyService,
            IEventPublisher eventPublisher,
            MediaSettings mediaSettings,
            INopDataProvider dataProvider,
            IStoreContext storeContext,
            INopFileProvider fileProvider,
            IProductAttributeParser productAttributeParser,
            IRepository<PictureBinary> pictureBinaryRepository,
            IUrlRecordService urlRecordService,
            IDownloadService downloadService,
            IHttpContextAccessor httpContextAccessor,
            IPluginService pluginService,
            CustomFonts customFonts)
            : base(dataProvider,
                downloadService,
                eventPublisher,
                httpContextAccessor,
                fileProvider,
                productAttributeParser,
                pictureRepository,
                pictureBinaryRepository,
                productPictureRepository,
                settingService,
                urlRecordService,
                webHelper,
                mediaSettings)
        {
            _categoryRepository = categoryRepository;
            _manufacturerRepository = manufacturerRepository;
            _productPictureRepository = productPictureRepository;
            _settingService = settingService;
            _fileProvider = fileProvider;

            _storeContext = storeContext;
            _pluginService = pluginService;
            _customFonts = customFonts;

            _watermarkImage = new Lazy<Image<Rgba32>>(() =>
            {
                int watermarkPictureId = GetSettings().PictureId;
                if (watermarkPictureId != 0)
                {
                    Picture picture = base.GetPictureById(watermarkPictureId);
                    byte[] pictureBinary = LoadPictureBinary(picture);
                    return Image.Load(pictureBinary);
                }

                return null;
            });
        }

        public virtual Task DeleteThumbs()
        {
            string defaultThumbsPath =
                _fileProvider.GetAbsolutePath(NopMediaDefaults.ImageThumbsPath);

            var imageDirectoryInfo = new DirectoryInfo(defaultThumbsPath);
            foreach (var fileInfo in imageDirectoryInfo.GetFiles())
                fileInfo.Delete();

            return Task.CompletedTask;
        }

        public override string GetPictureUrl(ref Picture picture,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            if (!IsPluginInstalled)
            {
                return base.GetPictureUrl(ref picture, targetSize, showDefaultPicture, storeLocation, defaultPictureType);
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

            int storeId = EngineContext.Current.Resolve<Nop.Core.IStoreContext>().CurrentStore.Id;
            string lastPart = GetFileExtensionFromMimeType(picture.MimeType);
            string thumbFileName;
            if (storeId == 1)
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
                                //resizing required
                                using (var originImage = Image.Load<Rgba32>(pictureBinary, out var imageFormat))
                                {
                                    originImage.Mutate(imageProcess => imageProcess.Resize(new ResizeOptions
                                    {
                                        Mode = ResizeMode.Max,
                                        Size = CalculateDimensions(originImage.Size(), targetSize)
                                    }));
                                    Image<Rgba32> image = MakeImageWatermark(originImage, picture.Id);
                                    pictureBinaryResized = EncodeImage(image, imageFormat);
                                }
                            }
                        }
                        else
                        {
                            using (var originImage = Image.Load<Rgba32>(pictureBinary, out var imageFormat))
                            {
                                Image<Rgba32> image = MakeImageWatermark(originImage, picture.Id);
                                pictureBinaryResized = EncodeImage(image, imageFormat);
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

        private Image<Rgba32> MakeImageWatermark(Image<Rgba32> sourceImage, int pictureId)
        {
            WatermarkSettings currentSettings = GetSettings();
            bool applyWatermark = IsWaterkmarkRequired(pictureId, currentSettings);

            if (applyWatermark &&
                ((sourceImage.Height > currentSettings.MinimumImageHeightForWatermark) ||
                 (sourceImage.Width > currentSettings.MinimumImageWidthForWatermark)))
            {
                if (currentSettings.WatermarkTextEnable && !String.IsNullOrEmpty(currentSettings.WatermarkText))
                {
                    PlaceTextWatermark(sourceImage, currentSettings);
                }
                
                if (currentSettings.WatermarkPictureEnable && _watermarkImage.Value != null)
                {
                    PlaceImageWatermark(sourceImage, _watermarkImage.Value, currentSettings);
                }
            }
            return sourceImage;
        }

        private static void PlaceImageWatermark(Image<Rgba32> destImage, Image<Rgba32> watermarkImage, WatermarkSettings currentSettings)
        {
            double watermarkSizeInPercent = (double)currentSettings.PictureSettings.Size / 100;
            Size boundingBoxSize = new Size((int)(destImage.Width * watermarkSizeInPercent),
                (int)(destImage.Height * watermarkSizeInPercent));
            Size calculatedWatermarkSize = ScaleRectangleToFitBounds(boundingBoxSize, watermarkImage.Size());
            if (calculatedWatermarkSize.Width == 0 || calculatedWatermarkSize.Height == 0)
            {
                return;
            }

            Size watermarkSize = new Size((int)(calculatedWatermarkSize.Width), (int)(calculatedWatermarkSize.Height));
            watermarkImage.Mutate(w => w.Resize(watermarkSize));

            foreach (var position in currentSettings.PictureSettings.PositionList)
            {
                Point watermarkPosition = CalculateWatermarkPosition(position, destImage.Size(), calculatedWatermarkSize);
                destImage.Mutate(d => d.DrawImage(watermarkImage, watermarkPosition, (float)currentSettings.PictureSettings.Opacity));
            }
        }

        private void PlaceTextWatermark(Image<Rgba32> sourceBitmap, WatermarkSettings currentSettings)
        {
            string text = currentSettings.WatermarkText;
            int textAngle = currentSettings.TextRotatedDegree;
            double sizeFactor = (double)currentSettings.TextSettings.Size / 100;
            Size maxTextSize = new Size(
                (int)(sourceBitmap.Width * sizeFactor),
                (int)(sourceBitmap.Height * sizeFactor));

            int fontSize = ComputeMaxFontSize(currentSettings, text, textAngle, maxTextSize);
            Font font = CreateFont(currentSettings, (float)fontSize);
            FontRectangle originalTextSize = TextMeasurer.Measure(text, new RendererOptions(font));

            using (var textImage = new Image<Rgba32>((int) originalTextSize.Width, (int) originalTextSize.Height))
            {
                Rgba32.TryParseHex(currentSettings.TextColor, out var color);
                color.A = (byte)(currentSettings.TextSettings.Opacity * 255);
                textImage.Mutate<Rgba32>(i =>
                    i
                        //.DrawText(new TextGraphicsOptions(true), text, font, color, new PointF(0, 0))
                        .DrawText(new TextGraphicsOptions(), text, font, color, new PointF(0, 0))
                        .Rotate(textAngle)
                );

                foreach (var position in currentSettings.TextSettings.PositionList)
                {
                    Point textPosition = CalculateWatermarkPosition(position, sourceBitmap.Size(), textImage.Size());
                    sourceBitmap.Mutate(s => s.DrawImage(textImage, textPosition, 1));
                }
            }
        }

        private int ComputeMaxFontSize(WatermarkSettings settings, string text, int angle, Size maxTextSize)
        {
            for (int fontSize = 2; ; fontSize++)
            {
                Font tmpFont = CreateFont(settings, fontSize);
                var textSize = TextMeasurer.Measure(text, new RendererOptions(tmpFont));
                SizeF rotatedTextSize = CalculateRotatedRectSize(textSize, angle);
                if (((int)rotatedTextSize.Width > maxTextSize.Width) ||
                    ((int)rotatedTextSize.Height > maxTextSize.Height))
                {
                    return fontSize - 1;
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

        private static SizeF CalculateRotatedRectSize(FontRectangle rectSize, double angleDeg)
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

        private Font CreateFont(WatermarkSettings settings, float fontSize, FontStyle fontStyle = FontStyle.Bold)
        {
            if (settings.WatermarkFont.Contains(_customFonts.CustomFontPrefix))
            {
                string fontNameWithoutPrefix =
                    settings.WatermarkFont.Substring(_customFonts.CustomFontPrefix.Length);
                return _customFonts.FontCollection().CreateFont(fontNameWithoutPrefix, fontSize, fontStyle);
            }
            else
            {
                return SystemFonts.CreateFont(settings.WatermarkFont, fontSize, fontStyle);
            }
        }

        #region IDisposable

        private void ReleaseUnmanagedResources()
        {
            if (_watermarkImage.IsValueCreated)
            {
                _watermarkImage.Value.Dispose();
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
