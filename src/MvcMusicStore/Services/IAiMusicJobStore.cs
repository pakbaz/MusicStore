namespace MvcMusicStore.Services
{
    public interface IAiMusicJobStore
    {
        AiMusicJob Create(string prompt, int durationSeconds);

        AiMusicJob? Get(string id);
    }
}
