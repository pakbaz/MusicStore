using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MvcMusicStore.Services
{
    public class LocalAiMusicCreationService : IAiMusicCreationService
    {
        private static readonly string[] TempoDescriptors = ["Nocturne", "Motion", "Drive", "Lift"];
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

        public AiMusicCreationResult Generate(AiMusicCreationRequest request)
        {
            EnsureOriginalitySafeInput(request.StyleDirection);

            string style = ToTitleCase(request.StyleDirection);
            string mood = ToTitleCase(request.Mood);
            string instrumentation = ToTitleCase(request.Instrumentation);
            string genre = ToTitleCase(request.GenreName);
            int tempoBucket = GetTempoBucket(request.TempoBpm);

            int styleToken = StableToken($"{genre}|{style}|{mood}|{instrumentation}|{request.TempoBpm}");
            string tempoDescriptor = TempoDescriptors[tempoBucket];
            string noun = CoreNouns[styleToken % CoreNouns.Length];

            return new AiMusicCreationResult
            {
                Title = $"{mood} {genre} {noun} ({request.TempoBpm} BPM · {style})",
                ArtistName = $"AI {instrumentation} Collective",
                AlbumArtUrl = options.PlaceholderAlbumArtUrl,
                SuggestedPrice = options.DefaultPrice,
                OriginalityStatement = $"Original AI-generated composition using {genre} genre direction with {style} style guidance. " +
                    "This output is generated from abstract musical parameters and is not a copy of any specific copyrighted track."
            };
        }

        private static void EnsureOriginalitySafeInput(string styleDirection)
        {
            string normalized = styleDirection.Trim().ToLowerInvariant();
            foreach (string term in RestrictedTerms)
            {
                if (normalized.Contains(term, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Style direction requests must avoid copying or recreating specific existing songs.");
                }
            }
        }

        private static int StableToken(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        }

        private static int GetTempoBucket(int tempoBpm)
        {
            if (tempoBpm <= 80)
            {
                return 0;
            }

            if (tempoBpm <= 115)
            {
                return 1;
            }

            if (tempoBpm <= 145)
            {
                return 2;
            }

            return 3;
        }

        private static string ToTitleCase(string input)
        {
            string normalized = input.Trim().ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
        }
    }
}
