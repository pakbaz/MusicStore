using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MvcMusicStore.Services;

namespace MvcMusicStore.Controllers
{
    /// <summary>
    /// Streams blobs (album thumbnails and generated audio) from private Azure Storage
    /// containers using the app's managed identity. Anonymous public blob access is
    /// disallowed by subscription policy, so media is served through this proxy instead
    /// of direct blob URLs.
    /// </summary>
    [AllowAnonymous]
    [Route("media")]
    public class MediaController : Controller
    {
        private readonly BlobServiceClient blobServiceClient;
        private readonly StorageOptions storageOptions;
        private readonly ILogger<MediaController> logger;

        public MediaController(
            BlobServiceClient blobServiceClient,
            IOptions<StorageOptions> storageOptions,
            ILogger<MediaController> logger)
        {
            this.blobServiceClient = blobServiceClient;
            this.storageOptions = storageOptions.Value;
            this.logger = logger;
        }

        [HttpGet("thumbnails/{**blobName}")]
        public Task<IActionResult> Thumbnail(string blobName, CancellationToken cancellationToken)
            => StreamBlobAsync(storageOptions.ThumbnailsContainer, blobName, "image/jpeg", cancellationToken);

        [HttpGet("music/{**blobName}")]
        public Task<IActionResult> Music(string blobName, CancellationToken cancellationToken)
            => StreamBlobAsync(storageOptions.MusicContainer, blobName, "application/octet-stream", enableRangeProcessing: true, cancellationToken);

        private async Task<IActionResult> StreamBlobAsync(string containerName, string blobName, string fallbackContentType, CancellationToken cancellationToken)
            => await StreamBlobAsync(containerName, blobName, fallbackContentType, enableRangeProcessing: false, cancellationToken);

        private async Task<IActionResult> StreamBlobAsync(string containerName, string blobName, string fallbackContentType, bool enableRangeProcessing, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                return NotFound();
            }

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            try
            {
                Response.Headers.CacheControl = "public, max-age=86400";

                // Range processing requires a seekable stream so the framework can serve byte ranges.
                // This lets the preview player scrub, and is also required by Safari/iOS, which refuse
                // to play <audio> without range support. OpenReadAsync returns a seekable stream that
                // fetches ranges lazily, so we get this without buffering the whole blob in memory.
                if (enableRangeProcessing)
                {
                    var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                    var rangeContentType = string.IsNullOrWhiteSpace(properties.Value.ContentType)
                        ? fallbackContentType
                        : properties.Value.ContentType;

                    var seekableStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
                    return File(seekableStream, rangeContentType, enableRangeProcessing: true);
                }

                var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                var contentType = string.IsNullOrWhiteSpace(download.Value.Details.ContentType)
                    ? fallbackContentType
                    : download.Value.Details.ContentType;

                return File(download.Value.Content, contentType);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (RequestFailedException ex)
            {
                logger.LogWarning(ex, "Failed to stream blob {Blob} from container {Container}.", blobName, containerName);
                return StatusCode(StatusCodes.Status502BadGateway);
            }
        }
    }
}
