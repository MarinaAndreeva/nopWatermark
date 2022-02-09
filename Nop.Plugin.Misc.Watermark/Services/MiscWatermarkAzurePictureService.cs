using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.Watermark.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.Watermark.Services
{
    public class MiscWatermarkAzurePictureService : MiscWatermarkPictureService
    {
        private static BlobContainerClient _blobContainerClient;
        private static BlobServiceClient _blobServiceClient;
        private static bool _azureBlobStorageAppendContainerName;
        private static bool _isInitialized;
        private static string _azureBlobStorageConnectionString;
        private static string _azureBlobStorageContainerName;
        private static string _azureBlobStorageEndPoint;

        private readonly IStaticCacheManager _staticCacheManager;
        private readonly MediaSettings _mediaSettings;

        private readonly object _locker = new();

        public MiscWatermarkAzurePictureService(
            IRepository<Picture> pictureRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            ISettingService settingService,
            IWebHelper webHelper,
            MediaSettings mediaSettings,
            IStoreContext storeContext,
            INopFileProvider fileProvider,
            IProductAttributeParser productAttributeParser,
            IRepository<PictureBinary> pictureBinaryRepository,
            IUrlRecordService urlRecordService,
            IDownloadService downloadService,
            IHttpContextAccessor httpContextAccessor,
            IPluginService pluginService,
            FontProvider fontProvider,
            IStaticCacheManager staticCacheManager,
            AppSettings appSettings)
            : base(pictureRepository,
                categoryRepository,
                manufacturerRepository,
                productPictureRepository,
                settingService,
                webHelper,
                mediaSettings,
                storeContext,
                fileProvider,
                productAttributeParser,
                pictureBinaryRepository,
                urlRecordService,
                downloadService,
                httpContextAccessor,
                pluginService,
                fontProvider)
        {
            _staticCacheManager = staticCacheManager;
            _mediaSettings = mediaSettings;

            OneTimeInit(appSettings);
        }

        protected void OneTimeInit(AppSettings appSettings)
        {
            if (_isInitialized)
                return;

            if (string.IsNullOrEmpty(appSettings.Get<AzureBlobConfig>().ConnectionString))
                throw new Exception("Azure connection string for Blob is not specified");

            if (string.IsNullOrEmpty(appSettings.Get<AzureBlobConfig>().ContainerName))
                throw new Exception("Azure container name for Blob is not specified");

            if (string.IsNullOrEmpty(appSettings.Get<AzureBlobConfig>().EndPoint))
                throw new Exception("Azure end point for Blob is not specified");

            lock (_locker)
            {
                if (_isInitialized)
                    return;

                _azureBlobStorageAppendContainerName = appSettings.Get<AzureBlobConfig>().AppendContainerName;
                _azureBlobStorageConnectionString = appSettings.Get<AzureBlobConfig>().ConnectionString;
                _azureBlobStorageContainerName = appSettings.Get<AzureBlobConfig>().ContainerName.Trim().ToLower();
                _azureBlobStorageEndPoint = appSettings.Get<AzureBlobConfig>().EndPoint.Trim().ToLower().TrimEnd('/');

                _blobServiceClient = new BlobServiceClient(_azureBlobStorageConnectionString);
                _blobContainerClient = _blobServiceClient.GetBlobContainerClient(_azureBlobStorageContainerName);

                CreateCloudBlobContainerAsync().GetAwaiter().GetResult();

                _isInitialized = true;
            }
        }

        protected virtual async Task CreateCloudBlobContainerAsync()
        {
            await _blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
        }

        protected override Task<string> GetThumbLocalPathAsync(string thumbFileName)
        {
            var path = _azureBlobStorageAppendContainerName ? $"{_azureBlobStorageContainerName}/" : string.Empty;

            return Task.FromResult($"{_azureBlobStorageEndPoint}/{path}{thumbFileName}");
        }

        protected override async Task<string> GetThumbUrlAsync(string thumbFileName, string storeLocation = null) =>
            await GetThumbLocalPathAsync(thumbFileName);

        protected override async Task DeletePictureThumbsAsync(Picture picture)
        {
            //create a string containing the Blob name prefix
            var prefix = $"{picture.Id:0000000}";

            var tasks = await _blobContainerClient.GetBlobsAsync(BlobTraits.All, BlobStates.All, prefix)
                .Select(blob =>
                    _blobContainerClient.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots))
                .Select(dummy => (Task)dummy).ToListAsync();
            await Task.WhenAll(tasks);

            await _staticCacheManager.RemoveByPrefixAsync(NopMediaDefaults.ThumbsExistsPrefix);
        }

        protected override async Task<bool> GeneratedThumbExistsAsync(string thumbFilePath, string thumbFileName)
        {
            try
            {
                var key = _staticCacheManager.PrepareKeyForDefaultCache(NopMediaDefaults.ThumbExistsCacheKey, thumbFileName);

                return await _staticCacheManager.GetAsync(key,
                    async () => await _blobContainerClient.GetBlobClient(thumbFileName).ExistsAsync());
            }
            catch
            {
                return false;
            }
        }

        protected override async Task SaveThumbAsync(string thumbFilePath, string thumbFileName, string mimeType, byte[] binary)
        {
            var blobClient = _blobContainerClient.GetBlobClient(thumbFileName);
            await using var ms = new MemoryStream(binary);

            //set mime type
            BlobHttpHeaders headers = null;
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                headers = new BlobHttpHeaders
                {
                    ContentType = mimeType
                };
            }

            //set cache control
            if (!string.IsNullOrWhiteSpace(_mediaSettings.AzureCacheControlHeader))
            {
                headers ??= new BlobHttpHeaders();
                headers.CacheControl = _mediaSettings.AzureCacheControlHeader;
            }

            if (headers is null)
                //We must explicitly indicate through the parameter that the object needs to be overwritten if it already exists
                //See more: https://github.com/Azure/azure-sdk-for-net/issues/9470
                await blobClient.UploadAsync(ms, overwrite: true);
            else
                await blobClient.UploadAsync(ms, new BlobUploadOptions { HttpHeaders = headers });

            await _staticCacheManager.RemoveByPrefixAsync(NopMediaDefaults.ThumbsExistsPrefix);
        }

        public override async Task DeleteThumbs()
        {
            var tasks = await _blobContainerClient.GetBlobsAsync(BlobTraits.All, BlobStates.All)
                .Select(blob =>
                    _blobContainerClient.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots))
                .Select(dummy => (Task)dummy).ToListAsync();
            await Task.WhenAll(tasks);

            await _staticCacheManager.RemoveByPrefixAsync(NopMediaDefaults.ThumbsExistsPrefix);
        }
    }
}
