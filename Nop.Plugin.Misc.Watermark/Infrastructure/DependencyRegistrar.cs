using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            if (appSettings.AzureBlobConfig.Enabled)
                services.AddScoped<IPictureService, MiscWatermarkAzurePictureService>();
            else
                services.AddScoped<IPictureService, MiscWatermarkPictureService>();
            
            services.AddScoped<FontProvider>();
        }

        public int Order => 5;
    }
}
