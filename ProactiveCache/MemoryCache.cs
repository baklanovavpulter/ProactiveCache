using ProactiveCache.Internal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProactiveCache
{
    public class MemoryCache<TKey, TValue> : ICache<TKey, TValue>
    {

        public const int CLEAN_FREQUENCY_SEC = 600;

        private readonly int _cleanFrequencySec;
        protected readonly ConcurrentDictionary<TKey, CacheEntry> _entries;
        private long _nextClean;
        private Task _clean;

        protected struct CacheEntry
        {
            private readonly long _expireAt;
            public readonly ICacheEntry<TValue> Value;

            public CacheEntry(ICacheEntry<TValue> value, TimeSpan expire_ttl, long now_sec)
            {
                _expireAt = now_sec + expire_ttl.Ticks / TimeSpan.TicksPerSecond;
                Value = value;
            }

            public bool IsExpired(long now_sec) => now_sec >= _expireAt;
        }

        public int Count => _entries.Count;

        protected uint NowSec => ProCacheTimer.NowSec;

        public MemoryCache(int clean_frequency_sec = CLEAN_FREQUENCY_SEC)
        {
            _cleanFrequencySec = clean_frequency_sec;
            _nextClean = ProCacheTimer.NowSec + _cleanFrequencySec;
            _entries = new ConcurrentDictionary<TKey, CacheEntry>();
            _clean = Task.CompletedTask;
        }

        public void Set(TKey key, ICacheEntry<TValue> value, TimeSpan expiration_time)
        {
            var nowSec = ProCacheTimer.NowSec;
            var entry = new CacheEntry(value, expiration_time, nowSec);
            _entries.AddOrUpdate(key, entry, (k, v) => entry);

            StartScanForExpiredItemsIfNeeded(nowSec);
        }

        public bool TryGet(TKey key, out ICacheEntry<TValue> value)
        {
            if (!_entries.TryGetValue(key, out var entry) || entry.IsExpired(NowSec))
            {
                value = null;
                return false;
            }

            value = entry.Value;
            return true;
        }

        public void Remove(TKey key) => _entries.TryRemove(key, out var _);

        private void StartScanForExpiredItemsIfNeeded(long now_sec)
        {
            var nextExpirationScan = Volatile.Read(ref _nextClean);
            if (now_sec >= nextExpirationScan && _clean.IsCompleted && Interlocked.CompareExchange(ref _nextClean, now_sec + _cleanFrequencySec, nextExpirationScan) == nextExpirationScan)
            {
                _clean = Task.Factory.StartNew(ScanForExpiredItems, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            }
        }

        protected void ScanForExpiredItems()
        {
            var nowSec = ProCacheTimer.NowSec;
            foreach (var entry in _entries.ToArray())
            {
                if (entry.Value.IsExpired(nowSec))
                    _entries.TryRemove(entry.Key, out var _);
            }
        }
    }
}
