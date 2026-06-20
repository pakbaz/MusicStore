namespace MvcMusicStore.Models
{
    /// <summary>
    /// Cosmos settings that the catalog <see cref="MusicStoreEntities"/> needs beyond what the EF
    /// provider exposes: the database name and the dedicated container used for atomic id counters.
    /// Registered as a singleton and injected into the context.
    /// </summary>
    public sealed record CosmosCatalogOptions(string DatabaseName, string CountersContainerName = "Counters");
}
