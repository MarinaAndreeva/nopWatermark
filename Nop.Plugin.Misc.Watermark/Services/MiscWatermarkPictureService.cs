using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nito.AsyncEx;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Services.Plugins;
using Nop.Services.Seo;
using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkPictureService : PictureService, IDisposable
    {
        private readonly IRepository<ProductPicture> _productPictureRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Manufacturer> _manufacturerRepository;
        private readonly IPluginService _pluginService;
        private readonly INopFileProvider _fileProvider;
        private readonly FontProvider _fontProvider;
        private readonly ISettingService _settingService;
        private readonly MediaSettings _mediaSettings;
        private readonly IStoreContext _storeContext;
        private readonly AsyncLazy<SKImage> _watermarkImage;
        

        private bool IsPluginInstalled => _pluginService.GetPluginDescriptorBySystemNameAsync<WatermarkPlugin>("Misc.Watermark") != null;

        public MiscWatermarkPictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
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
            FontProvider fontProvider)
            : base(dataProvider,
                downloadService,
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
            _mediaSettings = mediaSettings;
            _fileProvider = fileProvider;

            _storeContext = storeContext;
            _pluginService = pluginService;
            _fontProvider = fontProvider;

            _watermarkImage = new AsyncLazy<SKImage>(async () =>
            {
                var watermarkPictureId = (await GetSettingsAsync()).PictureId;
                if (watermarkPictureId == 0)
                    return null;

                var picture = await base.GetPictureByIdAsync(watermarkPictureId);
                var pictureBinary = await LoadPictureBinaryAsync(picture);
                return SKImage.FromEncodedData(pictureBinary);
            });
        }

        public virtual Task DeleteThumbs()
        {
            var defaultThumbsPath =
                _fileProvider.GetAbsolutePath(NopMediaDefaults.ImageThumbsPath);

            var imageDirectoryInfo = new DirectoryInfo(defaultThumbsPath);
            foreach (var fileInfo in imageDirectoryInfo.GetFiles())
                fileInfo.Delete();

            return Task.CompletedTask;
        }

        public override async Task<(string Url, Picture Picture)> GetPictureUrlAsync(Picture picture,
            int targetSize = 0,
            bool showDefaultPicture = true,
            string storeLocation = null,
            PictureType defaultPictureType = PictureType.Entity)
        {
            if (!IsPluginInstalled)
                return await base.GetPictureUrlAsync(picture, targetSize, showDefaultPicture, storeLocation, defaultPictureType);

            if (picture == null)
                return showDefaultPicture ? (await GetDefaultPictureUrlAsync(targetSize, defaultPictureType, storeLocation), null) : (string.Empty, (Picture)null);

            byte[] pictureBinary = null;
            if (picture.IsNew)
            {
                await DeletePictureThumbsAsync(picture);
                pictureBinary = await LoadPictureBinaryAsync(picture);

                if ((pictureBinary?.Length ?? 0) == 0)
                    return showDefaultPicture ? (await GetDefaultPictureUrlAsync(targetSize, defaultPictureType, storeLocation), picture) : (string.Empty, picture);

                //we do not validate picture binary here to ensure that no exception ("Parameter is not valid") will be thrown
                picture = await UpdatePictureAsync(picture.Id,
                    pictureBinary,
                    picture.MimeType,
                    picture.SeoFilename,
                    picture.AltAttribute,
                    picture.TitleAttribute,
                    false,
                    false);
            }

            var seoFileName = picture.SeoFilename;

            var storeId = (await EngineContext.Current.Resolve<IStoreContext>().GetCurrentStoreAsync()).Id;
            var lastPart = await GetFileExtensionFromMimeTypeAsync(picture.MimeType);
            string thumbFileName;
            if (storeId == 1)
            {
                if (targetSize == 0)
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}.{lastPart}"
                        : $"{picture.Id:0000000}.{lastPart}";
                else
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}.{lastPart}";
            }
            else
            {
                if (targetSize == 0)
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{storeId}.{lastPart}";
                else
                    thumbFileName = !string.IsNullOrEmpty(seoFileName)
                        ? $"{picture.Id:0000000}_{seoFileName}_{targetSize}_{storeId}.{lastPart}"
                        : $"{picture.Id:0000000}_{targetSize}_{storeId}.{lastPart}";
            }
            
            var thumbFilePath = await GetThumbLocalPathAsync(thumbFileName);

            if (await GeneratedThumbExistsAsync(thumbFilePath, thumbFileName))
                return (await GetThumbUrlAsync(thumbFileName, storeLocation), picture);

            pictureBinary ??= await LoadPictureBinaryAsync(picture);

            //the named mutex helps to avoid creating the same files in different threads,
            //and does not decrease performance significantly, because the code is blocked only for the specific file.
            //you should be very careful, mutexes cannot be used in with the await operation
            //we can't use semaphore here, because it produces PlatformNotSupportedException exception on UNIX based systems
            using var mutex = new Mutex(false, thumbFileName);
            mutex.WaitOne();
            try
            {
                using var inputImage = SKBitmap.Decode(pictureBinary);
                SKBitmap outputImage = inputImage;

                if (targetSize != 0) //resizing required
                    try
                    {
                        var newSize =
                            ScaleRectangleToFitBounds(new SKSizeI(targetSize, targetSize), inputImage.Info.Size);
                        outputImage = inputImage.Resize(newSize, SKFilterQuality.Medium);
                    }
                    catch
                    {
                        // ignored
                    }

                MakeImageWatermarkAsync(outputImage, picture.Id).Wait();

                var format = GetImageFormatByMimeType(picture.MimeType);
                pictureBinary = outputImage.Encode(format,
                    _mediaSettings.DefaultImageQuality > 0 ? _mediaSettings.DefaultImageQuality : 80).ToArray();

                outputImage.Dispose();

                SaveThumbAsync(thumbFilePath, thumbFileName, string.Empty, pictureBinary).Wait();
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return (await GetThumbUrlAsync(thumbFileName, storeLocation), picture);
        }

        private async Task MakeImageWatermarkAsync(SKBitmap sourceImage, int pictureId)
        {
            var currentSettings = await GetSettingsAsync();
            var applyWatermark = IsWatermarkRequired(pictureId, currentSettings);

            if (!applyWatermark || ((sourceImage.Height <= currentSettings.MinimumImageHeightForWatermark) &&
                                    (sourceImage.Width <= currentSettings.MinimumImageWidthForWatermark)))
                return;
            
            if (currentSettings.WatermarkTextEnable && !string.IsNullOrEmpty(currentSettings.WatermarkText))
                PlaceTextWatermark(sourceImage, currentSettings);

            var watermarkImage = await _watermarkImage.Task;
            if (currentSettings.WatermarkPictureEnable && watermarkImage != null)
                PlaceImageWatermark(sourceImage, watermarkImage, currentSettings);
        }

        private static void PlaceImageWatermark(SKBitmap destImage, SKImage watermarkImage,
            WatermarkSettings currentSettings)
        {
            var watermarkSizeInPercent = (double)currentSettings.PictureSettings.Size / 100;
            var boundingBoxSize = new SKSizeI((int)(destImage.Width * watermarkSizeInPercent),
                (int)(destImage.Height * watermarkSizeInPercent));
            var calculatedWatermarkSize =
                ScaleRectangleToFitBounds(boundingBoxSize, new SKSizeI(watermarkImage.Width, watermarkImage.Height));
            if (calculatedWatermarkSize.Width == 0 || calculatedWatermarkSize.Height == 0)
                return;

            var alpha = (byte)(currentSettings.PictureSettings.Opacity * 255);
            using var paint = new SKPaint
            {
                BlendMode = SKBlendMode.SrcOver,
                Color = SKColors.White.WithAlpha(alpha),
                FilterQuality = SKFilterQuality.High
            };

            using var canvas = new SKCanvas(destImage);
            foreach (var watermarkPosition in currentSettings.PictureSettings.PositionList.Select(position =>
                     CalculateWatermarkPosition(position, destImage.Info.Size, calculatedWatermarkSize)))
                canvas.DrawImage(watermarkImage, SKRectI.Create(watermarkPosition, calculatedWatermarkSize),
                    paint);
        }

        private void PlaceTextWatermark(SKBitmap sourceBitmap, WatermarkSettings currentSettings)
        {
            var text = currentSettings.WatermarkText;
            var textAngle = currentSettings.TextRotatedDegree;
            var sizeFactor = (double)currentSettings.TextSettings.Size / 100;
            var maxTextSize = new SKSizeI(
                (int)(sourceBitmap.Width * sizeFactor),
                (int)(sourceBitmap.Height * sizeFactor));

            var color = SKColor.Parse(currentSettings.TextColor);
            color = color.WithAlpha((byte)(currentSettings.TextSettings.Opacity * 255));

            var typeface = GetFontTypeface(currentSettings);
            var fontSize = ComputeMaxFontSize(typeface, text, textAngle, maxTextSize, out var rotatedTextSize);

            using var paint = new SKPaint
            {
                Color = color,
                Typeface = typeface,
                TextSize = fontSize,
                TextAlign = SKTextAlign.Center,
                IsAntialias = true,
            };

            var horizontalTextRect = new SKRect();
            paint.MeasureText(text, ref horizontalTextRect);

            using var canvas = new SKCanvas(sourceBitmap);
            foreach (var textPosition in currentSettings.TextSettings.PositionList.Select(position =>
                     CalculateWatermarkPosition(position, sourceBitmap.Info.Size, rotatedTextSize)))
            {
                textPosition.Offset(rotatedTextSize.Width / 2, rotatedTextSize.Height / 2);

                canvas.Save();
                canvas.Translate(textPosition);
                canvas.RotateDegrees(textAngle);
                canvas.DrawText(text, 0, -horizontalTextRect.MidY, paint);
                canvas.Restore();
            }
        }

        private static int ComputeMaxFontSize(SKTypeface typeface, string text, int angle, SKSizeI bounds,
            out SKSizeI actualRotatedTextSize)
        {
            actualRotatedTextSize = new SKSizeI();
            using var paint = new SKPaint {Typeface = typeface};
            for (var fontSize = 2;; fontSize++)
            {
                paint.TextSize = fontSize;
                var textRect = new SKRect();
                paint.MeasureText(text, ref textRect);
                var rotatedTextSize = CalculateRotatedRectSize(textRect.Size, angle);
                if ((rotatedTextSize.Width > bounds.Width) ||
                    (rotatedTextSize.Height > bounds.Height))
                    return fontSize - 1;

                actualRotatedTextSize = rotatedTextSize.ToSizeI();
            }
        }

        private bool IsWatermarkRequired(int pictureId, WatermarkSettings settings)
        {
            if (settings.ApplyOnProductPictures && _productPictureRepository.Table.Any(product => product.PictureId == pictureId)) 
                return true;
            
            if (settings.ApplyOnCategoryPictures && _categoryRepository.Table.Any(category => category.PictureId == pictureId))
                return true;

            return settings.ApplyOnManufacturerPictures &&
                   _manufacturerRepository.Table.Any(manufacturer => manufacturer.PictureId == pictureId);
        }

        private async Task<WatermarkSettings> GetSettingsAsync()
        {
            var currentStore = await _storeContext.GetCurrentStoreAsync();
            return await _settingService.LoadSettingAsync<WatermarkSettings>(currentStore.Id);
        }

        private static SKSizeI ScaleRectangleToFitBounds(SKSizeI bounds, SKSizeI rect)
        {
            if (rect.Width < bounds.Width && rect.Height < bounds.Height)
                return rect;

            if (bounds.Width == 0 || bounds.Height == 0)
                return new SKSizeI(0, 0);

            var scaleFactorWidth = (double)rect.Width / bounds.Width;
            var scaleFactorHeight = (double)rect.Height / bounds.Height;

            var scaleFactor = Math.Max(scaleFactorWidth, scaleFactorHeight);
            return new SKSizeI
            {
                Width = (int)(rect.Width / scaleFactor),
                Height = (int)(rect.Height / scaleFactor)
            };
        }

        private static SKSize CalculateRotatedRectSize(SKSize rectSize, double angleDeg)
        {
            var angleRad = angleDeg * Math.PI / 180;
            var width = rectSize.Height * Math.Abs(Math.Sin(angleRad)) + 
                        rectSize.Width * Math.Abs(Math.Cos(angleRad));
            var height = rectSize.Height * Math.Abs(Math.Cos(angleRad)) +
                         rectSize.Width * Math.Abs(Math.Sin(angleRad));
            return new SKSize((float) width, (float) height);
        }
        
        private static SKPointI CalculateWatermarkPosition(WatermarkPosition watermarkPosition, SKSizeI imageSize, SKSizeI watermarkSize)
        {
            var position = new SKPointI();
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

        private SKTypeface GetFontTypeface(WatermarkSettings settings)
        {
            var typeface = _fontProvider.GetTypeface(settings.WatermarkFont);
            if (typeface != null)
                return typeface;

            return _fontProvider.AvailableFonts.Any()
                ? _fontProvider.GetTypeface(_fontProvider.AvailableFonts.First())
                : throw new InvalidOperationException("Fonts are missing");
        }

        #region IDisposable

        private void ReleaseUnmanagedResources()
        {
            if (_watermarkImage.IsStarted)
                _watermarkImage.Task.Result.Dispose();
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
