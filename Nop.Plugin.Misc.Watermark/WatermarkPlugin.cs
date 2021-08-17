using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Caching;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Plugins;
using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark
{
    public class WatermarkPlugin : BasePlugin, IMiscPlugin
    {
        private readonly ILanguageService _languageService;
        private readonly ILocalizationService _localizationService;
        private readonly IPictureService _pictureService;
        private readonly ISettingService _settingService;
        private readonly INopFileProvider _fileProvider;
        private readonly IWebHelper _webHelper;
        private readonly string _pluginLocalesPath;
        private readonly string _defaultWatermarkPicturePath;

        public WatermarkPlugin(ISettingService settingService, IPictureService pictureService,
            ILocalizationService localizationService, ILanguageService languageService, IWebHelper webHelper, INopFileProvider fileProvider)
        {
            _languageService = languageService;
            _localizationService = localizationService;
            _pictureService = pictureService;
            _settingService = settingService;
            _fileProvider = fileProvider;
            _webHelper = webHelper;

            _pluginLocalesPath = fileProvider.MapPath("~/Plugins/Misc.Watermark/Resources");
            _defaultWatermarkPicturePath = fileProvider.MapPath("~/Plugins/Misc.Watermark/Content/defaultWatermarkPicture.png");
        }

        public override string GetConfigurationPageUrl() =>
            _webHelper.GetStoreLocation() + "Admin/MiscWatermark/Configure";

        public override async Task InstallAsync()
        {
            var settings = new WatermarkSettings
            {
                WatermarkTextEnable = false,
                WatermarkText = "watermark text",
                WatermarkFont = "Arial",
                TextColor = new SKColor(8, 3, 71).ToRgb24Hex(),
                TextSettings = new CommonSettings
                {
                    Size = 50,
                    Opacity = 0.5,
                    PositionList = new List<WatermarkPosition>(),
                },
                TextRotatedDegree = 0,
                WatermarkPictureEnable = false,
                PictureId = (await _pictureService.InsertPictureAsync(
                    await _fileProvider.ReadAllBytesAsync(_defaultWatermarkPicturePath), MimeTypes.ImagePng,
                    "defaultWatermarkPicture")).Id,
                PictureSettings = new CommonSettings
                {
                    Size = 50,
                    Opacity = 0.5,
                    PositionList = new List<WatermarkPosition>(),
                },
                ApplyOnProductPictures = true,
                ApplyOnCategoryPictures = false,
                ApplyOnManufacturerPictures = false,
                MinimumImageWidthForWatermark = 150,
                MinimumImageHeightForWatermark = 150,
            };
            
            await _settingService.SaveSettingAsync(settings);

            await LoadLocaleResourcesAsync();

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            var watermarkPicture = await _pictureService.GetPictureByIdAsync((await _settingService.LoadSettingAsync<WatermarkSettings>()).PictureId);
            if (watermarkPicture != null)
                await _pictureService.DeletePictureAsync(watermarkPicture);

            await _settingService.DeleteSettingAsync<WatermarkSettings>();
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.Watermark");

            await _settingService.ClearCacheAsync();

            await new ClearCacheTask(EngineContext.Current.Resolve<IStaticCacheManager>()).ExecuteAsync();
            if (EngineContext.Current.Resolve<IPictureService>() is MiscWatermarkPictureService pictureService)
                pictureService.DeleteThumbs().Wait();

            await base.UninstallAsync();
        }

        private async Task LoadLocaleResourcesAsync()
        {
            var localesDirectory = new DirectoryInfo(_pluginLocalesPath);
            var defaultLocaleFile = localesDirectory.GetFiles("Locale.default.xml").FirstOrDefault();
            if (defaultLocaleFile != null)
                await loadLocalizationResourcesFile(defaultLocaleFile, await _languageService.GetAllLanguagesAsync(true));

            foreach (var fileInfo in localesDirectory.GetFiles("Locale.*.xml"))
            {
                var langCode = fileInfo.Name.Split(new[] {'.'}).Reverse().ElementAt(1);
                if (langCode == "default")
                    continue;

                var languages = (await _languageService.GetAllLanguagesAsync(true))
                    .Where(x => x.UniqueSeoCode == langCode);
                await loadLocalizationResourcesFile(fileInfo, languages);
            }

            async Task loadLocalizationResourcesFile(FileInfo fileInfo, IEnumerable<Nop.Core.Domain.Localization.Language> languages)
            {
                foreach (var language in languages)
                {
                    await using var file = fileInfo.OpenRead();
                    using var sr = new StreamReader(file, Encoding.UTF8);
                    await _localizationService.ImportResourcesFromXmlAsync(language, sr);
                }
            }
        }
    }
}