using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurePerformanceTest
{
    public interface ICache<TKey, TValue>
    {
        TValue GetOrAdd(TKey key, Lazy<TValue> lazyValue);
    }

    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private ConcurrentDictionary<TKey, TValue> cache;

        public MemoryCache()
        {
            cache = new ConcurrentDictionary<TKey, TValue>();
        }

        public TValue GetOrAdd(TKey key, Lazy<TValue> lazyValue)
        {            
            var value = cache.GetOrAdd(key, _ => lazyValue.Value);
            Trace.WriteLine(string.Format("Memory cache has {0} elements", cache.Count));
            return value;
        }
    }
}
