using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        public async Task PersistJsonAsync(string blobName, Action<Dictionary<string, object>> dict = null)
        {
            var blob = await GetBlobAsync(blobName);
            var data = new Dictionary<string, object>();
            dict?.Invoke(data);
            await blob.UploadTextAsync(JsonConvert.SerializeObject(new
            {
                data
            }, Formatting.Indented));
        }

        private async Task<CloudBlockBlob> GetBlobAsync(string blobName)
        {
            var container = _client.GetContainerReference(_containerName);
            await container.CreateIfNotExistsAsync();
            return container.GetBlockBlobReference(blobName);
        }
    }
}
