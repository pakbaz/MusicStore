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
            => StreamBlobAsync(storageOptions.MusicContainer, blobName, "application/octet-stream", cancellationToken);

        private async Task<IActionResult> StreamBlobAsync(string containerName, string blobName, string fallbackContentType, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                return NotFound();
            }

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            try
            {
                var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                var contentType = string.IsNullOrWhiteSpace(download.Value.Details.ContentType)
                    ? fallbackContentType
                    : download.Value.Details.ContentType;

                Response.Headers.CacheControl = "public, max-age=86400";
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
