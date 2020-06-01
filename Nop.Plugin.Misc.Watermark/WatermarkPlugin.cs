﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Localization;
using Nop.Core.Infrastructure;
using Nop.Core.Domain.Media;
using Nop.Services.Caching;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Plugins;
using SixLabors.ImageSharp.PixelFormats;

namespace Nop.Plugin.Misc.Watermark
{
    public class WatermarkPlugin : BasePlugin, IMiscPlugin
    {
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly string _pluginLocalesPath;
        private readonly string _defaultWatermarkPicturePath;

        public WatermarkPlugin(ISettingService settingService, IPictureService pictureService,
            ILocalizationService localizationService, ILanguageService languageService, IWebHelper webHelper, INopFileProvider fileProvider)
        {
            _settingService = settingService;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _languageService = languageService;
            _webHelper = webHelper;

            _pluginLocalesPath = fileProvider.MapPath("~/Plugins/Misc.Watermark/Resources");
            _defaultWatermarkPicturePath = fileProvider.MapPath("~/Plugins/Misc.Watermark/Content/defaultWatermarkPicture.png");
        }

        public override string GetConfigurationPageUrl()
        {
            return _webHelper.GetStoreLocation() + "Admin/MiscWatermark/Configure";
        }

        public override void Install()
        {
            string defaultWatermarkPictureMapPath = _defaultWatermarkPicturePath;
            WatermarkSettings settings = new WatermarkSettings
            {
                WatermarkTextEnable = false,
                WatermarkText = "watermark text",
                WatermarkFont = "Arial",
                TextColor = new Rgba32(8,3,71).ToRgb24Hex(),
                TextSettings = new CommonSettings()
                {
                    Size = 50,
                    Opacity = 0.5,
                    PositionList = new List<WatermarkPosition>(),
                },
                TextRotatedDegree = 0,
                WatermarkPictureEnable = false,
                PictureId = this._pictureService.InsertPicture(File.ReadAllBytes(defaultWatermarkPictureMapPath),
                    "image/png", "defaultWatermarkPicture").Id,
                PictureSettings = new CommonSettings()
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
            _settingService.SaveSetting(settings);

            LoadLocaleResources();

            base.Install();
        }

        public override void Uninstall()
        {
            Picture watermarkPicture = _pictureService.GetPictureById(_settingService.LoadSetting<WatermarkSettings>().PictureId);
            if (watermarkPicture != null)
            {
                _pictureService.DeletePicture(watermarkPicture);
            }

            _settingService.DeleteSetting<WatermarkSettings>();
            _localizationService.DeletePluginLocaleResources("Plugins.Misc.Watermark.");

            _settingService.ClearCache();

            new ClearCacheTask(EngineContext.Current.Resolve<IStaticCacheManager>()).Execute();
            if (EngineContext.Current.Resolve<IPictureService>() is MiscWatermarkPictureService pictureService)
                pictureService.DeleteThumbs().Wait();

            base.Uninstall();
        }

        private void LoadLocaleResources()
        {
            DirectoryInfo localesDirectory = new DirectoryInfo(_pluginLocalesPath);

            FileInfo defaultLocaleFile = localesDirectory.GetFiles("Locale.default.xml").FirstOrDefault();
            if (defaultLocaleFile != null)
            {
                using (var sr = new StreamReader(defaultLocaleFile.OpenRead(), Encoding.UTF8))
                {
                    foreach (var language in _languageService.GetAllLanguages(true))
                    {
                        _localizationService.ImportResourcesFromXml(language, sr);
                    }
                }
            }

            foreach (FileInfo fileInfo in localesDirectory.GetFiles("Locale.*.xml"))
            {
                var file = fileInfo.OpenRead();
                string langCode = fileInfo.Name.Split(new[] {'.'}).Reverse().ElementAt(1);
                if (langCode != "default")
                {
                    using (var sr = new StreamReader(file, Encoding.UTF8))
                    {
                        Language language = _languageService.GetAllLanguages(true)
                            .FirstOrDefault(x => x.UniqueSeoCode == langCode);
                        if (language != null)
                        {
                            _localizationService.ImportResourcesFromXml(language, sr);
                        }
                    }
                }
            }
        }
    }
}