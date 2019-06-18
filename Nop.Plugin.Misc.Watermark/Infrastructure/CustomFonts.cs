using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nop.Core.Infrastructure;
using SixLabors.Fonts;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class CustomFonts
    {
        private readonly Lazy<FontCollection> _fontCollection;

        public CustomFonts(INopFileProvider fileProvider)
        {
            _fontCollection = new Lazy<FontCollection>(() =>
            {
                FontCollection customFontsCollection = new FontCollection();
                var customFontsFiles = Directory.EnumerateFiles(fileProvider.MapPath("~/Plugins/Misc.Watermark/Fonts"),
                    "*.ttf",
                    SearchOption.AllDirectories);
                foreach (string fontFileName in customFontsFiles)
                {
                    customFontsCollection.Install(fontFileName);
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
