namespace MvcMusicStore.Services
{
    public interface IAiMusicCreationService
    {
        AiMusicCreationResult Generate(AiMusicCreationRequest request);
    }
}
