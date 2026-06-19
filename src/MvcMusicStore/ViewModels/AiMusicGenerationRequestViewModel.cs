using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MvcMusicStore.ViewModels
{
    public class AiMusicGenerationRequestViewModel
    {
        [Required]
        [Display(Name = "Genre")]
        public int GenreId { get; set; }

        [Required]
        [Display(Name = "Style direction")]
        public string? StyleDirection { get; set; }

        [Required]
        public string? Mood { get; set; }

        [Required]
        public string? Instrumentation { get; set; }

        [Range(40, 220)]
        [Display(Name = "Tempo (BPM)")]
        public int TempoBpm { get; set; } = 120;

        public IReadOnlyList<SelectListItem> Genres { get; set; } = [];

        public IReadOnlyList<SelectListItem> StyleDirections { get; set; } = [];

        public IReadOnlyList<SelectListItem> MoodOptions { get; set; } = [];

        public IReadOnlyList<SelectListItem> InstrumentationOptions { get; set; } = [];
    }
}
