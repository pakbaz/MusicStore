using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class AiMusicController : Controller
    {
        private static readonly string[] StyleDirections =
        [
            "Cinematic",
            "Ambient",
            "Minimalist",
            "Groove-forward",
            "Orchestral",
            "Experimental"
        ];

        private static readonly string[] MoodOptions =
        [
            "Uplifting",
            "Calm",
            "Energetic",
            "Dark",
            "Hopeful",
            "Reflective"
        ];

        private static readonly string[] InstrumentationOptions =
        [
            "Synths and drums",
            "Guitar-driven band",
            "Piano and strings",
            "Electronic pulse",
            "Percussion ensemble",
            "Hybrid orchestral"
        ];

        private readonly MusicStoreEntities db;
        private readonly IAiMusicCreationService aiMusicCreationService;

        public AiMusicController(MusicStoreEntities db, IAiMusicCreationService aiMusicCreationService)
        {
            this.db = db;
            this.aiMusicCreationService = aiMusicCreationService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var model = new AiMusicGenerationRequestViewModel();
            PopulateSelectionLists(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(AiMusicGenerationRequestViewModel model)
        {
            Genre? genre = db.Genres.SingleOrDefault(g => g.GenreId == model.GenreId);
            if (genre == null)
            {
                ModelState.AddModelError(nameof(model.GenreId), "Please choose a valid genre.");
            }

            ValidateSelection(ModelState, model.StyleDirection, StyleDirections, nameof(model.StyleDirection), "Please choose a valid style direction.");
            ValidateSelection(ModelState, model.Mood, MoodOptions, nameof(model.Mood), "Please choose a valid mood.");
            ValidateSelection(ModelState, model.Instrumentation, InstrumentationOptions, nameof(model.Instrumentation), "Please choose valid instrumentation.");

            if (!ModelState.IsValid)
            {
                PopulateSelectionLists(model);
                return View(model);
            }

            try
            {
                AiMusicCreationResult generationResult = aiMusicCreationService.Generate(new AiMusicCreationRequest
                {
                    GenreName = genre!.Name!,
                    StyleDirection = model.StyleDirection!,
                    Mood = model.Mood!,
                    Instrumentation = model.Instrumentation!,
                    TempoBpm = model.TempoBpm
                });

                Artist artist = db.Artists.SingleOrDefault(a => a.Name == generationResult.ArtistName)
                    ?? db.Artists.Add(new Artist { Name = generationResult.ArtistName }).Entity;

                Album album = new()
                {
                    Title = generationResult.Title,
                    GenreId = model.GenreId,
                    Artist = artist,
                    Price = generationResult.SuggestedPrice,
                    AlbumArtUrl = generationResult.AlbumArtUrl
                };

                db.Albums.Add(album);
                db.SaveChanges();

                return View("Result", new AiMusicGenerationResultViewModel
                {
                    AlbumId = album.AlbumId,
                    Genre = genre.Name!,
                    StyleDirection = model.StyleDirection!,
                    Mood = model.Mood!,
                    Instrumentation = model.Instrumentation!,
                    TempoBpm = model.TempoBpm,
                    Title = generationResult.Title,
                    ArtistName = generationResult.ArtistName,
                    SuggestedPrice = generationResult.SuggestedPrice,
                    OriginalityStatement = generationResult.OriginalityStatement
                });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.StyleDirection), ex.Message);
                PopulateSelectionLists(model);
                return View(model);
            }
        }

        private void PopulateSelectionLists(AiMusicGenerationRequestViewModel model)
        {
            model.Genres = db.Genres
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.GenreId.ToString(),
                    Text = g.Name!,
                    Selected = g.GenreId == model.GenreId
                })
                .ToList();

            model.StyleDirections = BuildSelectionList(StyleDirections, model.StyleDirection);
            model.MoodOptions = BuildSelectionList(MoodOptions, model.Mood);
            model.InstrumentationOptions = BuildSelectionList(InstrumentationOptions, model.Instrumentation);
        }

        private static IReadOnlyList<SelectListItem> BuildSelectionList(IEnumerable<string> values, string? selectedValue)
        {
            return values
                .Select(v => new SelectListItem
                {
                    Value = v,
                    Text = v,
                    Selected = string.Equals(v, selectedValue, StringComparison.Ordinal)
                })
                .ToList();
        }

        private static void ValidateSelection(ModelStateDictionary modelState, string? value, IEnumerable<string> validValues, string key, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!validValues.Contains(value, StringComparer.Ordinal))
            {
                modelState.AddModelError(key, errorMessage);
            }
        }
    }
}
