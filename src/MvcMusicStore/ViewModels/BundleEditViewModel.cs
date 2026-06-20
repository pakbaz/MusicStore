using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.ViewModels
{
    public class BundleEditViewModel
    {
        public int BundleId { get; set; }

        [Required]
        [StringLength(160, MinimumLength = 2)]
        public string? Title { get; set; }

        [StringLength(1024)]
        [DataType(DataType.MultilineText)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 1000.00)]
        [DataType(DataType.Currency)]
        [DisplayName("Bundle price")]
        public decimal BundlePrice { get; set; }

        [DisplayName("Active")]
        public bool IsActive { get; set; } = true;

        [DisplayName("Albums in bundle")]
        public List<int> SelectedAlbumIds { get; set; } = new();

        public List<AlbumChoice> AlbumChoices { get; set; } = new();
    }

    public class AlbumChoice
    {
        public int AlbumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
