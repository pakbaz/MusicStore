using System;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// The featured sale promoted on the homepage as the "Deal of the Day", paired with the
    /// album it spotlights and that album's effective pricing for the countdown panel.
    /// </summary>
    public class DealOfTheDay
    {
        public required Sale Sale { get; init; }
        public required Album Album { get; init; }
        public required AlbumPricing Pricing { get; init; }

        public DateTime EndsUtc => Sale.EndDateUtc;
    }
}
