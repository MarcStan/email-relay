using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public class BlobStoragePersister : IPersister
    {
        private readonly CloudBlobClient _client;
        private readonly string _containerName;

        public BlobStoragePersister(string connectionString, string containerName)
        {
            var storageAccount = StorageAccount.NewFromConnectionString(connectionString);
            _client = storageAccount.CreateCloudBlobClient();
            _containerName = containerName;
        }

        public async Task PersistAsync(string blobName, byte[] data)
        {
            var blob = await GetBlobAsync(blobName);
            await blob.UploadFromByteArrayAsync(data, 0, data.Length);
        }

        public async Task PersistAsync(string blobName, string text)
        {
            var blob = await GetBlobAsync(blobName);
            await blob.UploadTextAsync(text);
        }

        private async Task<CloudBlockBlob> GetBlobAsync(string blobName)
        {
            var container = _client.GetContainerReference(_containerName);
            await container.CreateIfNotExistsAsync();
            return container.GetBlockBlobReference(blobName);
        }
    }
}
