using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class AiMusicController : Controller
    {
        private const int MinDurationSeconds = 10;
        private const int MaxDurationSeconds = 60;
        private const int MaxPromptLength = 600;

        private static readonly IReadOnlyList<AiMusicSamplePrompt> SamplePromptLibrary =
        [
            new() { Label = "Cinematic / Uplifting", Prompt = "Uplifting cinematic orchestral piece with soaring strings, warm brass and a triumphant build, around 110 BPM" },
            new() { Label = "Lo-fi / Chill", Prompt = "Chill lo-fi hip hop beat with warm Rhodes piano, dusty vinyl crackle, mellow bass and a relaxed head-nod groove" },
            new() { Label = "Electronic / Energetic", Prompt = "Energetic electronic dance track with punchy synth bass, bright plucky arps and a driving four-on-the-floor beat" },
            new() { Label = "Ambient / Dark", Prompt = "Dark ambient soundscape with deep evolving drones, icy pads and distant metallic echoes, slow and brooding" },
            new() { Label = "Jazz / Reflective", Prompt = "Smooth late-night jazz trio with brushed drums, walking upright bass and expressive improvised piano" },
            new() { Label = "Folk / Hopeful", Prompt = "Hopeful acoustic folk instrumental with fingerpicked guitar, gentle strings and light hand percussion" },
            new() { Label = "Epic / Trailer", Prompt = "Epic orchestral trailer theme with thundering taiko drums, choir swells and heroic french horns" },
            new() { Label = "Funk / Groove", Prompt = "Funky upbeat groove with slap bass, wah-wah guitar, tight horn stabs and a danceable rhythm" }
        ];

        private static readonly (string Keyword, string Genre)[] GenreHints =
        [
            ("orchestral", "Classical"), ("cinematic", "Classical"), ("symphony", "Classical"),
            ("piano and strings", "Classical"), ("trailer", "Classical"), ("choir", "Classical"),
            ("lo-fi", "Electronic"), ("lofi", "Electronic"), ("edm", "Electronic"), ("synth", "Electronic"),
            ("techno", "Electronic"), ("house", "Electronic"), ("ambient", "Electronic"), ("dance", "Electronic"),
            ("swing", "Jazz"), ("bebop", "Jazz"), ("saxophone", "Jazz"),
            ("funk", "R&B"), ("groove", "R&B"), ("soul", "R&B"),
            ("guitar", "Rock"), ("band", "Rock"),
            ("folk", "Country"), ("acoustic", "Country"),
            ("hip hop", "Rap"), ("hiphop", "Rap"), ("trap", "Rap"), ("beat", "Rap"),
            ("heavy", "Metal"), ("thrash", "Metal"),
            ("salsa", "Latin"), ("bossa", "Latin")
        ];

        private readonly IServiceScopeFactory scopeFactory;
        private readonly IAiMusicJobStore jobStore;
        private readonly ILogger<AiMusicController> logger;

        public AiMusicController(
            IServiceScopeFactory scopeFactory,
            IAiMusicJobStore jobStore,
            ILogger<AiMusicController> logger)
        {
            this.scopeFactory = scopeFactory;
            this.jobStore = jobStore;
            this.logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new AiMusicGenerationRequestViewModel
            {
                SamplePrompts = SamplePromptLibrary
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Start(string? prompt, int durationSeconds)
        {
            prompt = (prompt ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return BadRequest(new { error = "Please describe the music you want to generate." });
            }

            if (prompt.Length > MaxPromptLength)
            {
                return BadRequest(new { error = $"Please keep your description under {MaxPromptLength} characters." });
            }

            int duration = Math.Clamp(durationSeconds <= 0 ? 30 : durationSeconds, MinDurationSeconds, MaxDurationSeconds);

            AiMusicJob job = jobStore.Create(prompt, duration);
            _ = Task.Run(() => RunGenerationAsync(job.Id));

            return Accepted(new { jobId = job.Id });
        }

        [HttpGet]
        public IActionResult Status(string id)
        {
            AiMusicJob? job = jobStore.Get(id);
            if (job is null)
            {
                return NotFound(new { error = "This generation could not be found. It may have expired - please start a new one." });
            }

            int elapsedSeconds = (int)(DateTimeOffset.UtcNow - job.CreatedAt).TotalSeconds;

            return Ok(new
            {
                status = job.Status.ToString().ToLowerInvariant(),
                elapsedSeconds,
                error = job.Error,
                result = job.Status == AiMusicJobStatus.Succeeded
                    ? new
                    {
                        albumId = job.AlbumId,
                        title = job.Title,
                        artistName = job.ArtistName,
                        genre = job.Genre,
                        audioUrl = job.AudioUrl,
                        durationSeconds = job.ResultDurationSeconds,
                        suggestedPrice = job.SuggestedPrice,
                        suggestedPriceDisplay = job.SuggestedPrice.ToString("C"),
                        originalityStatement = job.OriginalityStatement
                    }
                    : null
            });
        }

        private async Task RunGenerationAsync(string jobId)
        {
            AiMusicJob? job = jobStore.Get(jobId);
            if (job is null)
            {
                return;
            }

            job.Status = AiMusicJobStatus.Running;

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                IAiMusicCreationService creationService = scope.ServiceProvider.GetRequiredService<IAiMusicCreationService>();
                MusicStoreEntities db = scope.ServiceProvider.GetRequiredService<MusicStoreEntities>();

                AiMusicCreationResult result = await creationService.GenerateAsync(new AiMusicCreationRequest
                {
                    Prompt = job.Prompt,
                    DurationSeconds = job.DurationSeconds
                });

                Genre genre = await ResolveGenreAsync(db, job.Prompt);

                Artist? artist = await db.Artists.SingleOrDefaultAsync(a => a.Name == result.ArtistName);
                if (artist is null)
                {
                    artist = new Artist
                    {
                        ArtistId = await db.NextArtistIdAsync(),
                        Name = result.ArtistName
                    };
                    db.Artists.Add(artist);
                }

                Album album = new()
                {
                    AlbumId = await db.NextAlbumIdAsync(),
                    Title = result.Title,
                    GenreId = genre.GenreId,
                    GenreName = genre.Name,
                    ArtistId = artist.ArtistId,
                    ArtistName = artist.Name,
                    Price = result.SuggestedPrice,
                    AlbumArtUrl = result.AlbumArtUrl,
                    AudioUrl = result.AudioUrl
                };

                db.Albums.Add(album);
                await db.SaveChangesAsync();

                job.AlbumId = album.AlbumId;
                job.Title = result.Title;
                job.ArtistName = result.ArtistName;
                job.Genre = genre.Name;
                job.AudioUrl = result.AudioUrl;
                job.ResultDurationSeconds = result.DurationSeconds;
                job.SuggestedPrice = result.SuggestedPrice;
                job.OriginalityStatement = result.OriginalityStatement;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = AiMusicJobStatus.Succeeded;
            }
            catch (InvalidOperationException ex)
            {
                job.Error = ex.Message;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = AiMusicJobStatus.Failed;
                logger.LogWarning(ex, "AI music generation rejected for job {JobId}.", jobId);
            }
            catch (Exception ex)
            {
                job.Error = "Something went wrong while generating your track. Please try again in a moment.";
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = AiMusicJobStatus.Failed;
                logger.LogError(ex, "Unexpected error generating AI music for job {JobId}.", jobId);
            }
        }

        private static async Task<Genre> ResolveGenreAsync(MusicStoreEntities db, string prompt)
        {
            List<Genre> genres = await db.Genres.ToListAsync();
            string lower = prompt.ToLowerInvariant();

            Genre? direct = genres.FirstOrDefault(g =>
                !string.IsNullOrEmpty(g.Name) && lower.Contains(g.Name!.ToLowerInvariant()));
            if (direct is not null)
            {
                return direct;
            }

            foreach ((string keyword, string genreName) in GenreHints)
            {
                if (lower.Contains(keyword))
                {
                    Genre? hinted = genres.FirstOrDefault(g =>
                        string.Equals(g.Name, genreName, StringComparison.OrdinalIgnoreCase));
                    if (hinted is not null)
                    {
                        return hinted;
                    }
                }
            }

            return genres.FirstOrDefault(g => string.Equals(g.Name, "Electronic", StringComparison.OrdinalIgnoreCase))
                ?? genres.FirstOrDefault()
                ?? throw new InvalidOperationException("No genres are configured in the catalog.");
        }
    }
}
