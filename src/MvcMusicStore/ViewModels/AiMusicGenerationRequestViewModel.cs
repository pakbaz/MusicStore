namespace MvcMusicStore.ViewModels
{
    public class AiMusicSamplePrompt
    {
        public required string Label { get; init; }

        public required string Prompt { get; init; }
    }

    public class AiMusicGenerationRequestViewModel
    {
        public string? Prompt { get; set; }

        public int DurationSeconds { get; set; } = 15;

        public IReadOnlyList<int> DurationOptions { get; } = [10, 15, 30, 45, 60];

        public IReadOnlyList<AiMusicSamplePrompt> SamplePrompts { get; init; } = [];
    }
}
