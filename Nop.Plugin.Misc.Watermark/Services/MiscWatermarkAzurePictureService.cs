using System;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Blob;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Plugins;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkAzurePictureService : MiscWatermarkPictureService
    {
        private static bool _isInitialized;
        private static CloudBlobContainer _container;
        private static string _azureBlobStorageConnectionString;
        private static string _azureBlobStorageContainerName;
        private static string _azureBlobStorageEndPoint;

        private readonly object _locker = new object();

        public MiscWatermarkAzurePictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
            ILogger logger,
            IDbContext dbContext,
            IEventPublisher eventPublisher,
            MediaSettings mediaSettings,
            IDataProvider dataProvider,
            IStoreContext storeContext,
            IPluginFinder pluginFinder,
            CustomFonts customFonts,
            NopConfig config)
            : base(pictureRepository,
                categoryRepository,
                manufacturerRepository,
                productPictureRepository,
                settingService,
                webHelper,
                logger,
                dbContext,
                eventPublisher,
                mediaSettings,
                storeContext,
                pluginFinder,
                customFonts)
        {
            OneTimeInit(config);
        }

        protected void OneTimeInit(NopConfig config)
        {
            if (_isInitialized)
                return;

            if (string.IsNullOrEmpty(config.AzureBlobStorageConnectionString))
                throw new Exception("Azure connection string for BLOB is not specified");

            if (string.IsNullOrEmpty(config.AzureBlobStorageContainerName))
                throw new Exception("Azure container name for BLOB is not specified");

            if (string.IsNullOrEmpty(config.AzureBlobStorageEndPoint))
                throw new Exception("Azure end point for BLOB is not specified");

            lock (_locker)
            {
                if (_isInitialized)
                    return;

                _azureBlobStorageConnectionString = config.AzureBlobStorageConnectionString;
                _azureBlobStorageContainerName = config.AzureBlobStorageContainerName.Trim().ToLower();
                _azureBlobStorageEndPoint = config.AzureBlobStorageEndPoint.Trim().ToLower().TrimEnd('/');

                var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(_azureBlobStorageConnectionString);
                if (storageAccount == null)
                    throw new Exception("Azure connection string for BLOB is not working");

                //should we do it for each HTTP request?
                var blobClient = storageAccount.CreateCloudBlobClient();

                //GetContainerReference doesn't need to be async since it doesn't contact the server yet
                _container = blobClient.GetContainerReference(_azureBlobStorageContainerName);

                _container.CreateIfNotExists();
                _container.SetPermissions(new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                });

                _isInitialized = true;
            }
        }

        protected override string GetThumbLocalPath(string thumbFileName)
        {
            return $"{_azureBlobStorageEndPoint}/{_azureBlobStorageContainerName}/{thumbFileName}";
        }

        protected override string GetThumbUrl(string thumbFileName, string storeLocation = null)
        {
            return GetThumbLocalPath(thumbFileName);
        }

        protected override bool GeneratedThumbExists(string thumbFilePath, string thumbFileName)
        {
            try
            {
                CloudBlockBlob blockBlob = _container.GetBlockBlobReference(thumbFileName);
                return blockBlob.Exists();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        protected override void SaveThumb(string thumbFilePath, string thumbFileName, byte[] binary)
        {
            CloudBlockBlob blockBlob = _container.GetBlockBlobReference(thumbFileName);
            blockBlob.UploadFromByteArray(binary, 0, binary.Length);
        }

        protected override void DeletePictureThumbs(Picture picture)
        {
            string filter = string.Format("{0}", picture.Id.ToString("0000000"));
            var files = _container.ListBlobs(prefix: filter, useFlatBlobListing: false);
            foreach (var ff in files)
            {
                CloudBlockBlob blockBlob = (CloudBlockBlob)ff;
                blockBlob.Delete();
            }
        }

        public override void DeleteThumbs()
        {
            var files = _container.ListBlobs(prefix: null, useFlatBlobListing: false);
            foreach (var ff in files)
            {
                CloudBlockBlob blockBlob = (CloudBlockBlob)ff;
                blockBlob.Delete();
            }

        }
    }
}
