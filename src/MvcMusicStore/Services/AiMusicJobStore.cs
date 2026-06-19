using System.Collections.Concurrent;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// In-memory store for AI music generation jobs. Registered as a singleton so background
    /// generation and status polling share state within a replica. Web ingress session affinity
    /// keeps a browser's poll requests on the replica that owns the job.
    /// </summary>
    public class AiMusicJobStore : IAiMusicJobStore
    {
        private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, AiMusicJob> jobs = new();

        public AiMusicJob Create(string prompt, int durationSeconds)
        {
            PruneExpired();

            var job = new AiMusicJob
            {
                Id = Guid.NewGuid().ToString("N"),
                Prompt = prompt,
                DurationSeconds = durationSeconds
            };

            jobs[job.Id] = job;
            return job;
        }

        public AiMusicJob? Get(string id)
        {
            return jobs.TryGetValue(id, out AiMusicJob? job) ? job : null;
        }

        private void PruneExpired()
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - Retention;
            foreach (KeyValuePair<string, AiMusicJob> entry in jobs)
            {
                if (entry.Value.CreatedAt < cutoff)
                {
                    jobs.TryRemove(entry.Key, out _);
                }
            }
        }
    }
}
