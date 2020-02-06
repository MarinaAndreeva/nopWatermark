using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.WindowsAzure.Storage.Blob;
using Nop.Core;
using Nop.Core.Caching;
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
        private const string THUMB_EXISTS_KEY = "Nop.azure.thumb.exists-{0}";
        private const string THUMBS_PATTERN_KEY = "Nop.azure.thumb";

        private static bool _isInitialized;
        private static CloudBlobContainer _container;
        private static string _azureBlobStorageConnectionString;
        private static string _azureBlobStorageContainerName;
        private static string _azureBlobStorageEndPoint;

        private readonly IStaticCacheManager _cacheManager;
        private readonly MediaSettings _mediaSettings;
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
            IHostingEnvironment hostingEnvironment,
            IStaticCacheManager cacheManager,
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
                dataProvider,
                storeContext,
                pluginFinder,
                hostingEnvironment,
                customFonts)
        {
            _cacheManager = cacheManager;
            _mediaSettings = mediaSettings;

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

                CreateCloudBlobContainer();

                _isInitialized = true;
            }
        }

        protected virtual async void CreateCloudBlobContainer()
        {
            var storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(_azureBlobStorageConnectionString);
            if (storageAccount == null)
                throw new Exception("Azure connection string for BLOB is not working");

            //should we do it for each HTTP request?
            var blobClient = storageAccount.CreateCloudBlobClient();

            //GetContainerReference doesn't need to be async since it doesn't contact the server yet
            _container = blobClient.GetContainerReference(_azureBlobStorageContainerName);

            await _container.CreateIfNotExistsAsync();
            await _container.SetPermissionsAsync(new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Blob
            });
        }

        protected override async void DeletePictureThumbs(Picture picture)
        {
            await DeletePictureThumbsAsync(picture);
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
            return GeneratedThumbExistsAsync(thumbFilePath, thumbFileName).Result;
        }

        protected override async void SaveThumb(string thumbFilePath, string thumbFileName, string mimeType, byte[] binary)
        {
            await SaveThumbAsync(thumbFilePath, thumbFileName, mimeType, binary);
        }

        protected virtual async Task DeletePictureThumbsAsync(Picture picture)
        {
            //create a string containing the blob name prefix
            var prefix = $"{picture.Id:0000000}";

            BlobContinuationToken continuationToken = null;
            do
            {
                //get result segment
                //listing snapshots is only supported in flat mode, so set the useFlatBlobListing parameter to true.
                var resultSegment = await _container.ListBlobsSegmentedAsync(prefix, true, BlobListingDetails.All, null,
                    continuationToken, null, null);

                //delete files in result segment
                await Task.WhenAll(resultSegment.Results.Select(blobItem => ((CloudBlockBlob)blobItem).DeleteAsync()));

                //get the continuation token.
                continuationToken = resultSegment.ContinuationToken;
            }
            while (continuationToken != null);

            _cacheManager.RemoveByPattern(THUMBS_PATTERN_KEY);
        }

        protected virtual async Task<bool> GeneratedThumbExistsAsync(string thumbFilePath, string thumbFileName)
        {
            try
            {
                var key = string.Format(THUMB_EXISTS_KEY, thumbFileName);
                return await _cacheManager.Get(key, async () =>
                {
                    //GetBlockBlobReference doesn't need to be async since it doesn't contact the server yet
                    var blockBlob = _container.GetBlockBlobReference(thumbFileName);

                    return await blockBlob.ExistsAsync();
                });
            }
            catch
            {
                return false;
            }
        }

        protected virtual async Task SaveThumbAsync(string thumbFilePath, string thumbFileName, string mimeType, byte[] binary)
        {
            //GetBlockBlobReference doesn't need to be async since it doesn't contact the server yet
            var blockBlob = _container.GetBlockBlobReference(thumbFileName);

            //set mime type
            if (!string.IsNullOrEmpty(mimeType))
                blockBlob.Properties.ContentType = mimeType;

            //set cache control
            if (!string.IsNullOrEmpty(_mediaSettings.AzureCacheControlHeader))
                blockBlob.Properties.CacheControl = _mediaSettings.AzureCacheControlHeader;

            await blockBlob.UploadFromByteArrayAsync(binary, 0, binary.Length);

            _cacheManager.RemoveByPattern(THUMBS_PATTERN_KEY);
        }

        public override async Task DeleteThumbs()
        {
            BlobContinuationToken continuationToken = null;
            do
            {
                var resultSegment = await _container.ListBlobsSegmentedAsync(null, true, BlobListingDetails.All, null,
                    continuationToken, null, null);
                await Task.WhenAll(resultSegment.Results.Select(blobItem => ((CloudBlockBlob)blobItem).DeleteAsync()));
                continuationToken = resultSegment.ContinuationToken;
            }
            while (continuationToken != null);

            _cacheManager.RemoveByPattern(THUMBS_PATTERN_KEY);
        }
    }
}
