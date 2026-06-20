using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace MvcMusicStore.Services
{
    /// <summary>
    /// Generates collision-free sequential <see cref="int"/> ids using a dedicated Cosmos
    /// "Counters" container. Each logical sequence is a single document
    /// (<c>{ "id": "&lt;name&gt;", "value": &lt;int&gt; }</c>, partition key <c>/id</c>) and a new id is
    /// allocated with an atomic server-side <see cref="PatchOperation.Increment(string, long)"/>.
    ///
    /// Because the increment is a single-document operation, Cosmos executes it atomically, so
    /// concurrent callers always receive distinct values and the allocation never scans the
    /// container. The one-time <c>MAX(id)</c> scan used to seed a brand-new counter happens at most
    /// once per sequence and is intended to run at startup (see
    /// <c>MusicStoreEntities.EnsureSequencesInitializedAsync</c>), off the insert path.
    ///
    /// Stream APIs are used throughout so the behavior does not depend on the Cosmos client's JSON
    /// serializer and the 404 (missing counter) / 409 (concurrent create) cases are handled via
    /// status codes rather than exceptions.
    /// </summary>
    public sealed class CosmosSequenceGenerator
    {
        private readonly Container _counters;

        public CosmosSequenceGenerator(CosmosClient client, string databaseId, string containerId)
        {
            _counters = client.GetContainer(databaseId, containerId);
        }

        /// <summary>
        /// Atomically allocates and returns the next id for <paramref name="name"/>. If the counter
        /// document does not exist yet it is seeded once from <paramref name="seedCurrentMaxAsync"/>
        /// (the current maximum id, or 0 when empty) and then incremented, so the first allocated id
        /// is <c>max + 1</c>.
        /// </summary>
        public async Task<int> NextAsync(
            string name,
            Func<CancellationToken, Task<int>> seedCurrentMaxAsync,
            CancellationToken cancellationToken = default)
        {
            var partitionKey = new PartitionKey(name);

            using (var response = await IncrementAsync(name, partitionKey, cancellationToken))
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    EnsureSuccess(response);
                    return ReadValue(response.Content);
                }
            }

            // The counter does not exist yet: seed it once from the current max, then increment.
            await EnsureCounterCreatedAsync(name, partitionKey, seedCurrentMaxAsync, cancellationToken);

            using (var response = await IncrementAsync(name, partitionKey, cancellationToken))
            {
                EnsureSuccess(response);
                return ReadValue(response.Content);
            }
        }

        /// <summary>
        /// Ensures the counter for <paramref name="name"/> exists, seeding it from the current max
        /// when absent, without consuming an id. Safe to call repeatedly; once the counter exists
        /// this is a single point read and never scans the container.
        /// </summary>
        public async Task EnsureInitializedAsync(
            string name,
            Func<CancellationToken, Task<int>> seedCurrentMaxAsync,
            CancellationToken cancellationToken = default)
        {
            var partitionKey = new PartitionKey(name);

            using (var response = await _counters.ReadItemStreamAsync(name, partitionKey, cancellationToken: cancellationToken))
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    EnsureSuccess(response);
                    return;
                }
            }

            await EnsureCounterCreatedAsync(name, partitionKey, seedCurrentMaxAsync, cancellationToken);
        }

        private Task<ResponseMessage> IncrementAsync(string name, PartitionKey partitionKey, CancellationToken cancellationToken)
        {
            var operations = new[] { PatchOperation.Increment("/value", 1) };
            return _counters.PatchItemStreamAsync(name, partitionKey, operations, cancellationToken: cancellationToken);
        }

        private async Task EnsureCounterCreatedAsync(
            string name,
            PartitionKey partitionKey,
            Func<CancellationToken, Task<int>> seedCurrentMaxAsync,
            CancellationToken cancellationToken)
        {
            int seed = await seedCurrentMaxAsync(cancellationToken);

            var payload = JsonSerializer.SerializeToUtf8Bytes(new { id = name, value = seed });
            using var stream = new MemoryStream(payload);
            using var response = await _counters.CreateItemStreamAsync(stream, partitionKey, cancellationToken: cancellationToken);

            // A concurrent caller may have created the counter first; that is fine — the atomic
            // increments that follow still hand out distinct values.
            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                EnsureSuccess(response);
            }
        }

        private static int ReadValue(Stream content)
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.GetProperty("value").GetInt32();
        }

        private static void EnsureSuccess(ResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Cosmos counter operation failed with status {(int)response.StatusCode} ({response.StatusCode}): {response.ErrorMessage}");
            }
        }
    }
}
