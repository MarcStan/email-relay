using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public interface IPersister
    {
        /// <summary>
        /// Persist data as a json object with the dictionary as property data.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="dict"></param>
        /// <returns></returns>
        Task PersistJsonAsync(string blobName, Action<Dictionary<string, string>> dict = null);

        Task PersistAsync(string blobName, byte[] data);
    }
}
