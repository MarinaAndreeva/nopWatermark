using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Newtonsoft.Json;
using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.Watermark
{
    public class WatermarkSettings : ISettings
    {
        public bool WatermarkTextEnable { get; set; }
        public string WatermarkText { get; set; }
        public string WatermarkFont { get; set; }
        public Color TextColor { get; set; }
        public int TextRotatedDegree { get; set; }
        public CommonSettings TextSettings { get; set; }

        public bool WatermarkPictureEnable { get; set; }
        public int PictureId { get; set; }
        public CommonSettings PictureSettings { get; set; }

        public bool ApplyOnProductPictures { get; set; }
        public bool ApplyOnCategoryPictures { get; set; }
        public bool ApplyOnManufacturerPictures { get; set; }
        public int MinimumImageWidthForWatermark { get; set; }
        public int MinimumImageHeightForWatermark { get; set; }
    }

    public enum WatermarkPosition
    {
        TopLeftCorner,
        TopCenter,
        TopRightCorner,
        CenterLeft,
        Center,
        CenterRight,
        BottomLeftCorner,
        BottomCenter,
        BottomRightCorner,
    }

    [TypeConverter("Nop.Plugin.Misc.Watermark.CommonSettingsConvertor")]
    [JsonConverter(typeof(NoTypeConverterJsonConverter<CommonSettings>))]
    public class CommonSettings
    {
        public int Size { get; set; }
        public List<WatermarkPosition> PositionList { get; set; }
        public double Opacity { get; set; }
    }

}
