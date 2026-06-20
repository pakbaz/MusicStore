using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services
{
    public class LocalAiMusicCreationService : IAiMusicCreationService
    {
        private static readonly string[] CoreNouns = ["Canvas", "Signal", "Orbit", "Echo", "Spectrum", "Pulse", "Horizon", "Waves"];
        private static readonly string[] RestrictedTerms =
        [
            "copy",
            "clone",
            "cover",
            "remake",
            "reproduce",
            "exactly like",
            "same as",
            "verbatim"
        ];

        private readonly AiMusicCreationOptions options;

        public LocalAiMusicCreationService(IOptions<AiMusicCreationOptions> optionsAccessor)
        {
            options = optionsAccessor.Value;
        }

        public Task<AiMusicCreationResult> GenerateAsync(AiMusicCreationRequest request, CancellationToken cancellationToken = default)
        {
            string userPrompt = (request.Prompt ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new InvalidOperationException("Please describe the music you want to generate.");
            }

            EnsureOriginalitySafeInput(userPrompt);

            string[] words = userPrompt.Split(
                [' ', '\t', '\n', '\r', ',', '.', ';', ':', '-'],
                StringSplitOptions.RemoveEmptyEntries);
            string head = words.Length == 0 ? "Untitled AI Track" : string.Join(' ', words.Take(6));
            string title = ToTitleCase(head);
            int token = StableToken(userPrompt);
            string noun = CoreNouns[token % CoreNouns.Length];

            return Task.FromResult(new AiMusicCreationResult
            {
                Title = title.Length > 60 ? title[..60].TrimEnd() : title,
                ArtistName = $"AI {noun} Collective",
                AlbumArtUrl = options.PlaceholderAlbumArtUrl,
                SuggestedPrice = options.DefaultPrice,
                OriginalityStatement = "Original AI-generated instrumental composed from your text prompt. " +
                    "It is an original work, not a copy of any specific copyrighted recording."
            });
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

        private static string ToTitleCase(string input)
        {
            string normalized = input.Trim().ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
        }
    }
}
