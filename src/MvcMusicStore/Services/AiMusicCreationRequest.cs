namespace MvcMusicStore.Services
{
    public class AiMusicCreationRequest
    {
        /// <summary>
        /// Free-form description of the music to generate (genre, style, mood, instrumentation, tempo, etc.).
        /// </summary>
        public required string Prompt { get; init; }

        /// <summary>
        /// Requested track length in seconds. When 0, the service default is used.
        /// </summary>
        public int DurationSeconds { get; init; }
    }
}
