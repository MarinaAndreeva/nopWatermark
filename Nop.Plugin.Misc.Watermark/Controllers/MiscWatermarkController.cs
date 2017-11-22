using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Plugin.Misc.Watermark.Models;
using Nop.Services.Caching;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.Watermark.Controllers
{
    [Area(AreaNames.Admin)]
    public class MiscWatermarkController : BasePluginController
    {
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IPermissionService _permissionService;
        private readonly IWorkContext _workContext;

        public MiscWatermarkController(
            IWorkContext workContext,
            IStoreService storeService,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ISettingService settingService)
        {
            _workContext = workContext;
            _storeService = storeService;
            _localizationService = localizationService;
            _settingService = settingService;
            _permissionService = permissionService;
        }

        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            List<string> availableFonts = GetAvailableFontNames();

            int activeStoreScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            WatermarkSettings settings = _settingService.LoadSetting<WatermarkSettings>(activeStoreScope);

            var model = new ConfigurationModel
            {
                WatermarkTextEnable = settings.WatermarkTextEnable,
                WatermarkText = settings.WatermarkText,
                AvailableFontsList = availableFonts.Select(s => new SelectListItem {Text = s, Value = s}).ToList(),
                WatermarkFont = settings.WatermarkFont,
                TextColor = $"{settings.TextColor.R:X2}{settings.TextColor.G:X2}{settings.TextColor.B:X2}",
                TextSettings = new CommonWatermarkSettings
                {
                    Size = settings.TextSettings.Size,
                    TopLeftCorner = settings.TextSettings.PositionList.Contains(WatermarkPosition.TopLeftCorner),
                    TopCenter = settings.TextSettings.PositionList.Contains(WatermarkPosition.TopCenter),
                    TopRightCorner = settings.TextSettings.PositionList.Contains(WatermarkPosition.TopRightCorner),
                    CenterLeft = settings.TextSettings.PositionList.Contains(WatermarkPosition.CenterLeft),
                    Center = settings.TextSettings.PositionList.Contains(WatermarkPosition.Center),
                    CenterRight = settings.TextSettings.PositionList.Contains(WatermarkPosition.CenterRight),
                    BottomLeftCorner = settings.TextSettings.PositionList.Contains(WatermarkPosition.BottomLeftCorner),
                    BottomCenter = settings.TextSettings.PositionList.Contains(WatermarkPosition.BottomCenter),
                    BottomRightCorner = settings.TextSettings.PositionList.Contains(WatermarkPosition.BottomRightCorner),
                    Opacity = settings.TextSettings.Opacity
                },
                TextRotatedDegree = settings.TextRotatedDegree,
                WatermarkPictureEnable = settings.WatermarkPictureEnable,
                PictureId = settings.PictureId,
                PictureSettings = new CommonWatermarkSettings
                {
                    Size = settings.PictureSettings.Size,
                    TopLeftCorner = settings.PictureSettings.PositionList.Contains(WatermarkPosition.TopLeftCorner),
                    TopCenter = settings.PictureSettings.PositionList.Contains(WatermarkPosition.TopCenter),
                    TopRightCorner = settings.PictureSettings.PositionList.Contains(WatermarkPosition.TopRightCorner),
                    CenterLeft = settings.PictureSettings.PositionList.Contains(WatermarkPosition.CenterLeft),
                    Center = settings.PictureSettings.PositionList.Contains(WatermarkPosition.Center),
                    CenterRight = settings.PictureSettings.PositionList.Contains(WatermarkPosition.CenterRight),
                    BottomLeftCorner = settings.PictureSettings.PositionList.Contains(WatermarkPosition.BottomLeftCorner),
                    BottomCenter = settings.PictureSettings.PositionList.Contains(WatermarkPosition.BottomCenter),
                    BottomRightCorner = settings.PictureSettings.PositionList.Contains(WatermarkPosition.BottomRightCorner),
                    Opacity = settings.PictureSettings.Opacity
                },
                ActiveStoreScopeConfiguration = activeStoreScope,
                ApplyOnProductPictures = settings.ApplyOnProductPictures,
                ApplyOnCategoryPictures = settings.ApplyOnCategoryPictures,
                ApplyOnManufacturerPictures = settings.ApplyOnManufacturerPictures,
                MinimumImageWidthForWatermark = settings.MinimumImageWidthForWatermark,
                MinimumImageHeightForWatermark = settings.MinimumImageHeightForWatermark
            };
            if (activeStoreScope > 0)
            {
                model.WatermarkTextEnable_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.WatermarkTextEnable, activeStoreScope);
                model.Text_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.WatermarkText, activeStoreScope);
                model.WatermarkFont_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.WatermarkFont, activeStoreScope);
                model.TextColor_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.TextColor, activeStoreScope);
                model.TextSettings_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.TextSettings, activeStoreScope);
                model.WatermarkTextRotatedDegree_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.TextRotatedDegree, activeStoreScope);
                model.WatermarkPictureEnable_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.WatermarkPictureEnable, activeStoreScope);
                model.WatermarkPictureId_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.PictureId, activeStoreScope);
                model.PictureSettings_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.PictureSettings, activeStoreScope);
                model.ApplyOnProductPictures_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.ApplyOnProductPictures, activeStoreScope);
                model.ApplyOnCategoryPictures_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.ApplyOnCategoryPictures, activeStoreScope);
                model.ApplyOnManufacturerPictures_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.ApplyOnManufacturerPictures, activeStoreScope);
                model.WatermarkMinimumImageHeightForWatermark_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.MinimumImageHeightForWatermark, activeStoreScope);
                model.WatermarkMinimumImageWidthForWatermark_OverrideForStore =
                    _settingService.SettingExists(settings, x => x.MinimumImageWidthForWatermark, activeStoreScope);
            }
            return View("~/Plugins/Misc.Watermark/Views/MiscWatermark/Configure.cshtml", model);
        }

        [HttpPost]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            if (!ModelState.IsValid)
            {
                return Configure();
            }

            int activeStoreScope = GetActiveStoreScopeConfiguration(_storeService, _workContext);

            WatermarkSettings settings = _settingService.LoadSetting<WatermarkSettings>(activeStoreScope);

            settings.WatermarkTextEnable = model.WatermarkTextEnable;
            settings.WatermarkText = model.WatermarkText;
            settings.WatermarkFont = model.WatermarkFont;
            if (model.TextColor != null)
            {
                settings.TextColor = ColorTranslator.FromHtml("#" + model.TextColor);
            }
            settings.TextRotatedDegree = model.TextRotatedDegree;
            if (model.TextSettings != null)
            {
                settings.TextSettings.Size = model.TextSettings.Size;
                settings.TextSettings.Opacity = model.TextSettings.Opacity;
                settings.TextSettings.PositionList = PreparePositionList(model.TextSettings);
            }

            settings.WatermarkPictureEnable = model.WatermarkPictureEnable;
            settings.PictureId = model.PictureId == 0 ? settings.PictureId : model.PictureId;
            if (model.PictureSettings != null)
            {
                settings.PictureSettings.Size = model.PictureSettings.Size;
                settings.PictureSettings.Opacity = model.PictureSettings.Opacity;
                settings.PictureSettings.PositionList = PreparePositionList(model.PictureSettings);
            }

            settings.ApplyOnProductPictures = model.ApplyOnProductPictures;
            settings.ApplyOnCategoryPictures = model.ApplyOnCategoryPictures;
            settings.ApplyOnManufacturerPictures = model.ApplyOnManufacturerPictures;
            settings.MinimumImageHeightForWatermark = model.MinimumImageHeightForWatermark;
            settings.MinimumImageWidthForWatermark = model.MinimumImageHeightForWatermark;

            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.WatermarkTextEnable, model.WatermarkTextEnable_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.WatermarkText, model.Text_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.WatermarkFont, model.WatermarkFont_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.TextColor, model.TextColor_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.TextRotatedDegree, model.WatermarkTextRotatedDegree_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.TextSettings, model.TextSettings_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.WatermarkPictureEnable, model.WatermarkPictureEnable_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.PictureId, model.WatermarkPictureId_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.PictureSettings, model.PictureSettings_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.ApplyOnProductPictures, model.ApplyOnProductPictures_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.ApplyOnCategoryPictures, model.ApplyOnCategoryPictures_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.ApplyOnManufacturerPictures, model.ApplyOnManufacturerPictures_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.MinimumImageHeightForWatermark, model.WatermarkMinimumImageHeightForWatermark_OverrideForStore, activeStoreScope, false);
            _settingService.SaveSettingOverridablePerStore(settings,
                x => x.MinimumImageWidthForWatermark, model.WatermarkMinimumImageWidthForWatermark_OverrideForStore, activeStoreScope, false);

            //_settingService.ClearCache();
            new ClearCacheTask(EngineContext.Current.Resolve<IStaticCacheManager>()).Execute();
            Utils.ClearThumbsDirectory();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        private List<WatermarkPosition> PreparePositionList(CommonWatermarkSettings model)
        {
            var positionList = new List<WatermarkPosition>();
            if (model.TopLeftCorner)
                positionList.Add(WatermarkPosition.TopLeftCorner);
            if (model.TopCenter)
                positionList.Add(WatermarkPosition.TopCenter);
            if (model.TopRightCorner)
                positionList.Add(WatermarkPosition.TopRightCorner);
            if (model.CenterLeft)
                positionList.Add(WatermarkPosition.CenterLeft);
            if (model.Center)
                positionList.Add(WatermarkPosition.Center);
            if (model.CenterRight)
                positionList.Add(WatermarkPosition.CenterRight);
            if (model.BottomLeftCorner)
                positionList.Add(WatermarkPosition.BottomLeftCorner);
            if (model.BottomCenter)
                positionList.Add(WatermarkPosition.BottomCenter);
            if (model.BottomRightCorner)
                positionList.Add(WatermarkPosition.BottomRightCorner);
            return positionList;
        }

        private static List<string> GetAvailableFontNames()
        {
            var fonts = new InstalledFontCollection();
            return fonts.Families.Select(f => f.Name).ToList();
        }
    }
}