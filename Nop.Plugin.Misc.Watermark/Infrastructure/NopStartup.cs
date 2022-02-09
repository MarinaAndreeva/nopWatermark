using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            if (configuration.Get<AzureBlobConfig>().Enabled)
                services.AddScoped<IPictureService, MiscWatermarkAzurePictureService>();
            else
                services.AddScoped<IPictureService, MiscWatermarkPictureService>();

            services.AddScoped<FontProvider>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 3000;
    }
}
