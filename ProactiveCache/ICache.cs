using System;

namespace ProactiveCache
{
    public interface ICache<TKey, TValue>
    {
        void Set(TKey key, ICacheEntry<TValue> value, TimeSpan expiration_time);
        bool TryGet(TKey key, out ICacheEntry<TValue> value);
        void Remove(TKey key);
    }
}
