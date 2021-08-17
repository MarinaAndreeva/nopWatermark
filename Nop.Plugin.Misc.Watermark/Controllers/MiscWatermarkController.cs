using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Plugin.Misc.Watermark.Models;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Caching;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark.Controllers
{
    [Area(AreaNames.Admin)]
    public class MiscWatermarkController : BasePluginController
    {
        private readonly IStoreContext _storeContext;
        private readonly FontProvider _fontProvider;
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;

        public MiscWatermarkController(
            IPermissionService permissionService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            FontProvider fontProvider)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _permissionService = permissionService;
            _storeContext = storeContext;
            _fontProvider = fontProvider;
        }

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var activeStoreScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<WatermarkSettings>(activeStoreScope);
            var model = new ConfigurationModel
            {
                WatermarkTextEnable = settings.WatermarkTextEnable,
                WatermarkText = settings.WatermarkText,
                AvailableFontsList = GetAvailableFontNames(),
                WatermarkFont = settings.WatermarkFont,
                TextColor = $"{settings.TextColor}",
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
            //if the selected font is removed from the font catalog
            if (model.AvailableFontsList.All(i => i.Value != model.WatermarkFont))
                model.WatermarkFont = string.Empty;

            if (activeStoreScope > 0)
            {
                model.WatermarkTextEnable_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkTextEnable, activeStoreScope);
                model.Text_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkText, activeStoreScope);
                model.WatermarkFont_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkFont, activeStoreScope);
                model.TextColor_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextColor, activeStoreScope);
                model.TextSettings_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextSettings, activeStoreScope);
                model.WatermarkTextRotatedDegree_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.TextRotatedDegree, activeStoreScope);
                model.WatermarkPictureEnable_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.WatermarkPictureEnable, activeStoreScope);
                model.WatermarkPictureId_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.PictureId, activeStoreScope);
                model.PictureSettings_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.PictureSettings, activeStoreScope);
                model.ApplyOnProductPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnProductPictures, activeStoreScope);
                model.ApplyOnCategoryPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnCategoryPictures, activeStoreScope);
                model.ApplyOnManufacturerPictures_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.ApplyOnManufacturerPictures, activeStoreScope);
                model.WatermarkMinimumImageHeightForWatermark_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MinimumImageHeightForWatermark, activeStoreScope);
                model.WatermarkMinimumImageWidthForWatermark_OverrideForStore =
                    await _settingService.SettingExistsAsync(settings, x => x.MinimumImageWidthForWatermark, activeStoreScope);
            }

            return View("~/Plugins/Misc.Watermark/Views/MiscWatermark/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var activeStoreScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<WatermarkSettings>(activeStoreScope);

            settings.WatermarkTextEnable = model.WatermarkTextEnable;
            settings.WatermarkText = model.WatermarkText;
            settings.WatermarkFont = model.WatermarkFont;
            if (model.TextColor != null)
            {
                SKColor.TryParse(model.TextColor, out var color);
                settings.TextColor = color.ToRgb24Hex();
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

            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkTextEnable, model.WatermarkTextEnable_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkText, model.Text_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkFont, model.WatermarkFont_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextColor, model.TextColor_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextRotatedDegree, model.WatermarkTextRotatedDegree_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.TextSettings, model.TextSettings_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.WatermarkPictureEnable, model.WatermarkPictureEnable_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.PictureId, model.WatermarkPictureId_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.PictureSettings, model.PictureSettings_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnProductPictures, model.ApplyOnProductPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnCategoryPictures, model.ApplyOnCategoryPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.ApplyOnManufacturerPictures, model.ApplyOnManufacturerPictures_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MinimumImageHeightForWatermark, model.WatermarkMinimumImageHeightForWatermark_OverrideForStore, activeStoreScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings,
                x => x.MinimumImageWidthForWatermark, model.WatermarkMinimumImageWidthForWatermark_OverrideForStore, activeStoreScope, false);

            await new ClearCacheTask(EngineContext.Current.Resolve<IStaticCacheManager>()).ExecuteAsync();
            if (EngineContext.Current.Resolve<IPictureService>() is MiscWatermarkPictureService pictureService)
                await pictureService.DeleteThumbs();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        private static List<WatermarkPosition> PreparePositionList(CommonWatermarkSettings model)
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

        private List<SelectListItem> GetAvailableFontNames()
        {
            var customGroup = new SelectListGroup {Name = "Custom"};
            var customFonts = _fontProvider.CustomFonts.Select(s =>
                new SelectListItem {Text = s, Value = s, Group = customGroup});

            var systemGroup = new SelectListGroup {Name = "System"};
            var systemFonts = _fontProvider.SystemFonts.Select(s =>
                new SelectListItem {Text = s, Value = s, Group = systemGroup});
            
            return customFonts.Concat(systemFonts).ToList();
        }
    }
}