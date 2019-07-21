using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public interface IPersister
    {
        Task PersistAsync(string blobName, string text);

        Task PersistAsync(string blobName, byte[] data);
    }
}
