using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Generates original music by calling the ACE-Step music generation service (separate container app),
    /// stores the produced audio in Azure Blob Storage, and returns catalog-ready metadata plus the audio URL.
    /// </summary>
    public class AceStepMusicCreationService : IAiMusicCreationService
    {
        private static readonly string[] CoreNouns = ["Canvas", "Signal", "Orbit", "Echo", "Spectrum", "Pulse", "Horizon", "Waves"];
        private static readonly string[] RestrictedTerms =
        [
            "copy", "clone", "cover", "remake", "reproduce", "exactly like", "same as", "verbatim"
        ];

        private readonly HttpClient httpClient;
        private readonly BlobServiceClient blobServiceClient;
        private readonly AiMusicCreationOptions options;
        private readonly MusicGenOptions musicGenOptions;
        private readonly StorageOptions storageOptions;
        private readonly ILogger<AceStepMusicCreationService> logger;

        public AceStepMusicCreationService(
            HttpClient httpClient,
            BlobServiceClient blobServiceClient,
            IOptions<AiMusicCreationOptions> optionsAccessor,
            IOptions<MusicGenOptions> musicGenOptionsAccessor,
            IOptions<StorageOptions> storageOptionsAccessor,
            ILogger<AceStepMusicCreationService> logger)
        {
            this.httpClient = httpClient;
            this.blobServiceClient = blobServiceClient;
            options = optionsAccessor.Value;
            musicGenOptions = musicGenOptionsAccessor.Value;
            storageOptions = storageOptionsAccessor.Value;
            this.logger = logger;

            if (httpClient.Timeout == TimeSpan.FromSeconds(100) && musicGenOptions.TimeoutSeconds > 0)
            {
                httpClient.Timeout = TimeSpan.FromSeconds(musicGenOptions.TimeoutSeconds);
            }
        }

        public async Task<AiMusicCreationResult> GenerateAsync(AiMusicCreationRequest request, CancellationToken cancellationToken = default)
        {
            string userPrompt = (request.Prompt ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new InvalidOperationException("Please describe the music you want to generate.");
            }

            EnsureOriginalitySafeInput(userPrompt);

            string title = BuildTitle(userPrompt);
            string artistName = BuildArtistName(userPrompt);
            string prompt = BuildModelPrompt(userPrompt);

            string? audioUrl = null;
            int durationSeconds = request.DurationSeconds > 0
                ? request.DurationSeconds
                : Math.Max(1, musicGenOptions.DefaultDurationSeconds);

            if (string.IsNullOrWhiteSpace(musicGenOptions.BaseUrl))
            {
                logger.LogWarning("MusicGen BaseUrl is not configured; returning metadata without generated audio.");
            }
            else
            {
                try
                {
                    var generateUrl = musicGenOptions.BaseUrl!.TrimEnd('/') + "/generate";
                    using var response = await httpClient.PostAsJsonAsync(generateUrl, new GenerateRequest
                    {
                        Prompt = prompt,
                        DurationSeconds = durationSeconds,
                        Bpm = null,
                        Format = "mp3"
                    }, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);
                        logger.LogError("Music generation service returned {StatusCode}: {Body}", response.StatusCode, body);
                        throw new InvalidOperationException("The music generation service was unable to create a track. Please try again.");
                    }

                    var generated = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken)
                        ?? throw new InvalidOperationException("The music generation service returned an empty response.");

                    if (string.IsNullOrWhiteSpace(generated.AudioBase64))
                    {
                        throw new InvalidOperationException("The music generation service returned no audio.");
                    }

                    byte[] audioBytes = Convert.FromBase64String(generated.AudioBase64);
                    durationSeconds = generated.DurationSeconds > 0 ? generated.DurationSeconds : durationSeconds;
                    audioUrl = await UploadAudioAsync(audioBytes, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(ex, "Unable to reach the music generation service at {BaseUrl}.", musicGenOptions.BaseUrl);
                    throw new InvalidOperationException("The music generation service is currently unavailable. Please try again shortly.");
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "Music generation timed out after {Timeout}s.", musicGenOptions.TimeoutSeconds);
                    throw new InvalidOperationException("The music generation request took too long. Try a shorter track or try again.");
                }
            }

            return new AiMusicCreationResult
            {
                Title = title,
                ArtistName = artistName,
                AlbumArtUrl = options.PlaceholderAlbumArtUrl,
                AudioUrl = audioUrl,
                DurationSeconds = audioUrl is null ? 0 : durationSeconds,
                SuggestedPrice = options.DefaultPrice,
                OriginalityStatement = "Original AI-generated instrumental composed locally by the ACE-Step model from your text prompt. " +
                    "It is an original work, not a copy of any specific copyrighted recording."
            };
        }

        private async Task<string> UploadAudioAsync(byte[] audioBytes, CancellationToken cancellationToken)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(storageOptions.MusicContainer);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobName = $"{Guid.NewGuid():N}.mp3";
            var blobClient = containerClient.GetBlobClient(blobName);

            using var stream = new MemoryStream(audioBytes);
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "audio/mpeg" } },
                cancellationToken);

            return "/media/music/" + blobName;
        }

        private static string BuildModelPrompt(string userPrompt)
        {
            string trimmed = userPrompt.Trim();
            if (trimmed.IndexOf("instrumental", StringComparison.OrdinalIgnoreCase) < 0)
            {
                trimmed += ", instrumental";
            }

            return trimmed + ". Original, high quality, cohesive arrangement.";
        }

        private static string BuildTitle(string userPrompt)
        {
            string[] words = userPrompt.Split(
                [' ', '\t', '\n', '\r', ',', '.', ';', ':', '-'],
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                return "Untitled AI Track";
            }

            string head = string.Join(' ', words.Take(6));
            string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(head.ToLowerInvariant());
            return title.Length > 60 ? title[..60].TrimEnd() : title;
        }

        private static string BuildArtistName(string userPrompt)
        {
            int token = StableToken(userPrompt);
            string noun = CoreNouns[token % CoreNouns.Length];
            return $"AI {noun} Collective";
        }

        private static void EnsureOriginalitySafeInput(string text)
        {
            string normalized = text.Trim().ToLowerInvariant();
            foreach (string term in RestrictedTerms)
            {
                if (normalized.Contains(term, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Prompts must avoid copying or recreating specific existing songs or artists.");
                }
            }
        }

        private static int StableToken(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        }

        private sealed class GenerateRequest
        {
            [JsonPropertyName("prompt")]
            public required string Prompt { get; init; }

            [JsonPropertyName("durationSeconds")]
            public int DurationSeconds { get; init; }

            [JsonPropertyName("bpm")]
            public int? Bpm { get; init; }

            [JsonPropertyName("format")]
            public string Format { get; init; } = "mp3";
        }

        private sealed class GenerateResponse
        {
            [JsonPropertyName("audioBase64")]
            public string? AudioBase64 { get; init; }

            [JsonPropertyName("format")]
            public string? Format { get; init; }

            [JsonPropertyName("durationSeconds")]
            public int DurationSeconds { get; init; }

            [JsonPropertyName("seed")]
            public long Seed { get; init; }

            [JsonPropertyName("model")]
            public string? Model { get; init; }
        }
    }
}
