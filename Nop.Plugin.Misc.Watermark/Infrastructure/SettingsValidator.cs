using FluentValidation;
using Nop.Plugin.Misc.Watermark.Models;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class ConfigurationModelValidator : AbstractValidator<ConfigurationModel>
    {
        public ConfigurationModelValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.TextSettings).SetValidator(new CommonWatermarkSettingsValidator(localizationService));
            RuleFor(x => x.PictureSettings).SetValidator(new CommonWatermarkSettingsValidator(localizationService));
        }
    }

    public class CommonWatermarkSettingsValidator : AbstractValidator<CommonWatermarkSettings>
    {
        public CommonWatermarkSettingsValidator(ILocalizationService localizationService)
        {
            RuleFor(x => x.Opacity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1).WithMessage(
                localizationService.GetResource("Plugins.Misc.Watermark.WatermarkOpacityErrorMessage"), 0, 1);

            RuleFor(x => x.Size).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100)
                .WithMessage(localizationService.GetResource("Plugins.Misc.Watermark.SizeErrorMessage"), 0, 100);
        }
    }
}