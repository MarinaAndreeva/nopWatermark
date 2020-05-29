using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.Watermark.Services;
using Nop.Services.Media;

namespace Nop.Plugin.Misc.Watermark.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            if (config.AzureBlobStorageEnabled)
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
