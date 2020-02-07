using System;
using System.Drawing.Text;
using System.IO;
using Nop.Core;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class CustomFonts
    {
        private readonly Lazy<FontCollection> _fontCollection;

        public CustomFonts()
        {
            _fontCollection = new Lazy<FontCollection>(() =>
            {
                PrivateFontCollection customFontsCollection = new PrivateFontCollection();
                var customFontsFiles = Directory.EnumerateFiles(CommonHelper.MapPath("~/Plugins/Misc.Watermark/Fonts"),
                    "*.ttf",
                    SearchOption.AllDirectories);
                foreach (string fontFileName in customFontsFiles)
                {
                    customFontsCollection.AddFontFile(fontFileName);
                }

                return customFontsCollection;
            });
        }

        public FontCollection FontCollection()
        {
            return _fontCollection.Value;
        }

        public readonly string CustomFontPrefix = "custom/";
    }
}
