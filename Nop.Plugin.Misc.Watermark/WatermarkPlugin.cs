using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Localization;
using Nop.Core.Plugins;
using Nop.Services.Caching;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Plugin.Misc.Watermark.Infrastructure;

namespace Nop.Plugin.Misc.Watermark
{
    public class WatermarkPlugin : BasePlugin, IMiscPlugin
    {
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly ILanguageService _languageService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;

        public WatermarkPlugin(ISettingService settingService, IPictureService pictureService,
            ILocalizationService localizationService, ILanguageService languageService, IWebHelper webHelper)
        {
            _settingService = settingService;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _languageService = languageService;
            _webHelper = webHelper;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName,
            out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "MiscWatermark";
            routeValues = new RouteValueDictionary
            {
                {"Namespaces", "Nop.Plugin.Misc.Watermark.Controllers"},
                {"area", null}
            };
        }

        public override void Install()
        {
            string defaultWatermarkPictureMapPath = _webHelper
                .MapPath("~/Plugins/Misc.Watermark/Content/defaultWatermarkPicture.png");
            WatermarkSettings settings = new WatermarkSettings
            {
                WatermarkTextEnable = false,
                WatermarkText = "watermark text",
                WatermarkFont = "Arial",
                TextColor = Color.FromArgb(8,3,71),
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
            _pictureService.DeletePicture(_pictureService.GetPictureById(_settingService.LoadSetting<WatermarkSettings>().PictureId));

            _settingService.DeleteSetting<WatermarkSettings>();
            DeleteLocaleResources();

            _settingService.ClearCache();
            new ClearCacheTask().Execute();
            Utils.ClearThumbsDirectory();

            base.Uninstall();
        }

        private void LoadLocaleResources()
        {
            DirectoryInfo localesDirectory = new DirectoryInfo(_webHelper.MapPath("~/Plugins/Misc.Watermark/Resources"));

            FileInfo defaultLocaleFile = localesDirectory.GetFiles("Locale.default.xml").FirstOrDefault();
            if (defaultLocaleFile != null)
            {
                using (var sr = new StreamReader(defaultLocaleFile.OpenRead(), Encoding.UTF8))
                {
                    string content = sr.ReadToEnd();
                    foreach (var language in _languageService.GetAllLanguages(true))
                    {
                        _localizationService.ImportResourcesFromXml(language, content);
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
                        string content = sr.ReadToEnd();
                        Language language = _languageService.GetAllLanguages(true)
                            .FirstOrDefault(x => x.UniqueSeoCode == langCode);
                        if (language != null)
                        {
                            _localizationService.ImportResourcesFromXml(language, content);
                        }
                    }
                }
            }
        }

        private void DeleteLocaleResources()
        {
            foreach (var lang in _languageService.GetAllLanguages(true))
            {
                var localeStringResources = _localizationService.GetAllResources(lang.Id)
                    .Where(x => x.ResourceName.StartsWith("Plugins.Misc.Watermark."));
                foreach (LocaleStringResource lsr in localeStringResources)
                {
                    _localizationService.DeleteLocaleStringResource(lsr);
                }
            }
        }
    }
}