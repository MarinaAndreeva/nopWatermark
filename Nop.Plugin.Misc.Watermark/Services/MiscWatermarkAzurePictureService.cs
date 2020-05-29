using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB.DataProvider;
using Microsoft.AspNetCore.Http;
using Microsoft.WindowsAzure.Storage.Blob;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Caching;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkAzurePictureService : MiscWatermarkPictureService
    {
        private static bool _azureBlobStorageAppendContainerName;
        private static bool _isInitialized;
        private static CloudBlobContainer _container;
        private static string _azureBlobStorageConnectionString;
        private static string _azureBlobStorageContainerName;
        private static string _azureBlobStorageEndPoint;

        private readonly ICacheKeyService _cacheKeyService;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly MediaSettings _mediaSettings;
        private readonly object _locker = new object();

        public MiscWatermarkAzurePictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
            ICacheKeyService cacheKeyService,
            IEventPublisher eventPublisher,
            MediaSettings mediaSettings,
            INopDataProvider dataProvider,
            IStoreContext storeContext,
            INopFileProvider fileProvider,
            IProductAttributeParser productAttributeParser,
            IRepository<PictureBinary> pictureBinaryRepository,
            IUrlRecordService urlRecordService,
            IDownloadService downloadService,            
            IHttpContextAccessor httpContextAccessor,
            IPluginService pluginService,
            CustomFonts customFonts,
            IStaticCacheManager staticCacheManager,
            NopConfig config)
            : base(pictureRepository,
                categoryRepository,
                manufacturerRepository,
                productPictureRepository,
                settingService,
                webHelper,
                cacheKeyService,
                eventPublisher,
                mediaSettings,
                dataProvider,
                storeContext,
                fileProvider,
                productAttributeParser,
                pictureBinaryRepository,
                urlRecordService,
                downloadService,
                httpContextAccessor,
                pluginService,
                customFonts)
        {
            _cacheKeyService = cacheKeyService;
            _staticCacheManager = staticCacheManager;
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

                _azureBlobStorageAppendContainerName = config.AzureBlobStorageAppendContainerName;
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
            var path = _azureBlobStorageAppendContainerName ? _azureBlobStorageContainerName + "/" : string.Empty;

            return $"{_azureBlobStorageEndPoint}/{path}{thumbFileName}";
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

            _staticCacheManager.RemoveByPrefix(NopMediaDefaults.ThumbsExistsPrefixCacheKey);
        }

        protected virtual async Task<bool> GeneratedThumbExistsAsync(string thumbFilePath, string thumbFileName)
        {
            try
            {
                var key = _cacheKeyService.PrepareKeyForDefaultCache(NopMediaDefaults.ThumbExistsCacheKey, thumbFileName);
                return await _staticCacheManager.GetAsync(key, async () =>
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

            _staticCacheManager.RemoveByPrefix(NopMediaDefaults.ThumbsExistsPrefixCacheKey);
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

            _staticCacheManager.RemoveByPrefix(NopMediaDefaults.ThumbsExistsPrefixCacheKey);
        }
    }
}
