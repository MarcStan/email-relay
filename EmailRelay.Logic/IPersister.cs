using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailRelay.Logic
{
    public interface IPersister
    {
        Task PersistAsync(string eventName, Action<Dictionary<string, string>> dict = null);
    }
}
