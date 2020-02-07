using Autofac;
using Autofac.Core;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Core.Caching;
using Nop.Plugin.Misc.Watermark.Controllers;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<MiscWatermarkController>().WithParameter(ResolvedParameter.ForNamed<ICacheManager>("nop_cache_static"));
            bool azureBlobStorageEnabled = !string.IsNullOrEmpty(config.AzureBlobStorageConnectionString);
            if (azureBlobStorageEnabled)
            {
                builder.RegisterType<MiscWatermarkAzurePictureService>().As<IPictureService>().InstancePerLifetimeScope();
            }
            else
            {
                builder.RegisterType<MiscWatermarkPictureService>().As<IPictureService>().InstancePerLifetimeScope();
            }
            builder.RegisterType<CustomFonts>().SingleInstance();
        }

        public int Order
        {
            get
            {
                return 5;
            }
        }
    }
}
