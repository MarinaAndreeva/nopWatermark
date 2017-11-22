using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using FluentValidation.Attributes;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Mvc.Models;

namespace Nop.Plugin.Misc.Watermark.Models
{
    [Validator(typeof(ConfigurationModelValidator))]
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        #region Watermark Text

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkTextEnable")]
        public bool WatermarkTextEnable { get; set; }
        public bool WatermarkTextEnable_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkText")]
        public string WatermarkText { get; set; }
        public bool Text_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkFont")]
        public string WatermarkFont { get; set; }
        public bool WatermarkFont_OverrideForStore { get; set; }
        public List<SelectListItem> AvailableFontsList { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkTextColor")]
        public string TextColor { get; set; }
        public bool TextColor_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkTextRotatedDegree")]
        public int TextRotatedDegree { get; set; }
        public bool WatermarkTextRotatedDegree_OverrideForStore { get; set; }

        public CommonWatermarkSettings TextSettings { get; set; }

        public bool TextSettings_OverrideForStore { get; set; }

        #endregion

        #region Watermark Picture

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkPictureEnable")]
        public bool WatermarkPictureEnable { get; set; }
        public bool WatermarkPictureEnable_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.Picture")]
        [UIHint("Picture")]
        public int PictureId { get; set; }
        public bool WatermarkPictureId_OverrideForStore { get; set; }

        public CommonWatermarkSettings PictureSettings { get; set; }
        public bool PictureSettings_OverrideForStore { get; set; }
        #endregion

        #region Common

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkMinimumImageWidth")]
        public int MinimumImageWidthForWatermark { get; set; }
        public bool WatermarkMinimumImageWidthForWatermark_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkMinimumImageHeight")]
        public int MinimumImageHeightForWatermark { get; set; }
        public bool WatermarkMinimumImageHeightForWatermark_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.ApplyOnProductPictures")]
        public bool ApplyOnProductPictures { get; set; }
        public bool ApplyOnProductPictures_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.ApplyOnCategoryPictures")]
        public bool ApplyOnCategoryPictures { get; set; }
        public bool ApplyOnCategoryPictures_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.ApplyOnManufacturerPictures")]
        public bool ApplyOnManufacturerPictures { get; set; }
        public bool ApplyOnManufacturerPictures_OverrideForStore { get; set; }

        #endregion
    }
    
    public class CommonWatermarkSettings
    {
        [NopResourceDisplayName("Plugins.Misc.Watermark.Size")]
        public int Size { get; set; }
        public bool WatermarkSize_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.TopLeftCornerPosition")]
        public bool TopLeftCorner { get; set; }
        public bool TopLeftCorner_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.TopCenterPosition")]
        public bool TopCenter { get; set; }
        public bool TopCenter_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.TopRightCornerPosition")]
        public bool TopRightCorner { get; set; }
        public bool TopRightCorner_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.CenterLeftPosition")]
        public bool CenterLeft { get; set; }
        public bool CenterLeft_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.CenterPosition")]
        public bool Center { get; set; }
        public bool Center_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.CenterRightPosition")]
        public bool CenterRight { get; set; }
        public bool CenterRight_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.BottomLeftCornerPosition")]
        public bool BottomLeftCorner { get; set; }
        public bool BottomLeftCorner_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.BottomCenterPosition")]
        public bool BottomCenter { get; set; }
        public bool BottomCenter_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.BottomRightCornerPosition")]
        public bool BottomRightCorner { get; set; }
        public bool BottomRightCorner_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.Watermark.WatermarkOpacity")]
        public double Opacity { get; set; }
        public bool Opacity_OverrideForStore { get; set; }
    }
}
