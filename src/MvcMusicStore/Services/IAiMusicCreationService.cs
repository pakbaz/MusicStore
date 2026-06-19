namespace MvcMusicStore.Services
{
    public interface IAiMusicCreationService
    {
        Task<AiMusicCreationResult> GenerateAsync(AiMusicCreationRequest request, CancellationToken cancellationToken = default);
    }
}
