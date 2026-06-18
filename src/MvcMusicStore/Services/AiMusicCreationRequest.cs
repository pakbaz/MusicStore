namespace MvcMusicStore.Services
{
    public class AiMusicCreationRequest
    {
        public required string GenreName { get; init; }

        public required string StyleDirection { get; init; }

        public required string Mood { get; init; }

        public required string Instrumentation { get; init; }

        public int TempoBpm { get; init; }
    }
}
