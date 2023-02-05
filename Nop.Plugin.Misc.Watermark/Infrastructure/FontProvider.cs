#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LinqToDB.Common;
using Nop.Core.Infrastructure;
using SkiaSharp;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class FontProvider : IDisposable
    {
        private const string FONTS_DIRECTORY_PATH = "~/Plugins/Misc.Watermark/Fonts";

        private readonly Lazy<IReadOnlyDictionary<string, string>> _customFonts; // key - font name, value - file path
        private readonly ConcurrentDictionary<string, SKTypeface?> _fontCache = new(); // key - font name

        public FontProvider(INopFileProvider fileProvider)
        {
            _customFonts = new Lazy<IReadOnlyDictionary<string, string>>(() =>
            {
                var fontsDirectoryPath = fileProvider.MapPath(FONTS_DIRECTORY_PATH);
                return fileProvider
                    .EnumerateFiles(fontsDirectoryPath, "*.ttf", false)
                    .ToDictionary(filename => filename.Replace(fontsDirectoryPath, string.Empty),
                        filename => filename);
            });
        }

        public IEnumerable<string> AvailableFonts => SystemFonts.Concat(CustomFonts);

        public IEnumerable<string> SystemFonts =>
            SKFontManager.Default.FontFamilies
                .Where(name => !string.IsNullOrEmpty(name)); // filter font families without a name

        public IEnumerable<string> CustomFonts => _customFonts.Value.Keys;

        /// <summary>
        /// Retrieve SKTypeface instance by its name. The caller must not dispose returned objects because fonts are
        /// loaded only one time and reused.
        /// </summary>
        public SKTypeface? GetTypeface(string fontName)
        {
            return _fontCache.GetOrAdd(fontName, CreateTypeface);
        }

        private SKTypeface? CreateTypeface(string fontName)
        {
            if (_customFonts.Value.TryGetValue(fontName, out var customFontPath))
                return SKTypeface.FromFile(customFontPath);

            if (SystemFonts.Contains(fontName))
                return SKTypeface.FromFamilyName(fontName);

            return null;
        }

        public void Dispose()
        {
            foreach (var typeface in _fontCache.Values)
                typeface?.Dispose();
            
            _fontCache.Clear();
        }
    }
}